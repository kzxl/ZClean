using CleanTool.Core.Models;

namespace CleanTool.Rules.Developer;

/// <summary>
/// Cleans Visual Studio MEF ComponentModelCache and build telemetry.
/// Resolves IDE freeze and broken extension catalog issues.
/// </summary>
public class VisualStudioArtifactsRule : BaseFolderRule
{
    public override string Id => "dev.vs.cache";
    public override string Name => "Visual Studio Component Cache";
    public override string Description => "Cleans Visual Studio ComponentModelCache and Roslyn server logs to resolve IDE corruption.";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => false;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var vsBase = Path.Combine(localAppData, "Microsoft", "VisualStudio");

        if (Directory.Exists(vsBase))
        {
            foreach (var vsInstance in Directory.GetDirectories(vsBase))
            {
                var componentCache = Path.Combine(vsInstance, "ComponentModelCache");
                if (Directory.Exists(componentCache))
                    yield return componentCache;

                var roslynLogs = Path.Combine(vsInstance, "Roslyn");
                if (Directory.Exists(roslynLogs))
                    yield return roslynLogs;
            }
        }
    }
}
