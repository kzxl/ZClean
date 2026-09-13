using CleanTool.Core.Contracts;
using CleanTool.Core.Models;

namespace CleanTool.Core.Engine;

/// <summary>
/// Fast inspection engine calculating disk volume usage and directory hierarchies.
/// </summary>
public class DiskAnalyzerService : IDiskAnalyzer
{
    public IReadOnlyList<DiskSpaceInfo> GetDrives()
    {
        var result = new List<DiskSpaceInfo>();
        var drives = DriveInfo.GetDrives();

        foreach (var drive in drives)
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                result.Add(new DiskSpaceInfo
                {
                    DriveName = drive.Name,
                    VolumeLabel = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                    DriveFormat = drive.DriveFormat,
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.TotalFreeSpace
                });
            }
            catch
            {
                // Unmounted or restricted drives skipped
            }
        }

        return result;
    }

    public async Task<DirectoryAnalysisNode> AnalyzeDirectoryAsync(
        string directoryPath, 
        int maxDepth = 3, 
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var rootDir = new DirectoryInfo(directoryPath);
            var rootNode = new DirectoryAnalysisNode
            {
                Name = rootDir.Name,
                FullPath = rootDir.FullName
            };

            TraverseDirectory(rootDir, rootNode, 0, maxDepth, cancellationToken);
            return rootNode;
        }, cancellationToken);
    }

    private void TraverseDirectory(
        DirectoryInfo dir, 
        DirectoryAnalysisNode node, 
        int currentDepth, 
        int maxDepth, 
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        long totalSize = 0;
        int fileCount = 0;

        try
        {
            // Enumerate files
            foreach (var file in dir.EnumerateFiles())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    totalSize += file.Length;
                    fileCount++;
                }
                catch
                {
                    // Ignore inaccessible files
                }
            }

            // Enumerate subdirectories
            var subDirs = dir.EnumerateDirectories();
            int subDirCount = 0;

            foreach (var subDir in subDirs)
            {
                ct.ThrowIfCancellationRequested();
                subDirCount++;

                if (currentDepth < maxDepth)
                {
                    var childNode = new DirectoryAnalysisNode
                    {
                        Name = subDir.Name,
                        FullPath = subDir.FullName
                    };

                    TraverseDirectory(subDir, childNode, currentDepth + 1, maxDepth, ct);
                    totalSize += childNode.TotalSizeBytes;
                    fileCount += childNode.FileCount;
                    node.Children.Add(childNode);
                }
                else
                {
                    // Beyond max depth, just sum up subfolder size without adding child nodes
                    totalSize += CalculateQuickSize(subDir, ct);
                }
            }

            node.TotalSizeBytes = totalSize;
            node.FileCount = fileCount;
            node.SubdirectoryCount = subDirCount;

            // Sort children by size descending
            node.Children.Sort((a, b) => b.TotalSizeBytes.CompareTo(a.TotalSizeBytes));
        }
        catch (UnauthorizedAccessException)
        {
            // Restricted system folders skipped gracefully
        }
        catch (Exception)
        {
            // Broken symlinks or missing paths
        }
    }

    private static long CalculateQuickSize(DirectoryInfo dir, CancellationToken ct)
    {
        long size = 0;
        try
        {
            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { size += file.Length; } catch { }
            }
        }
        catch { }
        return size;
    }
}
