using ZeroClean.Core.Models;

namespace ZeroClean.Core.Contracts;

/// <summary>
/// Core contract representing an autonomous cleaner plugin in Universe Architecture.
/// Each rule handles its own scanning, file enumeration, and target removal.
/// </summary>
public interface ICleanerRule
{
    /// <summary>Unique identifier for the rule (e.g., "sys.temp.user").</summary>
    string Id { get; }

    /// <summary>Human-readable display name.</summary>
    string Name { get; }

    /// <summary>Detailed description of the targets cleaned by this rule.</summary>
    string Description { get; }

    /// <summary>Clean target category classification.</summary>
    CleanCategory Category { get; }

    /// <summary>Safety/risk level of executing this rule.</summary>
    CleanRiskLevel RiskLevel { get; }

    /// <summary>Indicates if execution requires Windows Administrator privileges.</summary>
    bool RequiresElevation { get; }

    /// <summary>Whether this rule is selected by default in standard cleanup sweeps.</summary>
    bool IsDefaultEnabled { get; }

    /// <summary>
    /// Scans the target locations and reports re-claimable items without modifying disk.
    /// </summary>
    Task<RuleScanResult> ScanAsync(CleanOptions options, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs physical deletion or simulation of items based on options.
    /// </summary>
    Task<RuleCleanResult> CleanAsync(CleanOptions options, IProgress<CleanProgress>? progress = null, CancellationToken cancellationToken = default);
}
