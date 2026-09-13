using ZeroClean.Core.Models;

namespace ZeroClean.UI.ViewModels;

public class DiskDriveViewModel : ViewModelBase
{
    public DiskSpaceInfo Info { get; }

    public DiskDriveViewModel(DiskSpaceInfo info)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
    }

    public string DriveName => Info.DriveName;
    public string VolumeLabel => Info.VolumeLabel;
    public string DriveFormat => Info.DriveFormat;
    public long TotalBytes => Info.TotalBytes;
    public long FreeBytes => Info.FreeBytes;
    public long UsedBytes => Info.UsedBytes;
    public double UsedPercentage { get => Info.UsedPercentage; set { } }
    public double FreePercentage { get => Info.FreePercentage; set { } }

    public bool IsCriticalSpace => FreePercentage < 15.0;
    public bool IsWarningSpace => FreePercentage >= 15.0 && FreePercentage < 25.0;
    public string StatusColor => IsCriticalSpace ? "#EF4444" : (IsWarningSpace ? "#F59E0B" : "#10B981");
    public string StatusBadgeText => IsCriticalSpace ? "CRITICAL LOW SPACE" : (IsWarningSpace ? "LOW CAPACITY" : "HEALTHY CAPACITY");
}
