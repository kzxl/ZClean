namespace ZeroClean.Core.Models;

/// <summary>
/// Drive storage capacity and utilization metrics.
/// </summary>
public record DiskSpaceInfo
{
    public required string DriveName { get; init; }
    public required string VolumeLabel { get; init; }
    public required string DriveFormat { get; init; }
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public long UsedBytes => TotalBytes - FreeBytes;
    public double UsedPercentage => TotalBytes > 0 ? ((double)UsedBytes / TotalBytes) * 100.0 : 0.0;
    public double FreePercentage => TotalBytes > 0 ? ((double)FreeBytes / TotalBytes) * 100.0 : 0.0;
}

/// <summary>
/// Hierarchical node representing a directory tree branch for treemaps and disk breakdown.
/// </summary>
public record DirectoryAnalysisNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public long TotalSizeBytes { get; set; }
    public int FileCount { get; set; }
    public int SubdirectoryCount { get; set; }
    public List<DirectoryAnalysisNode> Children { get; init; } = new();
}
