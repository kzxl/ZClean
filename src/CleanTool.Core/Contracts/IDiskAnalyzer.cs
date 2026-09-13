using CleanTool.Core.Models;

namespace CleanTool.Core.Contracts;

/// <summary>
/// Service inspecting disk partitions and recursive directory footprints.
/// </summary>
public interface IDiskAnalyzer
{
    IReadOnlyList<DiskSpaceInfo> GetDrives();
    Task<DirectoryAnalysisNode> AnalyzeDirectoryAsync(string directoryPath, int maxDepth = 3, CancellationToken cancellationToken = default);
}
