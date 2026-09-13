using System.Diagnostics;
using Microsoft.Win32;

namespace CleanTool.Core.Engine;

[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
public record InstalledAppInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string DisplayVersion { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string InstallDate { get; init; } = "";
    public long EstimatedSizeBytes { get; init; }
    public string? InstallLocation { get; init; }
    public string? UninstallString { get; init; }
    public string? QuietUninstallString { get; init; }
    public bool IsBroken { get; init; }
    public RegistryHive SourceHive { get; init; } = RegistryHive.LocalMachine;
    public RegistryView SourceView { get; init; } = RegistryView.Default;
}

public record AppLeftoverFolder
{
    public required string FolderPath { get; init; }
    public required string FolderName { get; init; }
    public long EstimatedSizeBytes { get; init; }
    public int FileCount { get; init; }
    public DateTime LastModified { get; init; }
}

public record AppResidualAnalysis
{
    public required InstalledAppInfo App { get; init; }
    public required IReadOnlyList<string> RegistryKeysFound { get; init; }
    public required IReadOnlyList<AppLeftoverFolder> DirectoriesFound { get; init; }
    public long TotalResidualSizeBytes => DirectoriesFound.Sum(d => d.EstimatedSizeBytes);
}

public record UninstallExecutionResult
{
    public required bool Success { get; init; }
    public int ExitCode { get; init; }
    public required string CommandLine { get; init; }
    public bool WasDryRun { get; init; }
    public required string Message { get; init; }
}

[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class AppUninstallerService
{
    private static readonly HashSet<string> SystemFolderExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Windows", "Packages", "Temp", "Programs", "System",
        "assembly", "Common Files", "Internet Explorer", "Windows NT",
        "Windows Defender", "Windows Security", "ZeroUI"
    };

    public IReadOnlyList<InstalledAppInfo> GetInstalledApplications()
    {
        var apps = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);

        // HKLM 64-bit
        ReadUninstallRegistry(RegistryHive.LocalMachine, RegistryView.Registry64, apps);

        // HKLM 32-bit (Wow6432Node)
        ReadUninstallRegistry(RegistryHive.LocalMachine, RegistryView.Registry32, apps);

        // HKCU
        ReadUninstallRegistry(RegistryHive.CurrentUser, RegistryView.Default, apps);

        return apps.Values
            .OrderBy(a => a.DisplayName)
            .ToList();
    }

    /// <summary>
    /// Scans user and system AppData for residual folders of applications no longer present in Windows Uninstall registry.
    /// </summary>
    public IReadOnlyList<AppLeftoverFolder> DetectLeftoverFolders(IEnumerable<string>? customRoots = null)
    {
        var installed = GetInstalledApplications();
        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in installed)
        {
            if (!string.IsNullOrWhiteSpace(app.DisplayName))
            {
                knownNames.Add(app.DisplayName);
                foreach (var token in app.DisplayName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token.Length > 2)
                        knownNames.Add(token);
                }
            }
            if (!string.IsNullOrWhiteSpace(app.Publisher))
            {
                knownNames.Add(app.Publisher);
            }
        }

        var scanRoots = customRoots?.ToList() ?? new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        var leftovers = new List<AppLeftoverFolder>();

        foreach (var root in scanRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(root))
                {
                    var dirName = Path.GetFileName(subDir);
                    if (string.IsNullOrWhiteSpace(dirName)) continue;
                    if (SystemFolderExclusions.Contains(dirName)) continue;

                    // If dirName matches any installed software name or publisher, it is currently in use
                    bool isKnown = knownNames.Contains(dirName) || 
                                   knownNames.Any(k => k.Contains(dirName, StringComparison.OrdinalIgnoreCase));

                    if (!isKnown)
                    {
                        long size = 0;
                        int count = 0;
                        try
                        {
                            var di = new DirectoryInfo(subDir);
                            // Fast top-directory size estimation
                            foreach (var f in di.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                            {
                                try { size += f.Length; count++; } catch { }
                            }

                            foreach (var child in di.EnumerateDirectories().Take(5))
                            {
                                foreach (var f in child.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                                {
                                    try { size += f.Length; count++; } catch { }
                                }
                            }

                            if (count > 0)
                            {
                                leftovers.Add(new AppLeftoverFolder
                                {
                                    FolderPath = subDir,
                                    FolderName = dirName,
                                    EstimatedSizeBytes = size,
                                    FileCount = count,
                                    LastModified = di.LastWriteTime
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        return leftovers.OrderByDescending(l => l.EstimatedSizeBytes).ToList();
    }

    /// <summary>
    /// Revo-style post-uninstall or pre-clean targeted residual trace:
    /// Scans Registry and Disk locations matching an application's name, publisher, and install directory.
    /// </summary>
    public AppResidualAnalysis ScanAppResiduals(InstalledAppInfo app, IEnumerable<string>? customRoots = null)
    {
        var regKeys = new List<string>();
        var dirs = new List<AppLeftoverFolder>();

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(app.DisplayName))
        {
            tokens.Add(app.DisplayName.Trim());
            var parts = app.DisplayName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].Length > 2)
                tokens.Add(parts[0]);
        }

        // 1. Scan Registry for leftover keys
        foreach (var token in tokens)
        {
            CheckRegistryKeyExists(RegistryHive.CurrentUser, RegistryView.Default, $@"Software\{token}", regKeys);
            CheckRegistryKeyExists(RegistryHive.LocalMachine, RegistryView.Registry64, $@"Software\{token}", regKeys);
            CheckRegistryKeyExists(RegistryHive.LocalMachine, RegistryView.Registry32, $@"Software\{token}", regKeys);
        }

        // 2. Scan Disk Folders
        var searchRoots = customRoots?.ToList() ?? new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        foreach (var root in searchRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

            foreach (var token in tokens)
            {
                var targetDir = Path.Combine(root, token);
                if (Directory.Exists(targetDir))
                {
                    long size = 0;
                    int count = 0;
                    try
                    {
                        var di = new DirectoryInfo(targetDir);
                        foreach (var f in di.EnumerateFiles("*", SearchOption.AllDirectories).Take(500))
                        {
                            try { size += f.Length; count++; } catch { }
                        }

                        dirs.Add(new AppLeftoverFolder
                        {
                            FolderPath = targetDir,
                            FolderName = token,
                            EstimatedSizeBytes = size,
                            FileCount = count,
                            LastModified = di.LastWriteTime
                        });
                    }
                    catch { }
                }
            }
        }

        return new AppResidualAnalysis
        {
            App = app,
            RegistryKeysFound = regKeys.Distinct().ToList(),
            DirectoriesFound = dirs
        };
    }

    /// <summary>
    /// Builds uninstallation command line arguments based on installer type (MSI, Inno, NSIS).
    /// </summary>
    public (string ExePath, string Arguments) BuildUninstallCommand(InstalledAppInfo app, bool quiet = true)
    {
        if (quiet && !string.IsNullOrWhiteSpace(app.QuietUninstallString))
        {
            var parts = SplitExeAndArgs(app.QuietUninstallString);
            return (parts.Exe, parts.Args);
        }

        if (string.IsNullOrWhiteSpace(app.UninstallString))
        {
            return ("", "");
        }

        var cmd = app.UninstallString.Trim();
        var (exe, args) = SplitExeAndArgs(cmd);

        if (exe.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            // Ensure /x is present
            if (!args.Contains("/x", StringComparison.OrdinalIgnoreCase) && !args.Contains("-x", StringComparison.OrdinalIgnoreCase))
            {
                args = args.Replace("/I", "/X", StringComparison.OrdinalIgnoreCase);
                args = args.Replace("-I", "-X", StringComparison.OrdinalIgnoreCase);
            }
            if (quiet)
            {
                args += " /qn /norestart";
            }
            return (exe, args.Trim());
        }

        if (quiet)
        {
            if (exe.Contains("unins000", StringComparison.OrdinalIgnoreCase) || args.Contains("inno", StringComparison.OrdinalIgnoreCase))
            {
                args += " /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
            }
            else if (exe.Contains("setup", StringComparison.OrdinalIgnoreCase))
            {
                args += " /s /quiet";
            }
            else
            {
                args += " /S";
            }
        }

        return (exe, args.Trim());
    }

    /// <summary>
    /// Executes uninstallation safely with DryRun support.
    /// </summary>
    public async Task<UninstallExecutionResult> UninstallAppAsync(
        InstalledAppInfo app, 
        bool quiet = true, 
        bool dryRun = true)
    {
        var (exe, args) = BuildUninstallCommand(app, quiet);
        if (string.IsNullOrWhiteSpace(exe))
        {
            return new UninstallExecutionResult
            {
                Success = false,
                ExitCode = -1,
                CommandLine = "",
                WasDryRun = dryRun,
                Message = "No valid uninstallation command found for this application."
            };
        }

        string fullCommand = $"\"{exe}\" {args}".Trim();

        if (dryRun)
        {
            return new UninstallExecutionResult
            {
                Success = true,
                ExitCode = 0,
                CommandLine = fullCommand,
                WasDryRun = true,
                Message = $"[DRY-RUN] Simulated uninstallation for '{app.DisplayName}'. Command: {fullCommand}"
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return new UninstallExecutionResult
                {
                    Success = false,
                    ExitCode = -1,
                    CommandLine = fullCommand,
                    WasDryRun = false,
                    Message = "Failed to launch uninstaller process."
                };
            }

            // Await with timeout (5 minutes max)
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await proc.WaitForExitAsync(cts.Token);

            return new UninstallExecutionResult
            {
                Success = proc.ExitCode == 0,
                ExitCode = proc.ExitCode,
                CommandLine = fullCommand,
                WasDryRun = false,
                Message = proc.ExitCode == 0 
                    ? $"Successfully uninstalled '{app.DisplayName}'." 
                    : $"Uninstaller finished with exit code {proc.ExitCode}."
            };
        }
        catch (Exception ex)
        {
            return new UninstallExecutionResult
            {
                Success = false,
                ExitCode = -1,
                CommandLine = fullCommand,
                WasDryRun = false,
                Message = $"Uninstallation execution error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Removes a broken or invalid uninstall registry entry from Windows Add/Remove Programs.
    /// </summary>
    public bool RemoveBrokenUninstallEntry(InstalledAppInfo app, bool dryRun = true)
    {
        if (!app.IsBroken)
            return false;

        if (dryRun)
            return true;

        const string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(app.SourceHive, app.SourceView);
            using var uninstKey = baseKey.OpenSubKey(uninstallKeyPath, writable: true);
            if (uninstKey != null && uninstKey.GetSubKeyNames().Contains(app.Id, StringComparer.OrdinalIgnoreCase))
            {
                uninstKey.DeleteSubKeyTree(app.Id, throwOnMissingSubKey: false);
                return true;
            }
        }
        catch { }

        return false;
    }

    private static void CheckRegistryKeyExists(
        RegistryHive hive, 
        RegistryView view, 
        string subKeyPath, 
        List<string> foundKeys)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var subKey = baseKey.OpenSubKey(subKeyPath);
            if (subKey != null)
            {
                string hiveName = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";
                foundKeys.Add($@"{hiveName}\{subKeyPath}");
            }
        }
        catch { }
    }

    private static (string Exe, string Args) SplitExeAndArgs(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            int nextQuote = trimmed.IndexOf('"', 1);
            if (nextQuote > 1)
            {
                var exe = trimmed[1..nextQuote];
                var args = trimmed[(nextQuote + 1)..].Trim();
                return (exe, args);
            }
        }

        int firstSpace = trimmed.IndexOf(' ');
        if (firstSpace > 0)
        {
            return (trimmed[..firstSpace], trimmed[(firstSpace + 1)..].Trim());
        }

        return (trimmed, "");
    }

    private static void ReadUninstallRegistry(
        RegistryHive hive, 
        RegistryView view, 
        Dictionary<string, InstalledAppInfo> apps)
    {
        const string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstKey = baseKey.OpenSubKey(uninstallKeyPath);
            if (uninstKey == null) return;

            foreach (var subKeyName in uninstKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = uninstKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    // Exclude Windows System Updates / Hotfixes (KB numbers)
                    if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                        displayName.StartsWith("Update for", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Read size: Windows stores size in KB in "EstimatedSize"
                    long sizeBytes = 0;
                    var estSizeVal = subKey.GetValue("EstimatedSize");
                    if (estSizeVal is int sizeKb)
                        sizeBytes = (long)sizeKb * 1024;
                    else if (estSizeVal is long sizeKbLong)
                        sizeBytes = sizeKbLong * 1024;

                    var displayVersion = subKey.GetValue("DisplayVersion")?.ToString() ?? "";
                    var publisher = subKey.GetValue("Publisher")?.ToString() ?? "";
                    var installDate = subKey.GetValue("InstallDate")?.ToString() ?? "";
                    var installLocation = subKey.GetValue("InstallLocation")?.ToString();
                    var uninstallString = subKey.GetValue("UninstallString")?.ToString();
                    var quietUninstallString = subKey.GetValue("QuietUninstallString")?.ToString();

                    // Check if broken
                    bool isBroken = false;
                    if (!string.IsNullOrWhiteSpace(installLocation) && !Directory.Exists(installLocation))
                    {
                        isBroken = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(uninstallString))
                    {
                        var exePath = SplitExeAndArgs(uninstallString).Exe;
                        if (!string.IsNullOrWhiteSpace(exePath) && !File.Exists(exePath) && !exePath.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                        {
                            isBroken = true;
                        }
                    }

                    if (!apps.ContainsKey(displayName))
                    {
                        apps[displayName] = new InstalledAppInfo
                        {
                            Id = subKeyName,
                            DisplayName = displayName,
                            DisplayVersion = displayVersion,
                            Publisher = publisher,
                            InstallDate = installDate,
                            EstimatedSizeBytes = sizeBytes,
                            InstallLocation = installLocation,
                            UninstallString = uninstallString,
                            QuietUninstallString = quietUninstallString,
                            IsBroken = isBroken,
                            SourceHive = hive,
                            SourceView = view
                        };
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
