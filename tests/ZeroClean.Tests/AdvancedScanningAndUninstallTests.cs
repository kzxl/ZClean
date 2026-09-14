using System.Runtime.Versioning;
using ZeroClean.Core.Engine;
using Xunit;

namespace ZeroClean.Tests;

[SupportedOSPlatform("windows")]
public class AdvancedScanningAndUninstallTests
{
    [Fact]
    public void AppUninstallerService_BuildsQuietCommand_ForMsi()
    {
        var service = new AppUninstallerService();
        var app = new InstalledAppInfo
        {
            Id = "{12345678-ABCD-1234-ABCD-1234567890AB}",
            DisplayName = "Test MSI App",
            UninstallString = "MsiExec.exe /I{12345678-ABCD-1234-ABCD-1234567890AB}"
        };

        var (exe, args) = service.BuildUninstallCommand(app, quiet: true);

        Assert.Equal("MsiExec.exe", exe);
        Assert.Contains("/X", args, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/qn", args);
        Assert.Contains("/norestart", args);
    }

    [Fact]
    public void AppUninstallerService_BuildsQuietCommand_ForInnoSetup()
    {
        var service = new AppUninstallerService();
        var app = new InstalledAppInfo
        {
            Id = "TestInnoApp",
            DisplayName = "Test Inno App",
            UninstallString = "\"C:\\Program Files\\TestApp\\unins000.exe\""
        };

        var (exe, args) = service.BuildUninstallCommand(app, quiet: true);

        Assert.Equal(@"C:\Program Files\TestApp\unins000.exe", exe);
        Assert.Contains("/VERYSILENT", args);
        Assert.Contains("/SUPPRESSMSGBOXES", args);
    }

    [Fact]
    public void AppUninstallerService_BuildsQuietCommand_ForNsis()
    {
        var service = new AppUninstallerService();
        var app = new InstalledAppInfo
        {
            Id = "TestNsisApp",
            DisplayName = "Test NSIS App",
            UninstallString = "\"C:\\Program Files\\TestApp\\uninstall.exe\""
        };

        var (exe, args) = service.BuildUninstallCommand(app, quiet: true);

        Assert.Equal(@"C:\Program Files\TestApp\uninstall.exe", exe);
        Assert.Equal("/S", args);
    }

    [Fact]
    public async Task AppUninstallerService_SimulatesUninstall_DryRun()
    {
        var service = new AppUninstallerService();
        var app = new InstalledAppInfo
        {
            Id = "SampleApp",
            DisplayName = "Sample Application",
            UninstallString = "\"C:\\Program Files\\Sample\\uninstall.exe\""
        };

        var result = await service.UninstallAppAsync(app, quiet: true, dryRun: true);

        Assert.True(result.Success);
        Assert.True(result.WasDryRun);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[DRY-RUN]", result.Message);
        Assert.Contains("uninstall.exe", result.CommandLine);
    }

    [Fact]
    public void AppUninstallerService_ScansTargetedResiduals_InSandbox()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZeroClean_ResidualTest_" + Guid.NewGuid().ToString("N"));
        var residualAppDir = Path.Combine(sandbox, "AcmeCorpSoftware");
        Directory.CreateDirectory(residualAppDir);

        try
        {
            File.WriteAllText(Path.Combine(residualAppDir, "leftover.dat"), "residual settings data");

            var service = new AppUninstallerService();
            var app = new InstalledAppInfo
            {
                Id = "AcmeCorpSoftware",
                DisplayName = "AcmeCorpSoftware",
                UninstallString = "uninst.exe"
            };

            var report = service.ScanAppResiduals(app, customRoots: new[] { sandbox });

            Assert.NotNull(report);
            Assert.Single(report.DirectoriesFound);
            Assert.Equal("AcmeCorpSoftware", report.DirectoriesFound[0].FolderName);
            Assert.True(report.TotalResidualSizeBytes > 0);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }

    [Fact]
    public async Task LargeFileScannerService_FindsAndCategorizesLargeFiles()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZeroClean_LargeFileTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            var isoFile = Path.Combine(sandbox, "ubuntu-disk.iso");
            var zipFile = Path.Combine(sandbox, "archive-backup.zip");
            var tinyFile = Path.Combine(sandbox, "tiny.txt");

            // Write test bytes
            var isoBytes = new byte[100 * 1024]; // 100 KB
            var zipBytes = new byte[60 * 1024];  // 60 KB
            await File.WriteAllBytesAsync(isoFile, isoBytes);
            await File.WriteAllBytesAsync(zipFile, zipBytes);
            await File.WriteAllTextAsync(tinyFile, "hello");

            var scanner = new LargeFileScannerService();
            var results = await scanner.ScanLargeFilesAsync(
                sandbox, 
                minSizeBytes: 50 * 1024, // 50 KB threshold
                maxResults: 10);

            Assert.Equal(2, results.Count);
            Assert.Equal(LargeFileCategory.DiskImage, results[0].Category);
            Assert.Equal(LargeFileCategory.Archive, results[1].Category);
            Assert.True(results[0].SizeBytes >= results[1].SizeBytes);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }

