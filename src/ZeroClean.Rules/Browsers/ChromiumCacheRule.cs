using ZeroClean.Core.Models;

namespace ZeroClean.Rules.Browsers;

/// <summary>
/// Cleans temporary network and script caches for Chromium-based browsers (Chrome, Edge, Brave).
/// Does NOT touch logins, cookies, or bookmarks.
/// </summary>
public class ChromiumCacheRule : BaseFolderRule
{
    public override string Id => "browser.chromium.cache";
    public override string Name => "Chromium Browsers Cache";
    public override string Description => "Cleans network cache and GPU cache for Chrome, Edge, and Brave. Preserves cookies and logins.";
    public override CleanCategory Category => CleanCategory.Browser;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => true;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var browserBases = new[]
        {
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")
        };

        var cacheSubfolders = new[]
        {
            Path.Combine("Default", "Cache", "Cache_Data"),
            Path.Combine("Default", "Code Cache"),
            Path.Combine("Default", "GPUCache"),
            Path.Combine("Default", "Service Worker", "CacheStorage")
        };

        foreach (var baseDir in browserBases)
        {
            if (!Directory.Exists(baseDir))
                continue;

            foreach (var sub in cacheSubfolders)
            {
                var full = Path.Combine(baseDir, sub);
                if (Directory.Exists(full))
                    yield return full;
            }
        }
    }
}
