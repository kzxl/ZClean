using ZClean.Core.Models;

namespace ZClean.Rules.Developer;

/// <summary>
/// Cleans cached Python wheels and source distributions in %LOCALAPPDATA%\pip\cache.
/// </summary>
public class PipCacheRule : BaseFolderRule
{
    public override string Id => "dev.pip.cache";
    public override string Name => "Python pip Cache";
    public override string Description => "Cleans cached wheels and packages saved by pip (%LOCALAPPDATA%\\pip\\cache).";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Moderate;
    public override bool IsDefaultEnabled => false;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pipCache = Path.Combine(localAppData, "pip", "cache");
        if (Directory.Exists(pipCache))
            yield return pipCache;
    }
}
