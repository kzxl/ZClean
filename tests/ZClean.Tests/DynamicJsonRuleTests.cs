using ZClean.Core.Engine;
using ZClean.Core.Models;
using Xunit;

namespace ZClean.Tests;

public class DynamicJsonRuleTests
{
    [Fact]
    public async Task DynamicJsonRule_LoadsAndScans_TargetSandbox()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "ZClean_JsonRuleTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            var testFile = Path.Combine(sandbox, "custom_cache.log");
            await File.WriteAllTextAsync(testFile, "Log contents");

            var oldDate = DateTime.Now.AddDays(-2);
            File.SetCreationTime(testFile, oldDate);
            File.SetLastWriteTime(testFile, oldDate);

            var json = $@"
            {{
                ""id"": ""app.test.custom"",
                ""name"": ""Custom Test App"",
                ""description"": ""Test custom JSON rule"",
                ""category"": ""Application"",
                ""riskLevel"": ""Safe"",
                ""paths"": [ ""{sandbox.Replace("\\", "\\\\")}"" ],
                ""fileMask"": ""*.log"",
                ""recursive"": true
            }}";

            var rule = DynamicJsonRule.FromJson(json);
            Assert.Equal("app.test.custom", rule.Id);
            Assert.Equal("Custom Test App", rule.Name);
            Assert.Equal(CleanCategory.Application, rule.Category);

            var scan = await rule.ScanAsync(new CleanOptions { MinFileAge = TimeSpan.FromHours(1) });
            Assert.Single(scan.Items);
            Assert.Equal(testFile, scan.Items[0].FilePath);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }
}
