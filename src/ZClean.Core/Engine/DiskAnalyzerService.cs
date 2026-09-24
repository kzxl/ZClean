using ZClean.Core.Contracts;
using ZClean.Core.Models;

namespace ZClean.Core.Engine;

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
            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            // Enumerate files
            foreach (var file in dir.EnumerateFiles("*", enumOptions))
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
            var subDirs = dir.EnumerateDirectories("*", enumOptions);
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
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var file in dir.EnumerateFiles("*", enumOptions))
            {
                ct.ThrowIfCancellationRequested();
                try { size += file.Length; } catch { }
            }
        }
        catch { }
        return size;
    }

    private static readonly string[] TreemapPalette = new[]
    {
        "#3B82F6", // Blue
        "#8B5CF6", // Purple
        "#10B981", // Emerald
        "#F59E0B", // Amber
        "#EF4444", // Rose
        "#06B6D4", // Cyan
        "#6366F1", // Indigo
        "#EC4899", // Pink
        "#14B8A6", // Teal
        "#84CC16"  // Lime
    };

    public IReadOnlyList<SquarifiedTreeMapBlock> GenerateSquarifiedTreeMap(
        DirectoryAnalysisNode root, 
        double width, 
        double height, 
        int maxItems = 40)
    {
        var blocks = new List<SquarifiedTreeMapBlock>();
        if (root == null || width <= 0 || height <= 0)
            return blocks;

        var rawItems = new List<(string Name, string FullPath, long Size)>();
        if (root.Children.Count > 0)
        {
            foreach (var child in root.Children.Where(c => c.TotalSizeBytes > 0))
            {
                rawItems.Add((child.Name, child.FullPath, child.TotalSizeBytes));
            }
        }
        else if (root.TotalSizeBytes > 0)
        {
            rawItems.Add((root.Name, root.FullPath, root.TotalSizeBytes));
        }

        if (rawItems.Count == 0)
            return blocks;

        rawItems.Sort((a, b) => b.Size.CompareTo(a.Size));

        long totalSize = rawItems.Sum(r => r.Size);
        if (totalSize <= 0) return blocks;

        var items = new List<(string Name, string FullPath, long Size)>();
        if (rawItems.Count > maxItems)
        {
            items.AddRange(rawItems.Take(maxItems - 1));
            var remainderSize = rawItems.Skip(maxItems - 1).Sum(r => r.Size);
            if (remainderSize > 0)
            {
                items.Add(("[Other Files & Folders]", root.FullPath, remainderSize));
            }
        }
        else
        {
            items.AddRange(rawItems);
        }

        double totalArea = width * height;
        var areas = items.Select(item => (Item: item, Area: Math.Max(0.1, (item.Size / (double)totalSize) * totalArea))).ToList();

        double curX = 0;
        double curY = 0;
        double curW = width;
        double curH = height;

        int colorIdx = 0;
        var currentQueue = new Queue<((string Name, string FullPath, long Size) Item, double Area)>(areas);

        while (currentQueue.Count > 0 && curW > 0.5 && curH > 0.5)
        {
            var row = new List<((string Name, string FullPath, long Size) Item, double Area)>();
            double shortSide = Math.Min(curW, curH);

            while (currentQueue.Count > 0)
            {
                var next = currentQueue.Peek();
                var testRowAreas = row.Select(r => r.Area).Append(next.Area).ToList();

                if (row.Count == 0 || WorstAspect(testRowAreas, shortSide) <= WorstAspect(row.Select(r => r.Area).ToList(), shortSide))
                {
                    row.Add(currentQueue.Dequeue());
                }
                else
                {
                    break;
                }
            }

            double rowArea = row.Sum(r => r.Area);
            double rowThickness = rowArea / shortSide;

            if (curW <= curH)
            {
                double itemX = curX;
                foreach (var r in row)
                {
                    double itemW = (r.Area / rowArea) * curW;
                    blocks.Add(new SquarifiedTreeMapBlock
                    {
                        Name = r.Item.Name,
                        FullPath = r.Item.FullPath,
                        SizeBytes = r.Item.Size,
                        X = Math.Round(itemX, 1),
                        Y = Math.Round(curY, 1),
                        Width = Math.Max(1.0, Math.Round(itemW, 1)),
                        Height = Math.Max(1.0, Math.Round(rowThickness, 1)),
                        PercentOfTotal = (r.Item.Size / (double)totalSize) * 100.0,
                        ColorHex = TreemapPalette[colorIdx++ % TreemapPalette.Length],
                        FormattedSize = FormatBytes(r.Item.Size)
                    });
                    itemX += itemW;
                }
                curY += rowThickness;
                curH -= rowThickness;
            }
            else
            {
                double itemY = curY;
                foreach (var r in row)
                {
                    double itemH = (r.Area / rowArea) * curH;
                    blocks.Add(new SquarifiedTreeMapBlock
                    {
                        Name = r.Item.Name,
                        FullPath = r.Item.FullPath,
                        SizeBytes = r.Item.Size,
                        X = Math.Round(curX, 1),
                        Y = Math.Round(itemY, 1),
                        Width = Math.Max(1.0, Math.Round(rowThickness, 1)),
                        Height = Math.Max(1.0, Math.Round(itemH, 1)),
                        PercentOfTotal = (r.Item.Size / (double)totalSize) * 100.0,
                        ColorHex = TreemapPalette[colorIdx++ % TreemapPalette.Length],
                        FormattedSize = FormatBytes(r.Item.Size)
                    });
                    itemY += itemH;
                }
                curX += rowThickness;
                curW -= rowThickness;
            }
        }

        return blocks;
    }

    private static double WorstAspect(IReadOnlyList<double> rowAreas, double sideLength)
    {
        if (rowAreas.Count == 0 || sideLength <= 0) return double.MaxValue;
        double s = rowAreas.Sum();
        if (s <= 0) return double.MaxValue;

        double min = rowAreas.Min();
        double max = rowAreas.Max();
        if (min <= 0) return double.MaxValue;

        double sideSq = sideLength * sideLength;
        double sSq = s * s;

        return Math.Max((sideSq * max) / sSq, sSq / (sideSq * min));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

