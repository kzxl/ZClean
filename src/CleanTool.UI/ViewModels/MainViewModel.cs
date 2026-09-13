using System.Collections.ObjectModel;
using System.Windows;
using CleanTool.Core.Contracts;
using CleanTool.Core.Engine;
using CleanTool.Core.Models;
using CleanTool.Rules;

namespace CleanTool.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly CleanerEngine _engine;
    private readonly IRuleRegistry _registry;
    private readonly IDiskAnalyzer _diskAnalyzer;

    private int _selectedTabIndex;
    private bool _isBusy;
    private double _progressValue;
    private string _statusMessage = "Ready. Select items and click 'Scan System'.";
    private long _totalReclaimableBytes;
    private int _totalReclaimableFiles;

    // In-window InfoBar
    private bool _isInfoBarVisible;
    private string _infoBarMessage = "";
    private string _infoBarSeverity = "Info"; // Info, Success, Warning, Error

    public ObservableCollection<RuleItemViewModel> Rules { get; } = new();
    public ObservableCollection<DiskDriveViewModel> Drives { get; } = new();

    public RelayCommand ScanCommand { get; }
    public RelayCommand SimulateCleanCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand RefreshDrivesCommand { get; }
    public RelayCommand DismissInfoBarCommand { get; }

    public MainViewModel()
    {
        _registry = new RuleRegistry();
        RuleRegistrar.RegisterAll(_registry);
        _engine = new CleanerEngine(_registry);
        _diskAnalyzer = new DiskAnalyzerService();

        foreach (var rule in _registry.GetAllRules())
        {
            var vm = new RuleItemViewModel(rule);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RuleItemViewModel.IsSelected))
                {
                    UpdateTotals();
                }
            };
            Rules.Add(vm);
        }

        ScanCommand = new RelayCommand(async () => await ExecuteScanAsync(), () => !IsBusy);
        SimulateCleanCommand = new RelayCommand(async () => await ExecuteSimulateCleanAsync(), () => !IsBusy && TotalReclaimableBytes > 0);
        SelectAllCommand = new RelayCommand(() => SetAllRulesSelection(true));
        DeselectAllCommand = new RelayCommand(() => SetAllRulesSelection(false));
        RefreshDrivesCommand = new RelayCommand(LoadDrives);
        DismissInfoBarCommand = new RelayCommand(() => IsInfoBarVisible = false);

        LoadDrives();
        UpdateTotals();
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ScanCommand.CanExecute(null);
                SimulateCleanCommand.CanExecute(null);
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public long TotalReclaimableBytes
    {
        get => _totalReclaimableBytes;
        set => SetProperty(ref _totalReclaimableBytes, value);
    }

    public int TotalReclaimableFiles
    {
        get => _totalReclaimableFiles;
        set => SetProperty(ref _totalReclaimableFiles, value);
    }

    public bool IsInfoBarVisible
    {
        get => _isInfoBarVisible;
        set => SetProperty(ref _isInfoBarVisible, value);
    }

    public string InfoBarMessage
    {
        get => _infoBarMessage;
        set => SetProperty(ref _infoBarMessage, value);
    }

    public string InfoBarSeverity
    {
        get => _infoBarSeverity;
        set => SetProperty(ref _infoBarSeverity, value);
    }

    private void SetAllRulesSelection(bool selected)
    {
        foreach (var r in Rules)
        {
            r.IsSelected = selected;
        }
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        long totalSize = 0;
        int totalCount = 0;
        foreach (var r in Rules.Where(r => r.IsSelected))
        {
            totalSize += r.ScannedSize;
            totalCount += r.ScannedCount;
        }

        TotalReclaimableBytes = totalSize;
        TotalReclaimableFiles = totalCount;
    }

    public void LoadDrives()
    {
        Drives.Clear();
        foreach (var d in _diskAnalyzer.GetDrives())
        {
            Drives.Add(new DiskDriveViewModel(d));
        }
    }

    public async Task ExecuteScanAsync()
    {
        var selected = Rules.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowInfoBar("Please select at least one cleanup rule to scan.", "Warning");
            return;
        }

        IsBusy = true;
        StatusMessage = "Scanning selected categories (Safe Mode)...";
        ProgressValue = 0;

        try
        {
            var options = new CleanOptions
            {
                DryRun = true,
                MinFileAge = TimeSpan.FromHours(24)
            };

            int index = 0;
            long totalFoundSize = 0;
            int totalFoundCount = 0;

            foreach (var ruleVm in selected)
            {
                ruleVm.IsBusy = true;
                ruleVm.StatusText = "Scanning...";

                var scanResult = await ruleVm.Rule.ScanAsync(options);

                ruleVm.ScannedSize = scanResult.TotalSizeBytes;
                ruleVm.ScannedCount = scanResult.TotalCount;
                ruleVm.StatusText = scanResult.TotalCount > 0 ? "Items found" : "Clean";
                ruleVm.IsBusy = false;

                totalFoundSize += scanResult.TotalSizeBytes;
                totalFoundCount += scanResult.TotalCount;

                index++;
                ProgressValue = ((double)index / selected.Count) * 100;
            }

            UpdateTotals();
            StatusMessage = $"Scan completed. Found {TotalReclaimableFiles:N0} items ({FormatBytes(TotalReclaimableBytes)} reclaimable).";
            ShowInfoBar($"Scan completed successfully! {FormatBytes(TotalReclaimableBytes)} can be safely reclaimed.", "Success");
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan encountered an error.";
            ShowInfoBar($"Scan failed: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteSimulateCleanAsync()
    {
        var selected = Rules.Where(r => r.IsSelected && r.ScannedCount > 0).ToList();
        if (selected.Count == 0)
        {
            ShowInfoBar("No items are currently pending for cleanup.", "Info");
            return;
        }

        IsBusy = true;
        StatusMessage = "Simulating cleanup (Safe Dry-Run Mode)...";
        ProgressValue = 0;

        try
        {
            // Guaranteed Safe DryRun mode to protect machine
            var options = new CleanOptions
            {
                DryRun = true,
                MinFileAge = TimeSpan.FromHours(24)
            };

            int index = 0;
            long simulatedFreed = 0;
            int simulatedDeleted = 0;

            foreach (var ruleVm in selected)
            {
                ruleVm.IsBusy = true;
                ruleVm.StatusText = "Simulating...";

                var cleanResult = await ruleVm.Rule.CleanAsync(options);

                ruleVm.StatusText = "Simulated";
                ruleVm.IsBusy = false;

                simulatedFreed += cleanResult.BytesFreed;
                simulatedDeleted += cleanResult.DeletedCount;

                index++;
                ProgressValue = ((double)index / selected.Count) * 100;
            }

            StatusMessage = $"Simulation finished. Verified {simulatedDeleted:N0} files ({FormatBytes(simulatedFreed)}) can be safely purged.";
            ShowInfoBar($"Simulation completed: {FormatBytes(simulatedFreed)} space reclaim verified (0 files touched).", "Success");
        }
        catch (Exception ex)
        {
            StatusMessage = "Simulation encountered an error.";
            ShowInfoBar($"Simulation failed: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowInfoBar(string message, string severity)
    {
        InfoBarMessage = message;
        InfoBarSeverity = severity;
        IsInfoBarVisible = true;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
