using ZeroClean.Core.Models;

namespace ZeroClean.Rules.Browsers;

/// <summary>
/// Cleans cached pages and media in Mozilla Firefox profiles.
/// </summary>
public class FirefoxCacheRule : BaseFolderRule
{
    public override string Id => "browser.firefox.cache";
    public override string Name => "Firefox Cache";
    public override string Description => "Cleans cached temporary media and script files in Firefox profiles (cache2).";
    public override CleanCategory Category => CleanCategory.Browser;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => true;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profilesBase = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");

        if (Directory.Exists(profilesBase))
        {
            foreach (var profile in Directory.GetDirectories(profilesBase))
            {
                var cache2 = Path.Combine(profile, "cache2");
                if (Directory.Exists(cache2))
                    yield return cache2;
            }
        }
    }
}
