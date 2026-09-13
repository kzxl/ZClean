using ZeroClean.Core.Models;

namespace ZeroClean.Rules.Developer;

/// <summary>
/// Cleans compiled package binaries cached by the Go compiler in %LOCALAPPDATA%\go-build.
/// </summary>
public class GoBuildCacheRule : BaseFolderRule
{
    public override string Id => "dev.go.cache";
    public override string Name => "Go Build Cache";
    public override string Description => "Cleans compiled object caches created by 'go build' in %LOCALAPPDATA%\\go-build.";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Moderate;
    public override bool IsDefaultEnabled => false;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var goBuild = Path.Combine(localAppData, "go-build");
        if (Directory.Exists(goBuild))
            yield return goBuild;
    }
}
