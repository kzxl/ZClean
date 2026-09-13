using ZeroClean.Core.Engine;
using ZeroClean.Core.Models;
using ZeroClean.Rules;
using Xunit;

namespace ZeroClean.Tests;

public class RuleRegistryTests
{
    [Fact]
    public void RegisterAll_RegistersStandardRules()
    {
        var registry = new RuleRegistry();
        RuleRegistrar.RegisterAll(registry);

        var allRules = registry.GetAllRules();
        Assert.NotEmpty(allRules);

        var systemRules = registry.GetRulesByCategory(CleanCategory.System);
        Assert.Contains(systemRules, r => r.Id == "sys.temp.user");
        Assert.Contains(systemRules, r => r.Id == "sys.recyclebin");

        var devRules = registry.GetRulesByCategory(CleanCategory.Developer);
        Assert.Contains(devRules, r => r.Id == "dev.nuget.cache");

        var browserRules = registry.GetRulesByCategory(CleanCategory.Browser);
        Assert.Contains(browserRules, r => r.Id == "browser.chromium.cache");
    }
}
