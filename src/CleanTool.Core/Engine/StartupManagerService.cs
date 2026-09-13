using Microsoft.Win32;

namespace CleanTool.Core.Engine;

public enum StartupLocationType
{
    CurrentUserRegistry,
    LocalMachineRegistry,
    StartupFolder
}

public record StartupEntry
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string? ResolvedPath { get; init; }
    public required bool FileExists { get; init; }
    public required StartupLocationType LocationType { get; init; }
}

[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class StartupManagerService
{
    public IReadOnlyList<StartupEntry> GetStartupEntries()
    {
        var entries = new List<StartupEntry>();

        // 1. CurrentUser Run Key
        ReadRegistryRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupLocationType.CurrentUserRegistry, entries);

        // 2. LocalMachine Run Key
        ReadRegistryRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", StartupLocationType.LocalMachineRegistry, entries);

        // 3. User Startup Folder
        ReadStartupFolder(entries);

        return entries;
    }

    private static void ReadRegistryRunKey(
        RegistryKey root, 
        string subKeyPath, 
        StartupLocationType locType, 
        List<StartupEntry> result)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath, writable: false);
            if (key == null) return;

            foreach (var valueName in key.GetValueNames())
            {
                var cmd = key.GetValue(valueName)?.ToString();
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                var resolved = ExtractExecutablePath(cmd);
                bool exists = !string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved);

                result.Add(new StartupEntry
                {
                    Name = valueName,
                    Command = cmd,
                    ResolvedPath = resolved,
                    FileExists = exists,
                    LocationType = locType
                });
            }
        }
        catch { }
    }

    private static void ReadStartupFolder(List<StartupEntry> result)
    {
        try
        {
            var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (!Directory.Exists(startupDir)) return;

            foreach (var file in Directory.EnumerateFiles(startupDir))
            {
                result.Add(new StartupEntry
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    ResolvedPath = file,
                    FileExists = true,
                    LocationType = StartupLocationType.StartupFolder
                });
            }
        }
        catch { }
    }

    private static string? ExtractExecutablePath(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (trimmed.StartsWith('"'))
        {
            int nextQuote = trimmed.IndexOf('"', 1);
            if (nextQuote > 1)
                return trimmed[1..nextQuote];
        }

        int firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
    }
}
