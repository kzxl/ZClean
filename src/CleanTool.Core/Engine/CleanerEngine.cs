using System.Diagnostics;
using CleanTool.Core.Contracts;
using CleanTool.Core.Models;

namespace CleanTool.Core.Engine;

/// <summary>
/// Orchestrates bulk scan and clean executions across registered rules.
/// </summary>
public class CleanerEngine
{
    private readonly IRuleRegistry _ruleRegistry;

    public CleanerEngine(IRuleRegistry ruleRegistry)
    {
        _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
    }

    /// <summary>
    /// Executes a scan across specified rules (or all rules if none specified).
    /// </summary>
    public async Task<IReadOnlyList<RuleScanResult>> ScanAsync(
        IEnumerable<string>? ruleIds = null,
        CleanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var activeOptions = options ?? new CleanOptions();
        var rulesToRun = GetTargetRules(ruleIds);
        var results = new List<RuleScanResult>();

        int totalFiles = 0;
        long totalBytes = 0;

        foreach (var rule in rulesToRun)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ruleProgress = new Progress<ScanProgress>(p =>
            {
                progress?.Report(new ScanProgress(
                    p.CurrentItem,
                    totalFiles + p.FilesScanned,
                    totalBytes + p.TotalBytesFound));
            });

            var result = await rule.ScanAsync(activeOptions, ruleProgress, cancellationToken);
            results.Add(result);

            totalFiles += result.TotalCount;
            totalBytes += result.TotalSizeBytes;
        }

        return results;
    }

    /// <summary>
    /// Executes clean operation across specified rules.
    /// </summary>
    public async Task<IReadOnlyList<RuleCleanResult>> CleanAsync(
        IEnumerable<string>? ruleIds = null,
        CleanOptions? options = null,
        IProgress<CleanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var activeOptions = options ?? new CleanOptions();
        var rulesToRun = GetTargetRules(ruleIds);
        var results = new List<RuleCleanResult>();

        int totalProcessed = 0;
        long totalReclaimed = 0;

        foreach (var rule in rulesToRun)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ruleProgress = new Progress<CleanProgress>(p =>
            {
                progress?.Report(new CleanProgress(
                    p.CurrentItem,
                    totalProcessed + p.FilesProcessed,
                    totalReclaimed + p.BytesReclaimed));
            });

            var result = await rule.CleanAsync(activeOptions, ruleProgress, cancellationToken);
            results.Add(result);

            totalProcessed += result.DeletedCount + result.SkippedCount;
            totalReclaimed += result.BytesFreed;
        }

        return results;
    }

    private List<ICleanerRule> GetTargetRules(IEnumerable<string>? ruleIds)
    {
        if (ruleIds == null)
            return _ruleRegistry.GetAllRules().ToList();

        var idSet = new HashSet<string>(ruleIds, StringComparer.OrdinalIgnoreCase);
        return _ruleRegistry.GetAllRules().Where(r => idSet.Contains(r.Id)).ToList();
    }
}
