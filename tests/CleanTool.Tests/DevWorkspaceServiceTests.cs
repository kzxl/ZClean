using CleanTool.Core.Engine;
using Xunit;

namespace CleanTool.Tests;

public class DevWorkspaceServiceTests
{
    [Fact]
    public async Task ScanWorkspacesAsync_DetectsRepositoryAndArtifacts()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "CleanTool_DevWsTest_" + Guid.NewGuid().ToString("N"));
        var repoDir = Path.Combine(sandbox, "MySampleRepo");
        var nodeModules = Path.Combine(repoDir, "node_modules", "sample_pkg");
        var binDir = Path.Combine(repoDir, "bin", "Debug");

        Directory.CreateDirectory(nodeModules);
        Directory.CreateDirectory(binDir);

        try
        {
            // Create repo marker
            await File.WriteAllTextAsync(Path.Combine(repoDir, "package.json"), "{}");

            // Create dummy build artifacts
            await File.WriteAllTextAsync(Path.Combine(nodeModules, "index.js"), "console.log(1);");
            await File.WriteAllTextAsync(Path.Combine(binDir, "app.dll"), "fake binary payload");

            var service = new DevWorkspaceService();
            var repos = await service.ScanWorkspacesAsync(sandbox, dormantDaysThreshold: 30);

            Assert.Single(repos);
            var repo = repos[0];
            Assert.Equal("MySampleRepo", repo.RepoName);
            Assert.Equal(2, repo.Artifacts.Count);

            var types = repo.Artifacts.Select(a => a.ArtifactType).ToList();
            Assert.Contains("node_modules", types);
            Assert.Contains("bin", types);
            Assert.True(repo.TotalReclaimableBytes > 0);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("id_rsa")]
    [InlineData("id_ed25519")]
    [InlineData("server.key")]
    [InlineData("certificate.pem")]
    [InlineData(@"C:\Projects\MyRepo\.git\config")]
    public void SafetyGuard_ProtectsDeveloperSecrets(string path)
    {
        var guard = new SafetyGuard();
        Assert.True(guard.IsProtectedPath(path));
    }
}
