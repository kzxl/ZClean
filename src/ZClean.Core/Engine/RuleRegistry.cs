using System.Collections.Concurrent;
using ZClean.Core.Contracts;
using ZClean.Core.Models;

namespace ZClean.Core.Engine;

/// <summary>
/// Autonomous rule container supporting registration and querying.
/// </summary>
public class RuleRegistry : IRuleRegistry
{
    private readonly ConcurrentDictionary<string, ICleanerRule> _rules = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICleanerRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules[rule.Id] = rule;
    }

    public ICleanerRule? GetRule(string id)
    {
        return _rules.TryGetValue(id, out var rule) ? rule : null;
    }

    public IReadOnlyList<ICleanerRule> GetAllRules()
    {
        return _rules.Values.OrderBy(r => r.Category).ThenBy(r => r.Name).ToList();
    }

    public IReadOnlyList<ICleanerRule> GetRulesByCategory(CleanCategory category)
    {
        return _rules.Values.Where(r => r.Category == category).OrderBy(r => r.Name).ToList();
    }
}
