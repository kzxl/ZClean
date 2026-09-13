using CleanTool.Core.Models;

namespace CleanTool.UI.ViewModels;

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
    public double UsedPercentage => Info.UsedPercentage;
    public double FreePercentage => Info.FreePercentage;
}
