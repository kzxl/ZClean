using ZClean.Core.Contracts;
using ZClean.Rules.Applications;
using ZClean.Rules.Browsers;
using ZClean.Rules.Developer;
using ZClean.Rules.System;

namespace ZClean.Rules;

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
        registry.Register(new WindowsUpdateDownloadRule());
        registry.Register(new RecycleBinRule());
        registry.Register(new BrokenShortcutRule());

        // Developer rules
        registry.Register(new NuGetCacheRule());
        registry.Register(new NpmCacheRule());
        registry.Register(new PipCacheRule());
        registry.Register(new CargoCacheRule());
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
