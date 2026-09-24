using ZClean.Core.Contracts;
using ZClean.Core.Engine;
using ZClean.Core.Models;
using ZClean.Rules;
using Xunit;

namespace ZClean.Tests;

public class CleanerEngineTests
{
    private class DummySandboxFolderRule : BaseFolderRule
    {
        private readonly string _sandboxDir;

        public DummySandboxFolderRule(string sandboxDir, ISafetyGuard safetyGuard, IFileLockDetector fileLockDetector)
            : base(safetyGuard, fileLockDetector)
        {
            _sandboxDir = sandboxDir;
        }

        public override string Id => "test.sandbox.dummy";
        public override string Name => "Dummy Sandbox Rule";
        public override string Description => "Test-only rule targeting isolated temp folder.";
        public override CleanCategory Category => CleanCategory.System;

        protected override IEnumerable<string> GetTargetDirectories()
        {
            yield return _sandboxDir;
        }
    }

    [Fact]
    public async Task CleanerEngine_DryRun_DoesNotDeleteFiles()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZClean_Test_Sandbox_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            var dummyFile = Path.Combine(sandbox, "dummy.tmp");
            await File.WriteAllTextAsync(dummyFile, "ZClean test payload");
            var oldDate = DateTime.Now.AddDays(-3);
            File.SetCreationTime(dummyFile, oldDate);
            File.SetLastWriteTime(dummyFile, oldDate);

            var registry = new RuleRegistry();
            registry.Register(new DummySandboxFolderRule(sandbox, new SafetyGuard(), new FileLockDetector()));

            var engine = new CleanerEngine(registry);
            var options = new CleanOptions
            {
                DryRun = true,
                MinFileAge = TimeSpan.FromHours(1)
            };

            var cleanResults = await engine.CleanAsync(new[] { "test.sandbox.dummy" }, options);

            Assert.Single(cleanResults);
            Assert.Equal(1, cleanResults[0].DeletedCount);
            // File must still physically exist because DryRun was true
            Assert.True(File.Exists(dummyFile));
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }

    [Fact]
    public async Task CleanerEngine_LiveExecutionOnSandbox_DeletesOnlySandboxFiles()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZClean_Test_Sandbox_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            var dummyFile1 = Path.Combine(sandbox, "test1.tmp");
            var dummyFile2 = Path.Combine(sandbox, "test2.tmp");
            await File.WriteAllTextAsync(dummyFile1, "Sample data 1");
            await File.WriteAllTextAsync(dummyFile2, "Sample data 2");
            var oldDate = DateTime.Now.AddDays(-3);
            File.SetCreationTime(dummyFile1, oldDate);
            File.SetLastWriteTime(dummyFile1, oldDate);
            File.SetCreationTime(dummyFile2, oldDate);
            File.SetLastWriteTime(dummyFile2, oldDate);

            var registry = new RuleRegistry();
            registry.Register(new DummySandboxFolderRule(sandbox, new SafetyGuard(), new FileLockDetector()));

            var engine = new CleanerEngine(registry);
            var options = new CleanOptions
            {
                DryRun = false,
                MinFileAge = TimeSpan.FromHours(1)
            };

            var cleanResults = await engine.CleanAsync(new[] { "test.sandbox.dummy" }, options);

            Assert.Single(cleanResults);
            Assert.Equal(2, cleanResults[0].DeletedCount);
            Assert.False(File.Exists(dummyFile1));
            Assert.False(File.Exists(dummyFile2));
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }
}
