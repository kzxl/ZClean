using ZeroClean.Core.Models;

namespace ZeroClean.Rules.Developer;

/// <summary>
/// Cleans cached npm tarballs and registry metadata in %APPDATA%\npm-cache.
/// </summary>
public class NpmCacheRule : BaseFolderRule
{
    public override string Id => "dev.npm.cache";
    public override string Name => "npm Cache";
    public override string Description => "Cleans local npm download cache (%APPDATA%\\npm-cache). Safe to clear; packages re-download on install.";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Moderate;
    public override bool IsDefaultEnabled => false;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var npmCache = Path.Combine(appData, "npm-cache");
        if (Directory.Exists(npmCache))
            yield return npmCache;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var npmLocal = Path.Combine(localAppData, "npm-cache");
        if (Directory.Exists(npmLocal))
            yield return npmLocal;
    }
}
