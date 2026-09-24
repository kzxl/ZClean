using ZClean.Core.Models;

namespace ZClean.Rules.System;

/// <summary>
/// Cleans system temporary files located in C:\Windows\Temp.
/// </summary>
public class WindowsTempRule : BaseFolderRule
{
    public override string Id => "sys.temp.windows";
    public override string Name => "Windows System Temp";
    public override string Description => "Cleans operating system temp files in C:\\Windows\\Temp.";
    public override CleanCategory Category => CleanCategory.System;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool RequiresElevation => true;
    public override bool IsDefaultEnabled => true;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var winTemp = Path.Combine(winDir, "Temp");
        if (Directory.Exists(winTemp))
        {
            yield return winTemp;
        }
    }
}
