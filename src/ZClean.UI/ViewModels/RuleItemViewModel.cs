using ZClean.Core.Contracts;
using ZClean.Core.Models;

namespace ZClean.UI.ViewModels;

public class RuleItemViewModel : ViewModelBase
{
    private bool _isSelected;
    private long _scannedSize;
    private int _scannedCount;
    private bool _isBusy;
    private string _statusText = "Ready";

    public ICleanerRule Rule { get; }

    public RuleItemViewModel(ICleanerRule rule)
    {
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _isSelected = rule.IsDefaultEnabled;
    }

    public string Id => Rule.Id;
    public string Name => Rule.Name;
    public string Description => Rule.Description;
    public CleanCategory Category => Rule.Category;
    public CleanRiskLevel RiskLevel => Rule.RiskLevel;
    public bool RequiresElevation => Rule.RequiresElevation;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public long ScannedSize
    {
        get => _scannedSize;
        set => SetProperty(ref _scannedSize, value);
    }

    public int ScannedCount
    {
        get => _scannedCount;
        set => SetProperty(ref _scannedCount, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}
