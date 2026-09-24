namespace ZClean.Core.Engine;

public enum LargeFileCategory
{
    DiskImage,
    Archive,
    Installer,
    Media,
    Database,
    Log,
    Other
}

public record LargeFileInfo
{
    public required string FullPath { get; init; }
    public required string FileName { get; init; }
    public long SizeBytes { get; init; }
    public LargeFileCategory Category { get; init; }
    public DateTime LastModified { get; init; }
    public string Extension { get; init; } = "";
}

public class LargeFileScannerService
{
    private static readonly HashSet<string> DiskImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".iso", ".img", ".vhd", ".vhdx", ".vmdk", ".qcow2", ".bin"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".tar", ".gz", ".7z", ".rar", ".bz2", ".xz", ".tgz"
    };

    private static readonly HashSet<string> InstallerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".msi", ".exe", ".pkg", ".deb", ".appx"
    };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".flac", ".wav", ".psd", ".raw"
    };

    private static readonly HashSet<string> DatabaseExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mdf", ".ldf", ".bak", ".sqlite", ".sqlite3", ".db", ".dump"
    };

    private static readonly HashSet<string> LogExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".dmp", ".etl", ".trace"
    };

    public Task<IReadOnlyList<LargeFileInfo>> ScanLargeFilesAsync(
        string rootPath,
        long minSizeBytes = 100 * 1024 * 1024, // Default: 100MB
        int maxResults = 100,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<LargeFileInfo>>(() =>
        {
            if (!Directory.Exists(rootPath))
                return Array.Empty<LargeFileInfo>();

            var results = new List<LargeFileInfo>();
            int filesScanned = 0;

            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDir = queue.Dequeue();

                // Enumerate subdirectories safely
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(currentDir))
                    {
                        var dirName = Path.GetFileName(dir);
                        if (dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        queue.Enqueue(dir);
                    }
                }
                catch { }

                // Enumerate files safely
                try
                {
                    foreach (var file in Directory.EnumerateFiles(currentDir))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        filesScanned++;
                        if (filesScanned % 200 == 0)
                        {
                            progress?.Report(filesScanned);
                        }

                        try
                        {
                            var fi = new FileInfo(file);
                            if (fi.Length >= minSizeBytes)
                            {
                                results.Add(new LargeFileInfo
                                {
                                    FullPath = fi.FullName,
                                    FileName = fi.Name,
                                    SizeBytes = fi.Length,
                                    Category = ClassifyExtension(fi.Extension),
                                    LastModified = fi.LastWriteTime,
                                    Extension = fi.Extension.ToLowerInvariant()
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return results
                .OrderByDescending(f => f.SizeBytes)
                .Take(maxResults)
                .ToList();
        }, cancellationToken);
    }

    public static LargeFileCategory ClassifyExtension(string ext)
    {
        if (DiskImageExtensions.Contains(ext)) return LargeFileCategory.DiskImage;
        if (ArchiveExtensions.Contains(ext)) return LargeFileCategory.Archive;
        if (InstallerExtensions.Contains(ext)) return LargeFileCategory.Installer;
        if (MediaExtensions.Contains(ext)) return LargeFileCategory.Media;
        if (DatabaseExtensions.Contains(ext)) return LargeFileCategory.Database;
        if (LogExtensions.Contains(ext)) return LargeFileCategory.Log;
        return LargeFileCategory.Other;
    }
}
