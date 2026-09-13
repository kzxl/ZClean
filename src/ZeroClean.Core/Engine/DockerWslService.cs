using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ZeroClean.Core.Engine;

public record WslVdiskInfo
{
    public required string DistroName { get; init; }
    public required string FilePath { get; init; }
    public long SizeBytes { get; init; }
    public string SizeFormatted => FormatSize(SizeBytes);
    public DateTime LastModified { get; init; }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {suffixes[order]}";
    }
}

public record WslCompactResult
{
    public bool Success { get; init; }
    public bool IsDryRun { get; init; }
    public string VdiskPath { get; init; } = "";
    public long InitialSizeBytes { get; init; }
    public long FinalSizeBytes { get; init; }
    public long ReclaimedBytes => Math.Max(0, InitialSizeBytes - FinalSizeBytes);
    public string DiskpartScript { get; init; } = "";
    public string Output { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

public record DockerItemUsage
{
    public required string Type { get; init; }
    public string TotalCount { get; init; } = "0";
    public string ActiveCount { get; init; } = "0";
    public string Size { get; init; } = "0 B";
    public string Reclaimable { get; init; } = "0 B";
}

public record DockerStatusReport
{
    public bool IsDockerInstalled { get; init; }
    public bool IsDockerRunning { get; init; }
    public IReadOnlyList<DockerItemUsage> Items { get; init; } = Array.Empty<DockerItemUsage>();
    public string RawOutput { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

public record DockerPruneResult
{
    public bool Success { get; init; }
    public bool IsDryRun { get; init; }
    public bool IncludeVolumes { get; init; }
    public string Command { get; init; } = "";
    public string Output { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// WSL2 and Docker storage reclamation manager.
/// Identifies oversized ext4.vhdx virtual disk files, generates diskpart compaction scripts,
/// and orchestrates Docker daemon prune routines.
/// </summary>
public class DockerWslService
{
    /// <summary>
    /// Scans standard Windows locations for WSL2 and Docker Desktop ext4.vhdx files.
    /// </summary>
    public Task<IReadOnlyList<WslVdiskInfo>> DiscoverWslVdisksAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<WslVdiskInfo>();

        if (!OperatingSystem.IsWindows())
            return Task.FromResult<IReadOnlyList<WslVdiskInfo>>(list);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var searchRoots = new List<string>();

        // 1. Packages folder for Store WSL distributions
        var packagesDir = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesDir))
        {
            try
            {
                foreach (var pkg in Directory.GetDirectories(packagesDir))
                {
                    var localState = Path.Combine(pkg, "LocalState");
                    if (Directory.Exists(localState))
                        searchRoots.Add(localState);
                }
            }
            catch { }
        }

        // 2. Docker WSL folders
        var dockerData = Path.Combine(localAppData, "Docker", "wsl");
        if (Directory.Exists(dockerData))
            searchRoots.Add(dockerData);

        // 3. User profile .wsl folder
        var wslUserDir = Path.Combine(userProfile, ".wsl");
        if (Directory.Exists(wslUserDir))
            searchRoots.Add(wslUserDir);

        foreach (var root in searchRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var vhdxFiles = Directory.GetFiles(root, "*.vhdx", SearchOption.AllDirectories);
                foreach (var vhdx in vhdxFiles)
                {
                    try
                    {
                        var fi = new FileInfo(vhdx);
                        var distroName = DetermineDistroName(vhdx);
                        list.Add(new WslVdiskInfo
                        {
                            DistroName = distroName,
                            FilePath = fi.FullName,
                            SizeBytes = fi.Length,
                            LastModified = fi.LastWriteTime
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        return Task.FromResult<IReadOnlyList<WslVdiskInfo>>(list);
    }

    /// <summary>
    /// Generates diskpart script content for shrinking an unmounted ext4.vhdx virtual disk.
    /// </summary>
    public string GenerateDiskpartScript(string vdiskPath)
    {
        return $"select vdisk file=\"{vdiskPath}\"\r\nattach vdisk readonly\r\ncompact vdisk\r\ndetach vdisk\r\n";
    }

    /// <summary>
    /// Compacts a WSL2 virtual disk via Windows diskpart utility.
    /// In dryRun mode, returns planned script and metrics without modifying the disk.
    /// </summary>
    public async Task<WslCompactResult> CompactVdiskAsync(
        string vdiskPath,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vdiskPath) || !File.Exists(vdiskPath))
        {
            return new WslCompactResult
            {
                Success = false,
                VdiskPath = vdiskPath,
                ErrorMessage = $"Virtual disk file not found: '{vdiskPath}'."
            };
        }

        var fullPath = Path.GetFullPath(vdiskPath);
        var initialSize = new FileInfo(fullPath).Length;
        var script = GenerateDiskpartScript(fullPath);

        if (dryRun)
        {
            return new WslCompactResult
            {
                Success = true,
                IsDryRun = true,
                VdiskPath = fullPath,
                InitialSizeBytes = initialSize,
                FinalSizeBytes = initialSize,
                DiskpartScript = script,
                Output = $"[DRY-RUN] WSL disk compact script prepared for '{Path.GetFileName(fullPath)}':\n{script}\nNote: Run with --execute and Administrator privileges to compact."
            };
        }

        if (!OperatingSystem.IsWindows())
        {
            return new WslCompactResult
            {
                Success = false,
                VdiskPath = fullPath,
                ErrorMessage = "WSL2 compact is only supported on Windows operating systems."
            };
        }

        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"compact_{Guid.NewGuid():N}.txt");

        try
        {
            // 1. Shutdown WSL to release file locks
            try
            {
                using var shutdownProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "wsl.exe",
                    Arguments = "--shutdown",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (shutdownProc != null)
                {
                    await shutdownProc.WaitForExitAsync(cancellationToken);
                }
            }
            catch { }

            // 2. Write temp diskpart script
            await File.WriteAllTextAsync(tempScriptPath, script, cancellationToken);

            // 3. Execute diskpart /s
            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{tempScriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var finalSize = new FileInfo(fullPath).Length;

            return new WslCompactResult
            {
                Success = process.ExitCode == 0,
                IsDryRun = false,
                VdiskPath = fullPath,
                InitialSizeBytes = initialSize,
                FinalSizeBytes = finalSize,
                DiskpartScript = script,
                Output = stdout,
                ErrorMessage = process.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()
            };
        }
        catch (Exception ex)
        {
            return new WslCompactResult
            {
                Success = false,
                VdiskPath = fullPath,
                InitialSizeBytes = initialSize,
                FinalSizeBytes = initialSize,
                ErrorMessage = $"Failed to compact WSL virtual disk: {ex.Message}"
            };
        }
        finally
        {
            if (File.Exists(tempScriptPath))
            {
                try { File.Delete(tempScriptPath); } catch { }
            }
        }
    }

    /// <summary>
    /// Evaluates current Docker daemon disk footprint using 'docker system df'.
    /// </summary>
    public async Task<DockerStatusReport> GetDockerStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "system df",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return new DockerStatusReport
                {
                    IsDockerInstalled = true,
                    IsDockerRunning = false,
                    ErrorMessage = $"Docker daemon not running or returned error: {stderr}".Trim()
                };
            }

            return ParseDockerDfOutput(stdout);
        }
        catch
        {
            return new DockerStatusReport
            {
                IsDockerInstalled = false,
                IsDockerRunning = false,
                ErrorMessage = "Docker command-line tool not found on system PATH."
            };
        }
    }

    /// <summary>
    /// Parses textual table output from 'docker system df'.
    /// </summary>
    public DockerStatusReport ParseDockerDfOutput(string output)
    {
        var items = new List<DockerItemUsage>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return new DockerStatusReport
            {
                IsDockerInstalled = true,
                IsDockerRunning = true,
                Items = items
            };
        }

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        // Header example: TYPE            TOTAL     ACTIVE    SIZE      RECLAIMABLE
        foreach (var line in lines)
        {
            if (line.StartsWith("TYPE", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = Regex.Match(line.Trim(), @"^(Images|Containers|Local Volumes|Build Cache)\s+([0-9]+)\s+([0-9]+)\s+([0-9\.]+\s*[A-Za-z]+)\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                items.Add(new DockerItemUsage
                {
                    Type = match.Groups[1].Value.Trim(),
                    TotalCount = match.Groups[2].Value.Trim(),
                    ActiveCount = match.Groups[3].Value.Trim(),
                    Size = match.Groups[4].Value.Trim(),
                    Reclaimable = match.Groups[5].Value.Trim()
                });
            }
        }

        return new DockerStatusReport
        {
            IsDockerInstalled = true,
            IsDockerRunning = true,
            Items = items,
            RawOutput = output
        };
    }

    /// <summary>
    /// Executes Docker prune routine to remove dangling images, stopped containers, build caches, and unused volumes.
    /// </summary>
    public async Task<DockerPruneResult> PruneDockerAsync(
        bool includeVolumes = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var pruneArgs = includeVolumes ? "system prune -f --volumes" : "system prune -f";
        var fullCommand = $"docker {pruneArgs} && docker builder prune -f";

        if (dryRun)
        {
            return new DockerPruneResult
            {
                Success = true,
                IsDryRun = true,
                IncludeVolumes = includeVolumes,
                Command = fullCommand,
                Output = $"[DRY-RUN] Prepared Docker prune command: {fullCommand}\nNote: Cleans stopped containers, dangling images, build cache" + (includeVolumes ? " and unused volumes." : ".")
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = pruneArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // Also builder prune
            try
            {
                using var builderProc = Process.Start(new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "builder prune -f",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (builderProc != null)
                {
                    await builderProc.WaitForExitAsync(cancellationToken);
                    stdout += "\n" + await builderProc.StandardOutput.ReadToEndAsync(cancellationToken);
                }
            }
            catch { }

            return new DockerPruneResult
            {
                Success = process.ExitCode == 0,
                IsDryRun = false,
                IncludeVolumes = includeVolumes,
                Command = fullCommand,
                Output = stdout,
                ErrorMessage = process.ExitCode == 0 ? null : stderr.Trim()
            };
        }
        catch (Exception ex)
        {
            return new DockerPruneResult
            {
                Success = false,
                Command = fullCommand,
                ErrorMessage = $"Failed to execute Docker prune: {ex.Message}"
            };
        }
    }

    private static string DetermineDistroName(string vdiskPath)
    {
        var lower = vdiskPath.ToLowerInvariant();
        if (lower.Contains(@"docker\wsl\data"))
            return "Docker Desktop Data";
        if (lower.Contains(@"docker\wsl\distro"))
            return "Docker Desktop Distro";
        if (lower.Contains("canonical") || lower.Contains("ubuntu"))
            return "Ubuntu WSL2";
        if (lower.Contains("debian"))
            return "Debian WSL2";
        if (lower.Contains("arch"))
            return "Arch WSL2";
        if (lower.Contains("alpine"))
            return "Alpine WSL2";

        var dirName = Path.GetFileName(Path.GetDirectoryName(vdiskPath) ?? "");
        return string.IsNullOrWhiteSpace(dirName) ? "WSL2 Virtual Disk" : dirName;
    }
}
