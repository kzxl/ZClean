using ZeroClean.Core.Contracts;
using ZeroClean.Rules.Applications;
using ZeroClean.Rules.Browsers;
using ZeroClean.Rules.Developer;
using ZeroClean.Rules.System;

namespace ZeroClean.Rules;

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
        registry.Register(new BrokenShortcutRule());

        // Developer rules
        registry.Register(new NuGetCacheRule());
        registry.Register(new NpmCacheRule());
        registry.Register(new PipCacheRule());
        registry.Register(new GoBuildCacheRule());
        registry.Register(new AiModelCacheRule());
        registry.Register(new VisualStudioArtifactsRule());

        // Browser rules
        registry.Register(new ChromiumCacheRule());
        registry.Register(new FirefoxCacheRule());

        // Application rules
        registry.Register(new VsCodeCacheRule());
        registry.Register(new DiscordCacheRule());
    }
}
