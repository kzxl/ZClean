using ZClean.Core.Models;

namespace ZClean.Rules.System;

/// <summary>
/// Cleans temporary files located in the current user's %TEMP% directory.
/// </summary>
public class UserTempRule : BaseFolderRule
{
    public override string Id => "sys.temp.user";
    public override string Name => "User Temporary Files";
    public override string Description => "Cleans temporary runtime files created by user applications in %TEMP%.";
    public override CleanCategory Category => CleanCategory.System;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => true;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var tempPath = Path.GetTempPath();
        if (!string.IsNullOrWhiteSpace(tempPath) && Directory.Exists(tempPath))
        {
            yield return tempPath;
        }
    }
}