    [Fact]
    public async Task EmptyFolderScannerService_IdentifiesEmptyFolders()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZeroClean_EmptyFolderTest_" + Guid.NewGuid().ToString("N"));
        var emptyChild = Path.Combine(sandbox, "SubA", "DeepEmpty");
        var populatedChild = Path.Combine(sandbox, "SubB");

        Directory.CreateDirectory(emptyChild);
        Directory.CreateDirectory(populatedChild);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(populatedChild, "file.txt"), "content");

            var scanner = new EmptyFolderScannerService();
            var emptyFolders = await scanner.FindEmptyFoldersAsync(sandbox);

            Assert.NotEmpty(emptyFolders);
            Assert.Contains(emptyFolders, f => f.Name == "DeepEmpty");
            Assert.DoesNotContain(emptyFolders, f => f.Name == "SubB");
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }

    [Fact]
    public void AppUninstallerService_GetInstalledApplications_LiveSystem_DoesNotCrash()
    {
        var service = new AppUninstallerService();
        var apps = service.GetInstalledApplications();
        Assert.NotNull(apps);
        var leftovers = service.DetectLeftoverFolders(existingApps: apps);
        Assert.NotNull(leftovers);
    }

    [Fact]
    public void AppUninstallerService_ScanAppResiduals_NeverTargetsRootVendorDirectories()
    {
        var service = new AppUninstallerService();
        var sandbox = Path.Combine(Path.GetTempPath(), "ZeroClean_VendorSafety_" + Guid.NewGuid().ToString("N"));
        var googleDir = Path.Combine(sandbox, "Google");
        var chromeDir = Path.Combine(googleDir, "Chrome");
        Directory.CreateDirectory(chromeDir);
        File.WriteAllText(Path.Combine(chromeDir, "User Data.txt"), "Important Profile Data");

        try
        {
            var app = new InstalledAppInfo
            {
                Id = "GoogleEarth",
                DisplayName = "Google Earth Pro",
                InstallLocation = null
            };

            var analysis = service.ScanAppResiduals(app, customRoots: new[] { sandbox });

            // Ensure the root Google directory is NEVER flagged as residual folder
            Assert.DoesNotContain(analysis.DirectoriesFound, d => string.Equals(d.FolderPath, googleDir, StringComparison.OrdinalIgnoreCase));
            // Ensure Software\Google is never flagged in registry keys
            Assert.DoesNotContain(analysis.RegistryKeysFound, k => k.EndsWith(@"\Software\Google", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }

    [Fact]
    public void AppUninstallerService_LeftoverConfidenceLevel_ScoresAccurately()
    {
        var service = new AppUninstallerService();
        var sandbox = Path.Combine(Path.GetTempPath(), "ZeroClean_ConfidenceTest_" + Guid.NewGuid().ToString("N"));
        var appInstallDir = Path.Combine(sandbox, "CustomApp");
        var vendorSubDir = Path.Combine(sandbox, "AcmeCorp", "WidgetPro");
        var genericDir = Path.Combine(sandbox, "StandaloneWidget");

        Directory.CreateDirectory(appInstallDir);
        Directory.CreateDirectory(vendorSubDir);
        Directory.CreateDirectory(genericDir);

        File.WriteAllText(Path.Combine(appInstallDir, "app.bin"), "bin");
        File.WriteAllText(Path.Combine(vendorSubDir, "widget.dat"), "dat");
        File.WriteAllText(Path.Combine(genericDir, "cache.tmp"), "tmp");

        try
        {
            var app = new InstalledAppInfo
            {
                Id = "CustomAppId",
                DisplayName = "CustomApp",
                InstallLocation = appInstallDir
            };

            var analysis = service.ScanAppResiduals(app, customRoots: new[] { sandbox });
            var appFolder = analysis.DirectoriesFound.FirstOrDefault(d => string.Equals(d.FolderPath, appInstallDir, StringComparison.OrdinalIgnoreCase));
            
            Assert.NotNull(appFolder);
            Assert.Equal(LeftoverConfidenceLevel.Safe, appFolder.Confidence);
            Assert.True(appFolder.IsSelected);
            Assert.Contains("Matches confirmed application installation directory", appFolder.ConfidenceReason);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }
}

