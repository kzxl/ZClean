namespace ZClean.Core.Models;

/// <summary>
/// Risk assessment for cleanup rules.
/// </summary>
public enum CleanRiskLevel
{
    /// <summary>Pure temp or disposable cache files. Completely safe to remove.</summary>
    Safe,

    /// <summary>Build caches or packages that can be re-downloaded if needed.</summary>
    Moderate,

    /// <summary>System-level or historical backup files requiring administrative privileges.</summary>
    High
}
