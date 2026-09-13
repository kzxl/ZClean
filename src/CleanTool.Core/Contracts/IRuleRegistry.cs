using CleanTool.Core.Models;

namespace CleanTool.Core.Contracts;

/// <summary>
/// Discovery and registration repository for cleaner rules.
/// </summary>
public interface IRuleRegistry
{
    void Register(ICleanerRule rule);
    ICleanerRule? GetRule(string id);
    IReadOnlyList<ICleanerRule> GetAllRules();
    IReadOnlyList<ICleanerRule> GetRulesByCategory(CleanCategory category);
}
