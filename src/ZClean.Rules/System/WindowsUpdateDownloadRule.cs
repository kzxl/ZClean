using ZClean.Core.Models;

namespace ZClean.Rules.System;

/// <summary>
/// Cleans cached Windows Update installer files in C:\Windows\SoftwareDistribution\Download.
/// </summary>
public class WindowsUpdateDownloadRule : BaseFolderRule
{
    public override string Id => "sys.updates.download";
    public override string Name => "Windows Update Download Cache";
    public override string Description => "Cleans completed Windows Update download files in C:\\Windows\\SoftwareDistribution\\Download.";
    public override CleanCategory Category => CleanCategory.System;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool RequiresElevation => true;
    public override bool IsDefaultEnabled => false; // User-selected because Windows Update service might lock active items

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var downloadDir = Path.Combine(winDir, "SoftwareDistribution", "Download");
        if (Directory.Exists(downloadDir))
        {
            yield return downloadDir;
        }
    }
}
