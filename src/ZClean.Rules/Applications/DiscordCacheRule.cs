using ZClean.Core.Models;

namespace ZClean.Rules.Applications;

/// <summary>
/// Cleans Discord Electron caches (Cache, Code Cache, GPUCache).
/// </summary>
public class DiscordCacheRule : BaseFolderRule
{
    public override string Id => "app.discord.cache";
    public override string Name => "Discord Cache";
    public override string Description => "Cleans cached media, image assets, and Chromium GPU cache in Discord.";
    public override CleanCategory Category => CleanCategory.Application;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => true;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "discord");

        if (Directory.Exists(baseDir))
        {
            var targets = new[] { "Cache", "Code Cache", "GPUCache" };
            foreach (var t in targets)
            {
                var full = Path.Combine(baseDir, t);
                if (Directory.Exists(full))
                    yield return full;
            }
        }
    }
}
