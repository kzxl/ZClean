using ZeroClean.Core.Contracts;
using ZeroClean.Core.Engine;
using Xunit;

namespace ZeroClean.Tests;

public class ExtensionOptionsTests
{
    private readonly string _sandboxDir;

    public ExtensionOptionsTests()
    {
        _sandboxDir = Path.Combine(Path.GetTempPath(), "cleantool_tests_ext_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sandboxDir);
    }

    [Fact]
    public async Task FileShredder_DoDMethod_OverwritesAndDeletesFile()
    {
        // Arrange
        var filePath = Path.Combine(_sandboxDir, "secret_data.txt");
        await File.WriteAllTextAsync(filePath, "VERY_CONFIDENTIAL_KEY_XYZ_1234567890_HELLO_WORLD");
        var shredder = new FileShredderService();

        // Act
        var result = await shredder.ShredFileAsync(filePath, new ShredOptions
        {
            DryRun = false,
            Method = ShredMethod.DoD_5220_22_M
        });

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsDryRun);
        Assert.Equal(1, result.FilesShredded);
        Assert.Equal(3, result.PassesPerformed);
        Assert.True(result.TotalBytesShredded > 0);
        Assert.False(File.Exists(filePath), "Shredded file must no longer exist on disk.");
    }

    [Fact]
    public async Task FileShredder_DryRun_PreservesOriginalFile()
    {
        // Arrange
        var filePath = Path.Combine(_sandboxDir, "preserve_me.txt");
        await File.WriteAllTextAsync(filePath, "Sample content to preserve during dry-run.");
        var shredder = new FileShredderService();

        // Act
        var result = await shredder.ShredFileAsync(filePath, new ShredOptions { DryRun = true });

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsDryRun);
        Assert.Equal(1, result.FilesShredded);
        Assert.True(File.Exists(filePath), "Original file must be preserved in DryRun mode.");
    }

    [Fact]
    public async Task FileShredder_DirectoryShred_ObliteratesDirectoryTree()
    {
        // Arrange
        var subDir = Path.Combine(_sandboxDir, "shred_folder");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "file1.log"), "Log 1 data");
        await File.WriteAllTextAsync(Path.Combine(subDir, "file2.log"), "Log 2 data");

        var shredder = new FileShredderService();

        // Act
        var result = await shredder.ShredDirectoryAsync(subDir, new ShredOptions { DryRun = false });

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsDryRun);
        Assert.Equal(2, result.FilesShredded);
        Assert.False(Directory.Exists(subDir), "Target directory tree must be removed.");
    }

    [Fact]
    public async Task FileShredder_ProtectedSystemPath_ThrowsSecurityViolation()
    {
        // Arrange
        var shredder = new FileShredderService();
        var sysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await shredder.ShredFileAsync(sysPath, new ShredOptions { DryRun = false });
        });
    }

    [Fact]
    public void StorageSentinel_EvaluateDrive_AssignsCorrectThresholds()
    {
        // Arrange
        var sentinel = new StorageSentinelService();

        // Critical: 100 GB total, 4 GB free (4%)
        var critical = sentinel.EvaluateDrive("C:\\", "OSDisk", 100L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);
        Assert.Equal(DriveAlertLevel.Critical, critical.AlertLevel);
        Assert.Contains("CRITICAL", critical.AlertMessage);

        // Warning: 100 GB total, 12 GB free (12%)
        var warning = sentinel.EvaluateDrive("D:\\", "Data", 100L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
        Assert.Equal(DriveAlertLevel.Warning, warning.AlertLevel);
        Assert.Contains("WARNING", warning.AlertMessage);

        // Normal: 100 GB total, 50 GB free (50%)
        var normal = sentinel.EvaluateDrive("E:\\", "Storage", 100L * 1024 * 1024 * 1024, 50L * 1024 * 1024 * 1024);
        Assert.Equal(DriveAlertLevel.Normal, normal.AlertLevel);
        Assert.Null(normal.AlertMessage);
    }

    [Fact]
    public void DismService_ParseDismOutput_CorrectlyExtractsMetrics()
    {
        // Arrange
        var dism = new DismComponentService();
        var sampleOutput = @"
Deployment Image Servicing and Management tool
Version: 10.0.22621.1

Image Version: 10.0.22621.1

[==========================100.0%==========================]

Component Store (WinSxS) Size : 8.95 GB
Actual Size of Component Store : 8.23 GB
Shared with Windows : 4.71 GB
Backups and Disabled Features : 3.52 GB
Cache and Temporary Data : 0.00 KB
Date of Last Cleanup : 2026-03-01 10:20:00
Number of Superseded Packages : 3
Component Store Cleanup Recommended : Yes

The operation completed successfully.
";

        // Act
        var report = dism.ParseDismOutput(sampleOutput);

        // Assert
        Assert.True(report.Success);
        Assert.Equal("8.95 GB", report.ComponentStoreSize);
        Assert.Equal("8.23 GB", report.ActualSize);
        Assert.Equal("4.71 GB", report.SharedWithWindows);
        Assert.Equal("3.52 GB", report.BackupsAndFeatures);
        Assert.Equal(3, report.SupersededPackagesCount);
        Assert.True(report.IsCleanupRecommended);
    }

    [Fact]
    public async Task DismService_RunCleanupAsync_DryRunGeneratesResetBaseCommand()
    {
        // Arrange
        var dism = new DismComponentService();

        // Act
        var result = await dism.RunCleanupAsync(resetBase: true, dryRun: true);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsDryRun);
        Assert.True(result.ResetBase);
        Assert.Contains("/StartComponentCleanup", result.Command);
        Assert.Contains("/ResetBase", result.Command);
    }

    [Fact]
    public void DockerWslService_GenerateDiskpartScript_GeneratesCorrectCommands()
    {
        // Arrange
        var wsl = new DockerWslService();
        var targetPath = @"C:\WSL\Ubuntu\ext4.vhdx";

        // Act
        var script = wsl.GenerateDiskpartScript(targetPath);

        // Assert
        Assert.Contains($"select vdisk file=\"{targetPath}\"", script);
        Assert.Contains("attach vdisk readonly", script);
        Assert.Contains("compact vdisk", script);
        Assert.Contains("detach vdisk", script);
    }

    [Fact]
    public void DockerWslService_ParseDockerDfOutput_ExtractsUsageTable()
    {
        // Arrange
        var docker = new DockerWslService();
        var sampleOutput = @"
TYPE            TOTAL     ACTIVE    SIZE      RECLAIMABLE
Images          12        4         3.5GB     2.1GB (60%)
Containers      5         1         120MB     80MB (66%)
Local Volumes   8         2         1.2GB     900MB (75%)
Build Cache     30        0         5.4GB     5.4GB (100%)
";

        // Act
        var report = docker.ParseDockerDfOutput(sampleOutput);

        // Assert
        Assert.True(report.IsDockerInstalled);
        Assert.Equal(4, report.Items.Count);

        var images = report.Items.First(i => i.Type == "Images");
        Assert.Equal("12", images.TotalCount);
        Assert.Equal("3.5GB", images.Size);
        Assert.Equal("2.1GB (60%)", images.Reclaimable);

        var buildCache = report.Items.First(i => i.Type == "Build Cache");
        Assert.Equal("5.4GB", buildCache.Size);
        Assert.Equal("5.4GB (100%)", buildCache.Reclaimable);
    }

    [Fact]
    public void VssService_ParseVssOutput_ExtractsShadowCopiesAndStorage()
    {
        // Arrange
        var vss = new VssManagerService();
        var shadowsOutput = @"
Contents of shadow copy set ID: {a1b2c3d4-e5f6-7890-1234-567890abcdef}
   Contained 1 shadow copies at creation time: 2026-03-05 14:30:00
      Shadow Copy ID: {98765432-10fe-dcba-9876-543210fedcba}
      Original Volume name: \\?\Volume{12345678-0000-0000-0000-000000000000}\
      Creation Time: 2026-03-05 14:30:00
      Attributes: Persistent
";
        var storageOutput = @"
Used Shadow Copy Storage space: 12.5 GB (5%)
Allocated Shadow Copy Storage space: 15.0 GB (6%)
Maximum Shadow Copy Storage space: 20.0 GB (8%)
For volume: (C:)\\?\Volume{12345678-0000-0000-0000-000000000000}\
";

        // Act
        var report = vss.ParseVssOutput(shadowsOutput, storageOutput);

        // Assert
        Assert.True(report.Success);
        Assert.Single(report.ShadowCopies);
        Assert.Equal("{98765432-10fe-dcba-9876-543210fedcba}", report.ShadowCopies[0].ShadowCopyId);
        Assert.Single(report.StorageUsage);
        Assert.Equal("12.5 GB (5%)", report.StorageUsage[0].UsedSpace);
    }

    [Fact]
    public async Task VssService_PurgeOldest_DryRunProducesPlannedCommand()
    {
        // Arrange
        var vss = new VssManagerService();

        // Act
        var result = await vss.PurgeOldestShadowAsync("C:", dryRun: true);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsDryRun);
        Assert.Contains("vssadmin delete shadows /for=C: /oldest /quiet", result.Command);
    }
}
