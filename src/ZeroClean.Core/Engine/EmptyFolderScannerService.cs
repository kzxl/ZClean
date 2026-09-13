namespace ZeroClean.Core.Engine;

public record EmptyFolderInfo
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public DateTime LastModified { get; init; }
}

public class EmptyFolderScannerService
{
    private static readonly HashSet<string> ProtectedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "System32", "SysWOW64", "Program Files", "Program Files (x86)",
        ".git", ".vs", "AppData", "Local", "Roaming", "System Volume Information"
    };

    public Task<IReadOnlyList<EmptyFolderInfo>> FindEmptyFoldersAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<EmptyFolderInfo>>(() =>
        {
            if (!Directory.Exists(rootPath))
                return Array.Empty<EmptyFolderInfo>();

            var emptyFolders = new List<EmptyFolderInfo>();
            ScanDirectoryRecursive(rootPath, emptyFolders, cancellationToken);
            return (IReadOnlyList<EmptyFolderInfo>)emptyFolders;
        }, cancellationToken);
    }

    private bool ScanDirectoryRecursive(
        string dirPath, 
        List<EmptyFolderInfo> emptyFolders,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dirName = Path.GetFileName(dirPath);
        if (ProtectedDirectoryNames.Contains(dirName))
            return false;

        bool hasFiles = false;
        try
        {
            hasFiles = Directory.EnumerateFiles(dirPath).Any();
        }
        catch
        {
            return false; // Inaccessible
        }

        bool hasNonEmptySubdirs = false;
        try
        {
            foreach (var subDir in Directory.EnumerateDirectories(dirPath))
            {
                bool subDirIsEmpty = ScanDirectoryRecursive(subDir, emptyFolders, cancellationToken);
                if (!subDirIsEmpty)
                {
                    hasNonEmptySubdirs = true;
                }
            }
        }
        catch
        {
            return false;
        }

        bool isEmpty = !hasFiles && !hasNonEmptySubdirs;

        // Never mark the rootPath itself as an empty candidate to be removed
        if (isEmpty)
        {
            try
            {
                var di = new DirectoryInfo(dirPath);
                emptyFolders.Add(new EmptyFolderInfo
                {
                    Path = di.FullName,
                    Name = di.Name,
                    LastModified = di.LastWriteTime
                });
            }
            catch { }
        }

        return isEmpty;
    }
}
