namespace ZeroClean.Core.Models;

/// <summary>
/// Execution parameters for scan and clean routines.
/// </summary>
public record CleanOptions
{
    /// <summary>Simulate the operation without deleting any physical files.</summary>
    public bool DryRun { get; init; } = true;

    /// <summary>Minimum age of files before they qualify for cleanup.</summary>
    public TimeSpan? MinFileAge { get; init; } = TimeSpan.FromHours(24);

    /// <summary>When true, skip locked files quietly without recording as hard errors.</summary>
    public bool SkipLockedFiles { get; init; } = true;

    /// <summary>Explicit directory paths or files to protect from deletion.</summary>
    public IReadOnlyList<string> ExcludedPaths { get; init; } = Array.Empty<string>();

    /// <summary>File name or extension patterns to protect (e.g. *.lock, *.pid).</summary>
    public IReadOnlyList<string> ExcludedPatterns { get; init; } = Array.Empty<string>();
}
