using CleanTool.Core.Contracts;
using CleanTool.Core.Models;

namespace CleanTool.Core.Engine;

public enum RecommendationPriority
{
    High,
    Medium,
    Low
}

public enum HealthGrade
{
    A, // 90-100: Optimal
    B, // 75-89: Good
    C, // 50-74: Degraded
    D  // 0-49: Action Required
}

public record SystemHealthScore
{
    public int Score { get; init; } = 100;
    public HealthGrade Grade { get; init; } = HealthGrade.A;
    public string Summary { get; init; } = "System in optimal condition.";
    public IReadOnlyList<string> IssuesDetected { get; init; } = Array.Empty<string>();
}

public record CleanRecommendation
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public long EstimatedSavingsBytes { get; init; }
    public RecommendationPriority Priority { get; init; }
    public required string ActionCategory { get; init; }
    public required string SuggestedCommand { get; init; }
}

public record CleanAdvisorReport
{
    public required SystemHealthScore HealthScore { get; init; }
    public required IReadOnlyList<CleanRecommendation> Recommendations { get; init; }
    public long TotalPotentialSavingsBytes => Recommendations.Sum(r => r.EstimatedSavingsBytes);
    public int HighPriorityCount => Recommendations.Count(r => r.Priority == RecommendationPriority.High);
    public int MediumPriorityCount => Recommendations.Count(r => r.Priority == RecommendationPriority.Medium);
    public int LowPriorityCount => Recommendations.Count(r => r.Priority == RecommendationPriority.Low);
}

