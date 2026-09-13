using System.Diagnostics;
using ZeroClean.Core.Contracts;

namespace ZeroClean.Core.Engine;

public enum DevRepoState
{
    Active,
    Dormant,
    DirtyWorktree
}

public record DevArtifactTarget
{
    public required string Path { get; init; }
    public required string ArtifactType { get; init; } // node_modules, bin/obj, target, .vs, venv
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
}

public record DevRepoInfo
{
    public required string RepoPath { get; init; }
    public required string RepoName { get; init; }
    public required DevRepoState State { get; init; }
    public required DateTime LastActivityTime { get; init; }
    public required List<DevArtifactTarget> Artifacts { get; init; } = new();
    public long TotalReclaimableBytes => Artifacts.Sum(a => a.SizeBytes);
    public int TotalArtifactFiles => Artifacts.Sum(a => a.FileCount);
}

public class DevWorkspaceService
{
    private static readonly string[] DisposableFolderNames =
    {
        "node_modules",
        "bin",
        "obj",
        "target",
        ".vs",
        "TestResults",
        "__pycache__",
        ".pytest_cache",
        "venv",
        ".venv"
    };

    private static readonly string[] RepoMarkerFiles =
    {
        ".git",
        "package.json",
        "Cargo.toml",
        "pyproject.toml",
        "go.mod"
    };

    private readonly ISafetyGuard _safetyGuard;

    public DevWorkspaceService(ISafetyGuard? safetyGuard = null)
    {
        _safetyGuard = safetyGuard ?? new SafetyGuard();
    }

    /// <summary>
    /// Scans a workspace path for code repositories, detects dormant projects, and measures disposable build artifacts.
    /// </summary>
    public async Task<IReadOnlyList<DevRepoInfo>> ScanWorkspacesAsync(
        string rootDirectory, 
        int dormantDaysThreshold = 30,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var repos = new List<DevRepoInfo>();
            if (!Directory.Exists(rootDirectory))
                return repos;

            progress?.Report($"Scanning developer workspaces under: {rootDirectory}...");

            var discoveredRepoDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DiscoverRepositories(new DirectoryInfo(rootDirectory), discoveredRepoDirs, 0, maxDepth: 4, cancellationToken);

            progress?.Report($"Found {discoveredRepoDirs.Count} candidate repositories. Inspecting Git state & artifacts...");

            foreach (var repoPath in discoveredRepoDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var repoInfo = AnalyzeRepository(repoPath, dormantDaysThreshold);
                    if (repoInfo.Artifacts.Count > 0)
                    {
                        repos.Add(repoInfo);
                    }
                }
                catch { }
            }

            // Order by reclaimable space descending
            repos.Sort((a, b) => b.TotalReclaimableBytes.CompareTo(a.TotalReclaimableBytes));
            return (IReadOnlyList<DevRepoInfo>)repos;
        }, cancellationToken);
    }

    private void DiscoverRepositories(DirectoryInfo dir, HashSet<string> repos, int depth, int maxDepth, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (depth > maxDepth) return;
        if (_safetyGuard.IsProtectedPath(dir.FullName)) return;

        bool isRepo = false;
        try
        {
            // Check if this directory contains a repo marker
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                dir.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).Any() ||
                RepoMarkerFiles.Any(m => File.Exists(Path.Combine(dir.FullName, m))))
            {
                isRepo = true;
                repos.Add(dir.FullName);
            }
        }
        catch { }

        // If it's a repository, do not recurse into child subprojects unless needed
        if (isRepo) return;

        try
        {
            foreach (var sub in dir.EnumerateDirectories())
            {
                ct.ThrowIfCancellationRequested();
                if (sub.Name.StartsWith('.') && sub.Name != ".git") continue;
                if (DisposableFolderNames.Contains(sub.Name, StringComparer.OrdinalIgnoreCase)) continue;

                DiscoverRepositories(sub, repos, depth + 1, maxDepth, ct);
            }
        }
        catch { }
    }

    private DevRepoInfo AnalyzeRepository(string repoPath, int dormantDaysThreshold)
    {
        var repoDir = new DirectoryInfo(repoPath);
        var lastActivity = repoDir.LastWriteTime;
        bool isDirty = false;

        // Check git status if .git directory is present
        var gitDir = Path.Combine(repoPath, ".git");
        if (Directory.Exists(gitDir))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "status --porcelain",
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        isDirty = true;
                    }
                }

                // Check last commit date
                var logPsi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "log -1 --format=%ct",
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var logProc = Process.Start(logPsi);
                if (logProc != null)
                {
                    string tsStr = logProc.StandardOutput.ReadToEnd().Trim();
                    logProc.WaitForExit(3000);
                    if (long.TryParse(tsStr, out var unixSeconds))
                    {
                        lastActivity = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
                    }
                }
            }
            catch { }
        }

        var state = isDirty 
            ? DevRepoState.DirtyWorktree 
            : (DateTime.Now - lastActivity).TotalDays >= dormantDaysThreshold 
                ? DevRepoState.Dormant 
                : DevRepoState.Active;

        var artifacts = new List<DevArtifactTarget>();
        ScanArtifactFolders(repoDir, artifacts, 0, maxDepth: 4);

        return new DevRepoInfo
        {
            RepoPath = repoPath,
            RepoName = repoDir.Name,
            State = state,
            LastActivityTime = lastActivity,
            Artifacts = artifacts
        };
    }

    private void ScanArtifactFolders(DirectoryInfo dir, List<DevArtifactTarget> artifacts, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        try
        {
            foreach (var sub in dir.EnumerateDirectories())
            {
                if (DisposableFolderNames.Contains(sub.Name, StringComparer.OrdinalIgnoreCase))
                {
                    long size = 0;
                    int count = 0;

                    try
                    {
                        foreach (var file in sub.EnumerateFiles("*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                size += file.Length;
                                count++;
                            }
                            catch { }
                        }
                    }
                    catch { }

                    if (count > 0)
                    {
                        artifacts.Add(new DevArtifactTarget
                        {
                            Path = sub.FullName,
                            ArtifactType = sub.Name.ToLowerInvariant(),
                            SizeBytes = size,
                            FileCount = count
                        });
                    }
                }
                else if (sub.Name != ".git")
                {
                    ScanArtifactFolders(sub, artifacts, depth + 1, maxDepth);
                }
            }
        }
        catch { }
    }
}
