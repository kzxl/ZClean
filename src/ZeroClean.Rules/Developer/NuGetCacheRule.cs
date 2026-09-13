using ZeroClean.Core.Models;

namespace ZeroClean.Rules.Developer;

/// <summary>
/// Cleans cached NuGet packages stored in ~/.nuget/packages.
/// These packages can be automatically restored by 'dotnet restore' whenever needed.
/// </summary>
public class NuGetCacheRule : BaseFolderRule
{
    public override string Id => "dev.nuget.cache";
    public override string Name => "NuGet Package Cache";
    public override string Description => "Cleans global cached NuGet packages (~/.nuget/packages). Packages will be re-downloaded on next build.";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Moderate;
    public override bool IsDefaultEnabled => false; // User-opt in

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nugetPath = Path.Combine(userProfile, ".nuget", "packages");
        if (Directory.Exists(nugetPath))
            yield return nugetPath;
    }
}