[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class CleanAdvisorService
{
    private readonly CleanerEngine _engine;
    private readonly IRuleRegistry _ruleRegistry;
    private readonly DevWorkspaceService _devService;
    private readonly StartupManagerService _startupService;
    private readonly AppUninstallerService _uninstallerService;

    public CleanAdvisorService(
        CleanerEngine engine,
        IRuleRegistry ruleRegistry,
        DevWorkspaceService? devService = null,
        StartupManagerService? startupService = null,
        AppUninstallerService? uninstallerService = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
        _devService = devService ?? new DevWorkspaceService();
        _startupService = startupService ?? new StartupManagerService();
        _uninstallerService = uninstallerService ?? new AppUninstallerService();
    }

    /// <summary>
    /// Executes holistic heuristic analysis across system, dev workspaces, startup, and installed apps.
    /// Computes system health score and prioritized actionable advice.
    /// </summary>
    public async Task<CleanAdvisorReport> GenerateRecommendationsAsync(
        string? devWorkspaceRoot = null, 
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<CleanRecommendation>();
        var issues = new List<string>();
        int healthScore = 100;

        // 1. Scan default safe system & app rules
        long totalDisposableCacheBytes = 0;
        var defaultRules = _ruleRegistry.GetAllRules().Where(r => r.IsDefaultEnabled).Select(r => r.Id);
        var scanResults = await _engine.ScanAsync(defaultRules, options: new CleanOptions { DryRun = true }, cancellationToken: cancellationToken);
        
        foreach (var sr in scanResults.Where(r => r.TotalSizeBytes > 50 * 1024 * 1024)) // > 50 MB
        {
            totalDisposableCacheBytes += sr.TotalSizeBytes;
            var priority = sr.TotalSizeBytes > 500 * 1024 * 1024 
                ? RecommendationPriority.High 
                : RecommendationPriority.Medium;

            recommendations.Add(new CleanRecommendation
            {
                Id = $"rule.{sr.RuleId}",
                Title = $"Clean {sr.RuleName} ({sr.TotalCount} items)",
                Description = $"Accumulated {FormatBytes(sr.TotalSizeBytes)} of disposable cache files.",
                EstimatedSavingsBytes = sr.TotalSizeBytes,
                Priority = priority,
                ActionCategory = sr.Category.ToString(),
                SuggestedCommand = $"cleantool clean --rule {sr.RuleId}"
            });
        }

        if (totalDisposableCacheBytes > 1024L * 1024 * 1024)
        {
            healthScore -= 15;
            issues.Add($"Heavy cache build-up: {FormatBytes(totalDisposableCacheBytes)} in system temporary directories");
        }
        else if (totalDisposableCacheBytes > 500 * 1024 * 1024)
        {
            healthScore -= 10;
            issues.Add($"Moderate cache build-up: {FormatBytes(totalDisposableCacheBytes)} in temporary files");
        }
        else if (totalDisposableCacheBytes > 100 * 1024 * 1024)
        {
            healthScore -= 5;
        }

        // 2. Scan Dev Workspace for Dormant Repos
        if (!string.IsNullOrWhiteSpace(devWorkspaceRoot) && Directory.Exists(devWorkspaceRoot))
        {
            try
            {
                var repos = await _devService.ScanWorkspacesAsync(devWorkspaceRoot, dormantDaysThreshold: 30, cancellationToken: cancellationToken);
                var dormantRepos = repos.Where(r => r.State == DevRepoState.Dormant && r.TotalReclaimableBytes > 100 * 1024 * 1024).ToList();

                if (dormantRepos.Count > 0)
                {
                    long totalDormantBytes = dormantRepos.Sum(r => r.TotalReclaimableBytes);
                    healthScore -= 10;
                    issues.Add($"{dormantRepos.Count} dormant development repositories consume {FormatBytes(totalDormantBytes)}");

                    recommendations.Add(new CleanRecommendation
                    {
                        Id = "dev.dormant.repos",
                        Title = $"Purge {dormantRepos.Count} Dormant Projects ({FormatBytes(totalDormantBytes)})",
                        Description = $"Repositories inactive for > 30 days ({string.Join(", ", dormantRepos.Take(3).Select(r => r.RepoName))}...) contain stale node_modules and bin/obj builds.",
                        EstimatedSavingsBytes = totalDormantBytes,
                        Priority = RecommendationPriority.High,
                        ActionCategory = "Developer",
                        SuggestedCommand = $"cleantool dev \"{devWorkspaceRoot}\" --dormant-days 30"
                    });
                }
            }
            catch { }
        }

        // 3. Inspect Dead Startup Applications
        try
        {
            var startupEntries = _startupService.GetStartupEntries();
            var deadEntries = startupEntries.Where(e => !e.FileExists).ToList();
            if (deadEntries.Count > 0)
            {
                int deduction = Math.Min(deadEntries.Count * 5, 20);
                healthScore -= deduction;
                issues.Add($"{deadEntries.Count} broken/dead startup entries slowing down system boot");

                recommendations.Add(new CleanRecommendation
                {
                    Id = "sys.startup.dead",
                    Title = $"Remove {deadEntries.Count} Dead Startup Entries",
                    Description = $"Applications uninstalled or moved ({string.Join(", ", deadEntries.Take(3).Select(e => e.Name))}) are still being queried at Windows boot.",
                    EstimatedSavingsBytes = 0,
                    Priority = RecommendationPriority.High,
                    ActionCategory = "Startup",
                    SuggestedCommand = "cleantool startup"
                });
            }
        }
        catch { }

        // 4. Inspect Broken Installed Applications & Leftover Folders
        try
        {
            var installedApps = _uninstallerService.GetInstalledApplications();
            var brokenApps = installedApps.Where(a => a.IsBroken).ToList();
            if (brokenApps.Count > 0)
            {
                int deduction = Math.Min(brokenApps.Count * 5, 15);
                healthScore -= deduction;
                issues.Add($"{brokenApps.Count} broken uninstall registry entries detected");

                recommendations.Add(new CleanRecommendation
                {
                    Id = "app.broken.uninstall",
                    Title = $"Clean {brokenApps.Count} Broken Uninstaller Entries",
                    Description = $"Registry entries ({string.Join(", ", brokenApps.Take(3).Select(a => a.DisplayName))}) point to missing files.",
                    EstimatedSavingsBytes = 0,
                    Priority = RecommendationPriority.Medium,
                    ActionCategory = "Uninstaller",
                    SuggestedCommand = "cleantool apps --broken"
                });
            }

            var leftovers = _uninstallerService.DetectLeftoverFolders();
            var largeLeftovers = leftovers.Where(l => l.EstimatedSizeBytes > 20 * 1024 * 1024).Take(5).ToList();
            if (largeLeftovers.Count > 0)
            {
                long totalLeftoverBytes = largeLeftovers.Sum(l => l.EstimatedSizeBytes);
                int deduction = Math.Min(largeLeftovers.Count * 5, 20);
                healthScore -= deduction;
                issues.Add($"{largeLeftovers.Count} orphan AppData residual folders ({FormatBytes(totalLeftoverBytes)})");

                recommendations.Add(new CleanRecommendation
                {
                    Id = "app.leftover.folders",
                    Title = $"Review {largeLeftovers.Count} Residual AppData Folders ({FormatBytes(totalLeftoverBytes)})",
                    Description = $"Directories in AppData ({string.Join(", ", largeLeftovers.Take(3).Select(l => l.FolderName))}) belong to uninstalled software.",
                    EstimatedSavingsBytes = totalLeftoverBytes,
                    Priority = RecommendationPriority.Medium,
                    ActionCategory = "Leftovers",
                    SuggestedCommand = "cleantool leftovers"
                });
            }
        }
        catch { }

        // Normalize health score
        healthScore = Math.Clamp(healthScore, 0, 100);
        HealthGrade grade = healthScore switch
        {
            >= 90 => HealthGrade.A,
            >= 75 => HealthGrade.B,
            >= 50 => HealthGrade.C,
            _ => HealthGrade.D
        };

        string summary = grade switch
        {
            HealthGrade.A => "System is in optimal condition with minimal disk or registry waste.",
            HealthGrade.B => "System is in good health, but some safe optimizations are recommended.",
            HealthGrade.C => "Noticeable accumulation of cache, leftovers, or startup clutter detected.",
            _ => "Action required: substantial junk or dead entries are impacting performance and storage."
        };

        // Order recommendations by priority descending, then by size descending
        recommendations.Sort((a, b) =>
        {
            int pComp = a.Priority.CompareTo(b.Priority);
            return pComp != 0 ? pComp : b.EstimatedSavingsBytes.CompareTo(a.EstimatedSavingsBytes);
        });

        return new CleanAdvisorReport
        {
            HealthScore = new SystemHealthScore
            {
                Score = healthScore,
                Grade = grade,
                Summary = summary,
                IssuesDetected = issues
            },
            Recommendations = recommendations
        };
    }

    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
