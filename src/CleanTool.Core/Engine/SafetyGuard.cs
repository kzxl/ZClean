using CleanTool.Core.Contracts;
using CleanTool.Core.Models;

namespace CleanTool.Core.Engine;

/// <summary>
/// Defensive barrier safeguarding vital system and user assets.
/// </summary>
public class SafetyGuard : ISafetyGuard
{
    private static readonly HashSet<string> ProtectedSystemFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagefile.sys",
        "hiberfil.sys",
        "swapfile.sys",
        "bootmgr",
        "BOOTNXT",
        "ntuser.dat",
        "desktop.ini",
        ".env",
        ".env.local",
        ".env.development",
        ".env.production",
        ".env.test",
        "id_rsa",
        "id_rsa.pub",
        "id_ed25519",
        "id_ed25519.pub",
        "known_hosts",
        "authorized_keys"
    };

    private static readonly string[] ProtectedExtensions =
    {
        ".key",
        ".pem",
        ".pfx",
        ".p12"
    };

    private static readonly string[] ProtectedDirectories =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "WinSxS"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "assembly")
    };

    public bool IsProtectedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var fullPath = Path.GetFullPath(path);

        // Disallow drive root paths directly (e.g. C:\, D:\)
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), 
                          root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), 
                          StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = Path.GetFileName(fullPath);
        if (ProtectedSystemFiles.Contains(fileName))
            return true;

        var ext = Path.GetExtension(fullPath);
        if (!string.IsNullOrEmpty(ext) && ProtectedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return true;

        // Disallow touching inside .git database directory
        var sep = Path.DirectorySeparatorChar;
        var altSep = Path.AltDirectorySeparatorChar;
        if (fullPath.Contains($"{sep}.git{sep}") || fullPath.Contains($"{altSep}.git{altSep}") ||
            fullPath.EndsWith($"{sep}.git") || fullPath.EndsWith($"{altSep}.git"))
        {
            return true;
        }

        foreach (var protectedDir in ProtectedDirectories)
        {
            if (string.IsNullOrWhiteSpace(protectedDir))
                continue;

            if (fullPath.StartsWith(protectedDir, StringComparison.OrdinalIgnoreCase))
            {
                // Specifically allow C:\Windows\Temp
                var winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                if (fullPath.StartsWith(winTemp, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Specifically allow C:\Windows\Prefetch
                var winPrefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (fullPath.StartsWith(winPrefetch, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Specifically allow C:\Windows\SoftwareDistribution\Download
                var winDownload = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                if (fullPath.StartsWith(winDownload, StringComparison.OrdinalIgnoreCase))
                    continue;

                return true;
            }
        }

        return false;
    }

    public bool IsEligibleForDeletion(string filePath, CleanOptions options)
    {
        if (IsProtectedPath(filePath))
            return false;

        var normalizedPath = Path.GetFullPath(filePath);

        // Check user explicit path exclusions
        foreach (var excludedPath in options.ExcludedPaths)
        {
            if (!string.IsNullOrWhiteSpace(excludedPath) &&
                normalizedPath.StartsWith(Path.GetFullPath(excludedPath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check pattern exclusions (*.lock, *.pid)
        var fileName = Path.GetFileName(normalizedPath);
        foreach (var pattern in options.ExcludedPatterns)
        {
            if (MatchesWildcard(fileName, pattern))
                return false;
        }

        // Check file age threshold
        if (options.MinFileAge.HasValue && File.Exists(normalizedPath))
        {
            try
            {
                var lastWrite = File.GetLastWriteTime(normalizedPath);
                var created = File.GetCreationTime(normalizedPath);
                var newest = lastWrite > created ? lastWrite : created;

                if (DateTime.Now - newest < options.MinFileAge.Value)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    public bool IsSafeTargetDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return false;

        var fullPath = Path.GetFullPath(directoryPath);
        var root = Path.GetPathRoot(fullPath);

        if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                          root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                          StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsProtectedPath(fullPath);
    }

    private static bool MatchesWildcard(string text, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.StartsWith("*.") && Path.GetExtension(text).Equals(pattern[1..], StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
