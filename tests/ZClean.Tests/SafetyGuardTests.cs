using ZClean.Core.Engine;
using ZClean.Core.Models;
using Xunit;

namespace ZClean.Tests;

public class SafetyGuardTests
{
    private readonly SafetyGuard _guard = new();

    [Theory]
    [InlineData(@"C:\Windows\System32\ntoskrnl.exe")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Windows\SysWOW64\kernel32.dll")]
    [InlineData(@"C:\Program Files\Common Files")]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"C:\bootmgr")]
    public void IsProtectedPath_CriticalFilesAndFolders_ReturnsTrue(string path)
    {
        Assert.True(_guard.IsProtectedPath(path));
    }

    [Fact]
    public void IsProtectedPath_DriveRoot_ReturnsTrue()
    {
        Assert.True(_guard.IsProtectedPath(@"C:\"));
        Assert.True(_guard.IsProtectedPath(@"D:\"));
    }

    [Theory]
    [InlineData(@"C:\Windows\Temp\sample_crash.tmp")]
    [InlineData(@"C:\Windows\Prefetch\CMD.EXE-12345678.pf")]
    public void IsProtectedPath_AllowedSystemTempLocations_ReturnsFalse(string path)
    {
        Assert.False(_guard.IsProtectedPath(path));
    }

    [Fact]
    public void IsEligibleForDeletion_ExcludedPattern_ReturnsFalse()
    {
        var options = new CleanOptions
        {
            ExcludedPatterns = new[] { "*.keep", "protected.dat" }
        };

        Assert.False(_guard.IsEligibleForDeletion(@"C:\Temp\important.keep", options));
        Assert.False(_guard.IsEligibleForDeletion(@"C:\Temp\protected.dat", options));
    }
}
