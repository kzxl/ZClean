using ZClean.Core.Models;

namespace ZClean.Rules.Applications;

/// <summary>
/// Cleans Visual Studio Code cache, cached extensions, and GPU caches.
/// </summary>
public class VsCodeCacheRule : BaseFolderRule
{
    public override string Id => "app.vscode.cache";
    public override string Name => "VS Code Cache";
    public override string Description => "Cleans cached editor data, GPUCache, and VSIX downloads for Visual Studio Code.";
    public override CleanCategory Category => CleanCategory.Application;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => true;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "Code");

        if (Directory.Exists(baseDir))
        {
            var targets = new[] { "Cache", "CachedData", "CachedExtensionVSIXs", "GPUCache" };
            foreach (var t in targets)
            {
                var full = Path.Combine(baseDir, t);
                if (Directory.Exists(full))
                    yield return full;
            }
        }
    }
}
