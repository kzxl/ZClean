namespace ZClean.Core.Engine;

public enum DriveAlertLevel
{
    Normal,
    Warning,
    Critical
}

public record DriveSentinelStatus
{
    public required string DriveName { get; init; }
    public string DriveLabel { get; init; } = "";
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double PercentFree => TotalBytes > 0 ? (FreeBytes * 100.0) / TotalBytes : 0;
    public string TotalFormatted => FormatSize(TotalBytes);
    public string FreeFormatted => FormatSize(FreeBytes);
    public DriveAlertLevel AlertLevel { get; init; }
    public string? AlertMessage { get; init; }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {suffixes[order]}";
    }
}

public record StorageSentinelReport
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public IReadOnlyList<DriveSentinelStatus> Drives { get; init; } = Array.Empty<DriveSentinelStatus>();
    public DriveAlertLevel OverallStatus { get; init; }
    public int CriticalCount => Drives.Count(d => d.AlertLevel == DriveAlertLevel.Critical);
    public int WarningCount => Drives.Count(d => d.AlertLevel == DriveAlertLevel.Warning);
    public IReadOnlyList<string> ActionableRecommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Continuous or on-demand storage threshold watchdog.
/// Monitors free space percentages and disk margins across all fixed drives.
/// </summary>
public class StorageSentinelService
{
    public const double WarningPercentThreshold = 15.0;
    public const double CriticalPercentThreshold = 5.0;
    public const long WarningBytesThreshold = 15L * 1024 * 1024 * 1024;  // 15 GB
    public const long CriticalBytesThreshold = 5L * 1024 * 1024 * 1024;    // 5 GB

    /// <summary>
    /// Evaluates storage status across all mounted fixed drives.
    /// </summary>
    public StorageSentinelReport EvaluateSystemDrives(IEnumerable<DriveInfo>? drives = null)
    {
        var targetDrives = drives ?? DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
        var statusList = new List<DriveSentinelStatus>();

        foreach (var d in targetDrives)
        {
            try
            {
                var status = EvaluateDrive(
                    d.Name,
                    string.IsNullOrWhiteSpace(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                    d.TotalSize,
                    d.AvailableFreeSpace);

                statusList.Add(status);
            }
            catch
            {
                // Unreadable drive skipped
            }
        }

        var overall = statusList.Any(s => s.AlertLevel == DriveAlertLevel.Critical)
            ? DriveAlertLevel.Critical
            : statusList.Any(s => s.AlertLevel == DriveAlertLevel.Warning)
                ? DriveAlertLevel.Warning
                : DriveAlertLevel.Normal;

        var recs = GenerateSentinelRecommendations(statusList, overall);

        return new StorageSentinelReport
        {
            Timestamp = DateTime.Now,
            Drives = statusList,
            OverallStatus = overall,
            ActionableRecommendations = recs
        };
    }

    /// <summary>
    /// Evaluates threshold indicators for a single drive.
    /// </summary>
    public DriveSentinelStatus EvaluateDrive(string name, string label, long totalBytes, long freeBytes)
    {
        var percentFree = totalBytes > 0 ? (freeBytes * 100.0) / totalBytes : 0;
        DriveAlertLevel level = DriveAlertLevel.Normal;
        string? msg = null;

        if (percentFree < CriticalPercentThreshold || freeBytes < CriticalBytesThreshold)
        {
            level = DriveAlertLevel.Critical;
            msg = $"CRITICAL: Free space ({percentFree:0.0}%, {freeBytes / (1024 * 1024 * 1024.0):0.0} GB) is below critical threshold (5% or 5 GB).";
        }
        else if (percentFree < WarningPercentThreshold || freeBytes < WarningBytesThreshold)
        {
            level = DriveAlertLevel.Warning;
            msg = $"WARNING: Free space ({percentFree:0.0}%, {freeBytes / (1024 * 1024 * 1024.0):0.0} GB) is approaching low capacity limit (15% or 15 GB).";
        }

        return new DriveSentinelStatus
        {
            DriveName = name,
            DriveLabel = label,
            TotalBytes = totalBytes,
            FreeBytes = freeBytes,
            AlertLevel = level,
            AlertMessage = msg
        };
    }

    private static List<string> GenerateSentinelRecommendations(List<DriveSentinelStatus> drives, DriveAlertLevel overall)
    {
        var recs = new List<string>();

        if (overall == DriveAlertLevel.Normal)
        {
            recs.Add("All drive capacities are healthy (> 15% and > 15 GB free). Routine maintenance is on track.");
            return recs;
        }

        var troubledDrives = drives.Where(d => d.AlertLevel != DriveAlertLevel.Normal).Select(d => d.DriveName.TrimEnd('\\'));
        var driveNames = string.Join(", ", troubledDrives);

        recs.Add($"Storage pressure detected on drive(s): {driveNames}.");
        recs.Add("1. Run 'cleantool clean --profile Quick' to immediately purge user and system temp caches.");
        recs.Add("2. Run 'cleantool dev' to clean dangling node_modules, bin/obj, and package caches in projects.");
        recs.Add("3. Run 'cleantool dism' to inspect superseded Windows update packages in WinSxS.");
        recs.Add("4. Run 'cleantool large-files' to identify large installers or ISO disk images consuming disk space.");
        recs.Add("5. Run 'cleantool wsl' to inspect and compact oversized WSL2 ext4.vhdx virtual disks.");

        return recs;
    }
}
