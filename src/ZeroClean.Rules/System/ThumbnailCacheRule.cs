using ZeroClean.Core.Models;

namespace ZeroClean.Rules.System;

/// <summary>
/// Cleans Windows Explorer thumbnail database caches.
/// </summary>
public class ThumbnailCacheRule : BaseFolderRule
{
    public override string Id => "sys.thumbnails";
    public override string Name => "Thumbnail Cache";
    public override string Description => "Cleans Windows File Explorer thumbnail database files (thumbcache_*.db).";
    public override CleanCategory Category => CleanCategory.System;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => false; // Disabled by default to avoid slow file icon loading

    protected override string SearchPattern => "thumbcache_*.db";
    protected override bool Recursive => false;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var explorerDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
        if (Directory.Exists(explorerDir))
            yield return explorerDir;
    }
}
