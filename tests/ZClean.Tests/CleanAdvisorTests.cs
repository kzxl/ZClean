using ZClean.Core.Engine;
using ZClean.Rules;
using Xunit;

namespace ZClean.Tests;

public class CleanAdvisorTests
{
    [Fact]
    public async Task CleanAdvisorService_GeneratesPrioritizedRecommendations()
    {
        var registry = new RuleRegistry();
        // Register single lightweight rule for fast test
        registry.Register(new ZClean.Rules.Developer.GoBuildCacheRule());
        var engine = new CleanerEngine(registry);

        var advisor = new CleanAdvisorService(engine, registry);
        var report = await advisor.GenerateRecommendationsAsync();

        Assert.NotNull(report);
        Assert.NotNull(report.Recommendations);

        for (int i = 0; i < report.Recommendations.Count - 1; i++)
        {
            Assert.True(report.Recommendations[i].Priority <= report.Recommendations[i + 1].Priority);
        }
    }

    [Fact]
    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void AppUninstallerService_EnumeratesApplicationsWithoutCrashing()
    {
        var service = new AppUninstallerService();
        var apps = service.GetInstalledApplications();

        Assert.NotNull(apps);
        Assert.NotEmpty(apps);
    }

    [Fact]
    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task AppUninstallerService_DetectsOrphanLeftovers_InSandbox()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZClean_LeftoverTest_" + Guid.NewGuid().ToString("N"));
        var leftoverFolder = Path.Combine(sandbox, "ObsoleteUninstalledProgram12345");
        Directory.CreateDirectory(leftoverFolder);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(leftoverFolder, "config.old"), "stale data");

            var service = new AppUninstallerService();
            var leftovers = service.DetectLeftoverFolders(new[] { sandbox });

            Assert.Single(leftovers);
            Assert.Equal("ObsoleteUninstalledProgram12345", leftovers[0].FolderName);
            Assert.True(leftovers[0].EstimatedSizeBytes > 0);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }
}
