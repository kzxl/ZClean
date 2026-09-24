namespace ZClean.Core.Models;

/// <summary>
/// Detailed record of a single scanned file or directory item.
/// </summary>
public record ScanItemResult
{
    public required string FilePath { get; init; }
    public long SizeBytes { get; init; }
    public DateTime LastModified { get; init; }
    public bool IsLocked { get; init; }
    public string? SkipReason { get; init; }
}

/// <summary>
/// Aggregated scan outcome from an individual cleaner rule.
/// </summary>
public record RuleScanResult
{
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public CleanCategory Category { get; init; }
    public CleanRiskLevel RiskLevel { get; init; }
    public IReadOnlyList<ScanItemResult> Items { get; init; } = Array.Empty<ScanItemResult>();
    public long TotalSizeBytes { get; init; }
    public int TotalCount => Items.Count;
    public TimeSpan ScanDuration { get; init; }
}

/// <summary>
/// Execution metrics following a cleanup attempt on a rule.
/// </summary>
public record RuleCleanResult
{
    public required string RuleId { get; init; }
    public required string RuleName { get; init; }
    public int DeletedCount { get; init; }
    public long BytesFreed { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public TimeSpan CleanDuration { get; init; }
}

/// <summary>
/// Progress reporting packet for scanning phases.
/// </summary>
public record ScanProgress(string CurrentItem, int FilesScanned, long TotalBytesFound);

/// <summary>
/// Progress reporting packet for cleaning phases.
/// </summary>
public record CleanProgress(string CurrentItem, int FilesProcessed, long BytesReclaimed);
