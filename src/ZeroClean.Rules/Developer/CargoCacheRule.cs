using ZeroClean.Core.Models;

namespace ZeroClean.Rules.Developer;

/// <summary>
/// Cleans cached Rust/Cargo crate archives (.crate) stored in ~/.cargo/registry/cache.
/// </summary>
public class CargoCacheRule : BaseFolderRule
{
    public override string Id => "dev.cargo.cache";
    public override string Name => "Cargo (Rust) Crate Cache";
    public override string Description => "Cleans downloaded Rust package archives (.crate) in ~/.cargo/registry/cache.";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public override bool IsDefaultEnabled => false;

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cargoCache = Path.Combine(userProfile, ".cargo", "registry", "cache");
        if (Directory.Exists(cargoCache))
        {
            yield return cargoCache;
        }
    }
}
