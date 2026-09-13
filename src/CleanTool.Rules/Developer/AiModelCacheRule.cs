using CleanTool.Core.Models;

namespace CleanTool.Rules.Developer;

/// <summary>
/// Cleans downloaded AI/ML model checkpoints (HuggingFace Hub, PyTorch) and GPU shader compilation caches.
/// </summary>
public class AiModelCacheRule : BaseFolderRule
{
    public override string Id => "dev.ai.cache";
    public override string Name => "AI Models & Torch Cache";
    public override string Description => "Cleans cached HuggingFace model checkpoints (~/.cache/huggingface), PyTorch models, and GPU shader caches.";
    public override CleanCategory Category => CleanCategory.Developer;
    public override CleanRiskLevel RiskLevel => CleanRiskLevel.Moderate;
    public override bool IsDefaultEnabled => false; // Opt-in due to multi-GB re-download cost

    protected override IEnumerable<string> GetTargetDirectories()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // 1. HuggingFace Hub Cache
        var hfCache = Path.Combine(userProfile, ".cache", "huggingface", "hub");
        if (Directory.Exists(hfCache))
            yield return hfCache;

        // 2. PyTorch / Torchvision Cache
        var torchCache = Path.Combine(userProfile, ".cache", "torch");
        if (Directory.Exists(torchCache))
            yield return torchCache;

        // 3. NVIDIA Shader Caches
        var nvDx = Path.Combine(localAppData, "NVIDIA", "DXCache");
        if (Directory.Exists(nvDx))
            yield return nvDx;

        var nvGl = Path.Combine(localAppData, "NVIDIA", "GLCache");
        if (Directory.Exists(nvGl))
            yield return nvGl;
    }
}
