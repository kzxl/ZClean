using CleanTool.Core.Contracts;
using CleanTool.Rules.Applications;
using CleanTool.Rules.Browsers;
using CleanTool.Rules.Developer;
using CleanTool.Rules.System;

namespace CleanTool.Rules;

/// <summary>
/// Facilitates discovery and registration of all built-in cleaner rules.
/// </summary>
public static class RuleRegistrar
{
    public static void RegisterAll(IRuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        // System rules
        registry.Register(new UserTempRule());
        registry.Register(new WindowsTempRule());
        registry.Register(new CrashDumpsRule());
        registry.Register(new ThumbnailCacheRule());
        registry.Register(new WindowsErrorReportingRule());
        registry.Register(new RecycleBinRule());

        // Developer rules
        registry.Register(new NuGetCacheRule());
        registry.Register(new NpmCacheRule());
        registry.Register(new PipCacheRule());
        registry.Register(new GoBuildCacheRule());

        // Browser rules
        registry.Register(new ChromiumCacheRule());
        registry.Register(new FirefoxCacheRule());

        // Application rules
        registry.Register(new VsCodeCacheRule());
        registry.Register(new DiscordCacheRule());
    }
}
