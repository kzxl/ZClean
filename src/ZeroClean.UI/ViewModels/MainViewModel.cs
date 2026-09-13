using System.Collections.ObjectModel;
using System.Windows;
using ZeroClean.Core.Contracts;
using ZeroClean.Core.Engine;
using ZeroClean.Core.Models;
using ZeroClean.Rules;

namespace ZeroClean.UI.ViewModels;

[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class MainViewModel : ViewModelBase
{
    private readonly CleanerEngine _engine;
    private readonly IRuleRegistry _registry;
    private readonly IDiskAnalyzer _diskAnalyzer;
    private readonly CleanAdvisorService _advisorService;
    private readonly AppUninstallerService _uninstallerService;

    private int _selectedTabIndex;
    private bool _isBusy;
    private double _progressValue;
    private string _statusMessage = "Ready. Select items and click 'Scan System'.";
    private long _totalReclaimableBytes;
    private int _totalReclaimableFiles;

    // Advisor State
    private int _healthScore = 100;
    private string _healthGrade = "A";
    private string _healthSummary = "Run 'Analyze Health' to evaluate system clutter.";

    // Uninstaller State & Metrics
    private bool _isLoadingApps;
    private int _totalAppsCount;
    private long _totalAppsSize;
    private string _totalAppsSizeFormatted = "0 B";
    private int _brokenAppsCount;
    private int _totalLeftoverCount;
    private long _totalLeftoverSize;
    private string _totalLeftoverSizeFormatted = "0 B";
    private string _selectedFilterMode = "All";
    private string _searchAppText = "";
    private InstalledAppInfo? _selectedApp;
    private AppResidualAnalysis? _activeResidualAnalysis;
    private AppLeftoverFolder? _selectedLeftoverFolder;

    // In-window InfoBar
    private bool _isInfoBarVisible;
    private string _infoBarMessage = "";
    private string _infoBarSeverity = "Info"; // Info, Success, Warning, Error

    public ObservableCollection<RuleItemViewModel> Rules { get; } = new();
    public ObservableRangeCollection<RuleItemViewModel> FilteredRules { get; } = new();
    public ObservableCollection<DiskDriveViewModel> Drives { get; } = new();
    public ObservableCollection<CleanRecommendation> Recommendations { get; } = new();
    public ObservableCollection<string> IssuesDetected { get; } = new();
    public ObservableRangeCollection<InstalledAppInfo> InstalledApps { get; } = new();
    public ObservableRangeCollection<InstalledAppInfo> FilteredApps { get; } = new();
    public ObservableRangeCollection<AppLeftoverFolder> LeftoverFolders { get; } = new();

    private readonly LargeFileScannerService _largeFileService = new();
    private readonly DismComponentService _dismService = new();
    private readonly DockerWslService _dockerWslService = new();

    public ObservableRangeCollection<LargeFileInfo> LargeFiles { get; } = new();
    public ObservableRangeCollection<LargeFileInfo> FilteredLargeFiles { get; } = new();
    public ObservableCollection<WslVdiskInfo> WslDisks { get; } = new();

    private string _selectedRuleCategory = "All";
    public string SelectedRuleCategory
    {
        get => _selectedRuleCategory;
        set
        {
            if (SetProperty(ref _selectedRuleCategory, value))
            {
                ApplyRuleCategoryFilter();
            }
        }
    }

    private RuleItemViewModel? _selectedRule;
    public RuleItemViewModel? SelectedRule
    {
        get => _selectedRule;
        set => SetProperty(ref _selectedRule, value);
    }

    private string _selectedLargeFileType = "All";
    public string SelectedLargeFileType
    {
        get => _selectedLargeFileType;
        set
        {
            if (SetProperty(ref _selectedLargeFileType, value))
            {
                ApplyLargeFileFilter();
            }
        }
    }

    private DismAnalysisReport? _dismReport;
    public DismAnalysisReport? DismReport
    {
        get => _dismReport;
        set => SetProperty(ref _dismReport, value);
    }

    private string _activeSectionTitle = "⚡ System Cleaner";
    public string ActiveSectionTitle
    {
        get => _activeSectionTitle;
        set => SetProperty(ref _activeSectionTitle, value);
    }

    private string _activeSectionSubtitle = "Fast temporary files, browser history, crash dumps & package cache sweeping.";
    public string ActiveSectionSubtitle
    {
        get => _activeSectionSubtitle;
        set => SetProperty(ref _activeSectionSubtitle, value);
    }

    public RelayCommand ScanCommand { get; }
    public RelayCommand SimulateCleanCommand { get; }
    public RelayCommand LiveCleanCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand RefreshDrivesCommand { get; }
    public RelayCommand DismissInfoBarCommand { get; }
    public RelayCommand RunAdvisorCommand { get; }
    public RelayCommand LoadAppsCommand { get; }
    public RelayCommand ScanAppResidualsCommand { get; }
    public RelayCommand SimulateUninstallAppCommand { get; }
    public RelayCommand LiveUninstallAppCommand { get; }
    public RelayCommand LoadLeftoversCommand { get; }
    public RelayCommand SetFilterModeCommand { get; }
    public RelayCommand RemoveBrokenEntryCommand { get; }
    public RelayCommand<AppLeftoverFolder> OpenLeftoverFolderCommand { get; }
    public RelayCommand<AppLeftoverFolder> PurgeSelectedLeftoverCommand { get; }
    public RelayCommand ScanLargeFilesCommand { get; }
    public RelayCommand ScanWslDisksCommand { get; }
    public RelayCommand AnalyzeDismCommand { get; }
    public RelayCommand<string> ApplyPresetCommand { get; }
    public RelayCommand<string> SelectRuleCategoryCommand { get; }
    public RelayCommand<string> SelectLargeFileTypeCommand { get; }
    public RelayCommand<CleanRecommendation> FixRecommendationCommand { get; }
    public RelayCommand<LargeFileInfo> OpenLargeFileFolderCommand { get; }
    public RelayCommand<LargeFileInfo> DeleteLargeFileCommand { get; }
    public RelayCommand QuickOptimizeCommand { get; }
    public RelayCommand<object> NavigateTabCommand { get; }

    public MainViewModel()
    {
        _registry = new RuleRegistry();
        RuleRegistrar.RegisterAll(_registry);
        _engine = new CleanerEngine(_registry);
        _diskAnalyzer = new DiskAnalyzerService();
        _advisorService = new CleanAdvisorService(_engine, _registry);
        _uninstallerService = new AppUninstallerService();

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
        ApplyRuleCategoryFilter();

        ScanCommand = new RelayCommand(async () => await ExecuteScanAsync(), () => !IsBusy);
        SimulateCleanCommand = new RelayCommand(async () => await ExecuteSimulateCleanAsync(), () => !IsBusy && TotalReclaimableBytes > 0);
        LiveCleanCommand = new RelayCommand(async () => await ExecuteLiveCleanAsync(), () => !IsBusy && TotalReclaimableBytes > 0);
        SelectAllCommand = new RelayCommand(() => SetAllRulesSelection(true));
        DeselectAllCommand = new RelayCommand(() => SetAllRulesSelection(false));
        RefreshDrivesCommand = new RelayCommand(LoadDrives);
        DismissInfoBarCommand = new RelayCommand(() => IsInfoBarVisible = false);

        RunAdvisorCommand = new RelayCommand(async () => await ExecuteAdvisorAsync(), () => !IsBusy);
        LoadAppsCommand = new RelayCommand(async () => await ExecuteLoadAppsAsync(forceReload: true), () => !IsBusy && !IsLoadingApps);
        ScanAppResidualsCommand = new RelayCommand(ExecuteScanSelectedAppResiduals, () => !IsBusy && !IsLoadingApps && SelectedApp != null);
        SimulateUninstallAppCommand = new RelayCommand(async () => await ExecuteSimulateUninstallAsync(), () => !IsBusy && !IsLoadingApps && SelectedApp != null);
        LiveUninstallAppCommand = new RelayCommand(async () => await ExecuteLiveUninstallAsync(), () => !IsBusy && !IsLoadingApps && SelectedApp != null);
        LoadLeftoversCommand = new RelayCommand(async () => await ExecuteLoadLeftoversAsync(), () => !IsBusy && !IsLoadingApps);
        SetFilterModeCommand = new RelayCommand(param => { if (param is string mode) SelectedFilterMode = mode; });
        RemoveBrokenEntryCommand = new RelayCommand(async () => await ExecuteRemoveBrokenEntryAsync(), () => SelectedApp != null && SelectedApp.IsBroken);
        OpenLeftoverFolderCommand = new RelayCommand<AppLeftoverFolder>(ExecuteOpenLeftoverFolder);
        PurgeSelectedLeftoverCommand = new RelayCommand<AppLeftoverFolder>(async folder => await ExecutePurgeLeftoverAsync(folder));

        ScanLargeFilesCommand = new RelayCommand(async () => await ExecuteScanLargeFilesAsync(), () => !IsBusy);
        ScanWslDisksCommand = new RelayCommand(async () => await ExecuteScanWslDisksAsync(), () => !IsBusy);
        AnalyzeDismCommand = new RelayCommand(async () => await ExecuteAnalyzeDismAsync(), () => !IsBusy);

        ApplyPresetCommand = new RelayCommand<string>(ApplyRulePreset);
        SelectRuleCategoryCommand = new RelayCommand<string>(cat => { if (!string.IsNullOrEmpty(cat)) SelectedRuleCategory = cat; });
        SelectLargeFileTypeCommand = new RelayCommand<string>(type => { if (!string.IsNullOrEmpty(type)) SelectedLargeFileType = type; });
        FixRecommendationCommand = new RelayCommand<CleanRecommendation>(async rec => await ExecuteFixRecommendationAsync(rec));
        OpenLargeFileFolderCommand = new RelayCommand<LargeFileInfo>(ExecuteOpenLargeFileFolder);
        DeleteLargeFileCommand = new RelayCommand<LargeFileInfo>(async file => await ExecuteDeleteLargeFileAsync(file));
        QuickOptimizeCommand = new RelayCommand(async () => await ExecuteQuickOptimizeAsync(), () => !IsBusy);
        NavigateTabCommand = new RelayCommand<object>(param =>
        {
            if (param is int i) SelectedTabIndex = i;
            else if (param != null && int.TryParse(param.ToString(), out int parsed)) SelectedTabIndex = parsed;
        });

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
                OnPropertyChanged(nameof(FooterBadgeText));
                OnPropertyChanged(nameof(FooterBadgeStatus));
                ScanCommand.CanExecute(null);
                SimulateCleanCommand.CanExecute(null);
                LiveCleanCommand.CanExecute(null);
                RunAdvisorCommand.CanExecute(null);
                LoadAppsCommand.CanExecute(null);
                ScanAppResidualsCommand.CanExecute(null);
                SimulateUninstallAppCommand.CanExecute(null);
                LiveUninstallAppCommand.CanExecute(null);
                LoadLeftoversCommand.CanExecute(null);
            }
        }
    }

    public string FooterBadgeText => IsBusy ? "Processing..." : "Ready";
    public string FooterBadgeStatus => IsBusy ? "Accent" : "Online";

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

    public int HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    public string HealthGrade
    {
        get => _healthGrade;
        set => SetProperty(ref _healthGrade, value);
    }

    public string HealthSummary
    {
        get => _healthSummary;
        set => SetProperty(ref _healthSummary, value);
    }

    public bool IsLoadingApps
    {
        get => _isLoadingApps;
        set
        {
            if (SetProperty(ref _isLoadingApps, value))
            {
                LoadAppsCommand.CanExecute(null);
                LoadLeftoversCommand.CanExecute(null);
                OnPropertyChanged(nameof(HasNoApps));
            }
        }
    }

    public bool HasNoApps => !IsLoadingApps && FilteredApps.Count == 0;

    public int TotalAppsCount
    {
        get => _totalAppsCount;
        set => SetProperty(ref _totalAppsCount, value);
    }

    public long TotalAppsSize
    {
        get => _totalAppsSize;
        set => SetProperty(ref _totalAppsSize, value);
    }

    public string TotalAppsSizeFormatted
    {
        get => _totalAppsSizeFormatted;
        set => SetProperty(ref _totalAppsSizeFormatted, value);
    }

    public int BrokenAppsCount
    {
        get => _brokenAppsCount;
        set => SetProperty(ref _brokenAppsCount, value);
    }

    public int TotalLeftoverCount
    {
        get => _totalLeftoverCount;
        set => SetProperty(ref _totalLeftoverCount, value);
    }

    public long TotalLeftoverSize
    {
        get => _totalLeftoverSize;
        set => SetProperty(ref _totalLeftoverSize, value);
    }

    public string TotalLeftoverSizeFormatted
    {
        get => _totalLeftoverSizeFormatted;
        set => SetProperty(ref _totalLeftoverSizeFormatted, value);
    }

    public string SelectedFilterMode
    {
        get => _selectedFilterMode;
        set
        {
            if (SetProperty(ref _selectedFilterMode, value))
            {
                ApplyAppFilter();
            }
        }
    }

    public AppLeftoverFolder? SelectedLeftoverFolder
    {
        get => _selectedLeftoverFolder;
        set => SetProperty(ref _selectedLeftoverFolder, value);
    }

    public string SearchAppText
    {
        get => _searchAppText;
        set
        {
            if (SetProperty(ref _searchAppText, value))
            {
                ApplyAppFilter();
            }
        }
    }

    public InstalledAppInfo? SelectedApp
    {
        get => _selectedApp;
        set
        {
            if (SetProperty(ref _selectedApp, value))
            {
                ScanAppResidualsCommand.CanExecute(null);
                SimulateUninstallAppCommand.CanExecute(null);
                LiveUninstallAppCommand.CanExecute(null);
                RemoveBrokenEntryCommand.CanExecute(null);
            }
        }
    }

    public AppResidualAnalysis? ActiveResidualAnalysis
    {
        get => _activeResidualAnalysis;
        set => SetProperty(ref _activeResidualAnalysis, value);
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

    public void LoadDrives()
    {
        Drives.Clear();
        foreach (var drive in _diskAnalyzer.GetDrives())
        {
            Drives.Add(new DiskDriveViewModel(drive));
        }
    }

    public void SetAllRulesSelection(bool selected)
    {
        foreach (var rule in Rules)
        {
            rule.IsSelected = selected;
        }
        UpdateTotals();
    }

    public void UpdateTotals()
    {
        TotalReclaimableBytes = Rules.Where(r => r.IsSelected).Sum(r => r.ScannedSize);
        TotalReclaimableFiles = Rules.Where(r => r.IsSelected).Sum(r => r.ScannedCount);
    }

    public async Task ExecuteScanAsync()
    {
        var selected = Rules.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowInfoBar("Please select at least one rule to scan.", "Warning");
            return;
        }

        IsBusy = true;
        StatusMessage = "Scanning selected targets...";
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
                ruleVm.StatusText = $"{scanResult.TotalCount:N0} items ({FormatBytes(scanResult.TotalSizeBytes)})";
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

    public async Task ExecuteLiveCleanAsync()
    {
        var selected = Rules.Where(r => r.IsSelected && r.ScannedCount > 0).ToList();
        if (selected.Count == 0)
        {
            ShowInfoBar("No items are currently pending for cleanup.", "Info");
            return;
        }

        long totalBytes = selected.Sum(r => r.ScannedSize);
        int totalFiles = selected.Sum(r => r.ScannedCount);

        // Explicit user confirmation required
        var result = MessageBox.Show(
            $"⚠️ PERMANENT DELETION WARNING:\n\nAre you sure you want to permanently delete {totalFiles:N0} files ({FormatBytes(totalBytes)}) across {selected.Count} selected categories?\n\nThis physical deletion cannot be undone!",
            "Confirm System Cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            ShowInfoBar("Cleanup cancelled by user. No files were modified.", "Info");
            return;
        }

        IsBusy = true;
        StatusMessage = "Performing live system cleanup...";
        ProgressValue = 0;

        try
        {
            var options = new CleanOptions
            {
                DryRun = false, // Physical deletion confirmed by user!
                MinFileAge = TimeSpan.FromHours(24)
            };

            int index = 0;
            long totalFreed = 0;
            int totalDeleted = 0;

            foreach (var ruleVm in selected)
            {
                ruleVm.IsBusy = true;
                ruleVm.StatusText = "Cleaning...";

                var cleanResult = await ruleVm.Rule.CleanAsync(options);

                ruleVm.ScannedSize = 0;
                ruleVm.ScannedCount = 0;
                ruleVm.StatusText = $"Deleted {cleanResult.DeletedCount:N0} ({FormatBytes(cleanResult.BytesFreed)})";
                ruleVm.IsBusy = false;

                totalFreed += cleanResult.BytesFreed;
                totalDeleted += cleanResult.DeletedCount;

                index++;
                ProgressValue = ((double)index / selected.Count) * 100;
            }

            UpdateTotals();
            StatusMessage = $"Cleanup finished: Reclaimed {FormatBytes(totalFreed)} ({totalDeleted:N0} files).";
            ShowInfoBar($"Successfully cleaned {FormatBytes(totalFreed)} of disk storage.", "Success");
        }
        catch (Exception ex)
        {
            StatusMessage = "Error encountered during cleanup.";
            ShowInfoBar($"Cleanup failed: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteAdvisorAsync()
    {
        IsBusy = true;
        StatusMessage = "Generating system health recommendations...";
        ProgressValue = 30;

        try
        {
            var report = await _advisorService.GenerateRecommendationsAsync();
            HealthScore = report.HealthScore.Score;
            HealthGrade = report.HealthScore.Grade.ToString();
            HealthSummary = report.HealthScore.Summary;

            IssuesDetected.Clear();
            foreach (var issue in report.HealthScore.IssuesDetected)
            {
                IssuesDetected.Add(issue);
            }

            Recommendations.Clear();
            foreach (var rec in report.Recommendations)
            {
                Recommendations.Add(rec);
            }

            ProgressValue = 100;
            StatusMessage = $"Advisor assessment complete: {Recommendations.Count} recommendations generated.";
            ShowInfoBar($"Advisor: System Health Score {HealthScore}/100 (Grade {HealthGrade}). {FormatBytes(report.TotalPotentialSavingsBytes)} reclaimable.", "Info");
        }
        catch (Exception ex)
        {
            StatusMessage = "Advisor analysis failed.";
            ShowInfoBar($"Advisor analysis failed: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteLoadAppsAsync(bool forceReload = false)
    {
        if (IsLoadingApps) return;
        if (!forceReload && InstalledApps.Count > 0) return;

        IsLoadingApps = true;
        StatusMessage = "Enumerating installed applications and scanning registry hives...";

        try
        {
            var (apps, leftovers) = await Task.Run(() =>
            {
                var scannedApps = _uninstallerService.GetInstalledApplications();
                var scannedLeftovers = _uninstallerService.DetectLeftoverFolders(existingApps: scannedApps);
                return (scannedApps, scannedLeftovers);
            });

            InstalledApps.ReplaceRange(apps);
            LeftoverFolders.ReplaceRange(leftovers);

            TotalAppsCount = InstalledApps.Count;
            TotalAppsSize = InstalledApps.Sum(a => a.EstimatedSizeBytes);
            TotalAppsSizeFormatted = FormatBytes(TotalAppsSize);
            BrokenAppsCount = InstalledApps.Count(a => a.IsBroken);
            TotalLeftoverCount = LeftoverFolders.Count;
            TotalLeftoverSize = LeftoverFolders.Sum(l => l.EstimatedSizeBytes);
            TotalLeftoverSizeFormatted = FormatBytes(TotalLeftoverSize);

            ApplyAppFilter();
            if (SelectedApp == null && FilteredApps.Count > 0)
            {
                SelectedApp = FilteredApps[0];
            }
            OnPropertyChanged(nameof(HasNoApps));
            StatusMessage = $"Software inventory loaded: {TotalAppsCount} applications ({TotalAppsSizeFormatted}), {TotalLeftoverCount} orphan leftovers ({TotalLeftoverSizeFormatted}).";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load installed applications.";
            ShowInfoBar($"Failed to load installed apps: {ex.Message}", "Error");
        }
        finally
        {
            IsLoadingApps = false;
        }
    }

    public void ApplyAppFilter()
    {
        var q = InstalledApps.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchAppText))
        {
            q = q.Where(a => a.DisplayName.Contains(SearchAppText, StringComparison.OrdinalIgnoreCase) ||
                             a.Publisher.Contains(SearchAppText, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedFilterMode == "Large")
        {
            q = q.Where(a => a.EstimatedSizeBytes >= 500L * 1024 * 1024);
        }
        else if (SelectedFilterMode == "Broken")
        {
            q = q.Where(a => a.IsBroken);
        }

        var results = q.OrderByDescending(a => a.EstimatedSizeBytes).ToList();
        FilteredApps.ReplaceRange(results);
        if (SelectedApp != null && !results.Contains(SelectedApp))
        {
            SelectedApp = results.FirstOrDefault();
        }
        else if (SelectedApp == null && results.Count > 0)
        {
            SelectedApp = results[0];
        }
        OnPropertyChanged(nameof(HasNoApps));
    }

    public void ExecuteScanSelectedAppResiduals()
    {
        if (SelectedApp == null) return;

        IsBusy = true;
        StatusMessage = $"Scanning residuals for {SelectedApp.DisplayName}...";

        try
        {
            ActiveResidualAnalysis = _uninstallerService.ScanAppResiduals(SelectedApp);
            ShowInfoBar($"Found {ActiveResidualAnalysis.RegistryKeysFound.Count} registry keys & {ActiveResidualAnalysis.DirectoriesFound.Count} residual directories ({FormatBytes(ActiveResidualAnalysis.TotalResidualSizeBytes)}).", "Info");
            StatusMessage = "Residual scan completed.";
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Residual scan error: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteSimulateUninstallAsync()
    {
        if (SelectedApp == null) return;

        IsBusy = true;
        StatusMessage = $"Simulating uninstall for {SelectedApp.DisplayName}...";

        try
        {
            var result = await _uninstallerService.UninstallAppAsync(SelectedApp, quiet: true, dryRun: true);
            ShowInfoBar(result.Message, "Warning");
            StatusMessage = "Simulation complete.";
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Uninstall simulation error: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteLiveUninstallAsync()
    {
        if (SelectedApp == null) return;

        // Explicit user confirmation required
        var result = MessageBox.Show(
            $"⚠️ UNINSTALL SOFTWARE CONFIRMATION:\n\nAre you sure you want to uninstall:\n'{SelectedApp.DisplayName}'\n(Version: {SelectedApp.DisplayVersion}, Publisher: {SelectedApp.Publisher})?\n\nThis will execute the software uninstaller on your workstation.",
            "Confirm Software Uninstallation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            ShowInfoBar("Uninstallation cancelled by user.", "Info");
            return;
        }

        IsBusy = true;
        StatusMessage = $"Launching uninstaller for {SelectedApp.DisplayName}...";

        try
        {
            var execResult = await _uninstallerService.UninstallAppAsync(SelectedApp, quiet: false, dryRun: false);
            if (execResult.Success)
            {
                ShowInfoBar($"Successfully uninstalled '{SelectedApp.DisplayName}'.", "Success");
                await ExecuteLoadAppsAsync(forceReload: true);
            }
            else
            {
                ShowInfoBar($"Uninstallation did not succeed (Exit code: {execResult.ExitCode}).", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Uninstallation error: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExecuteLoadLeftoversAsync()
    {
        IsLoadingApps = true;
        StatusMessage = "Scanning AppData for residual folders...";

        try
        {
            var appsCache = InstalledApps.Count > 0 ? InstalledApps.ToList() : null;
            var list = await Task.Run(() => _uninstallerService.DetectLeftoverFolders(existingApps: appsCache));
            LeftoverFolders.ReplaceRange(list);

            TotalLeftoverCount = LeftoverFolders.Count;
            TotalLeftoverSize = LeftoverFolders.Sum(l => l.EstimatedSizeBytes);
            TotalLeftoverSizeFormatted = FormatBytes(TotalLeftoverSize);

            ShowInfoBar($"Discovered {LeftoverFolders.Count} leftover folders totaling {TotalLeftoverSizeFormatted}.", "Warning");
            StatusMessage = $"Leftover folders scan complete ({LeftoverFolders.Count} found).";
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Leftover folders scan error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoadingApps = false;
        }
    }

    public async Task ExecuteRemoveBrokenEntryAsync()
    {
        if (SelectedApp == null || !SelectedApp.IsBroken) return;

        var result = MessageBox.Show(
            $"⚠️ REMOVE BROKEN ENTRY:\n\nDo you want to remove the invalid uninstall entry for:\n'{SelectedApp.DisplayName}' from Windows Registry?\n\nThis will safely clean up the entry from Add/Remove Programs.",
            "Confirm Broken Entry Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            bool removed = _uninstallerService.RemoveBrokenUninstallEntry(SelectedApp, dryRun: false);
            if (removed)
            {
                ShowInfoBar($"Removed broken entry '{SelectedApp.DisplayName}' from Windows Registry.", "Success");
                await ExecuteLoadAppsAsync(forceReload: true);
            }
            else
            {
                ShowInfoBar("Could not remove the entry. Elevated administrative rights may be required.", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Error removing entry: {ex.Message}", "Error");
        }
    }

    public void ExecuteOpenLeftoverFolder(AppLeftoverFolder? folder)
    {
        if (folder != null && System.IO.Directory.Exists(folder.FolderPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folder.FolderPath)
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    public async Task ExecutePurgeLeftoverAsync(AppLeftoverFolder? folder)
    {
        if (folder == null) return;

        var result = MessageBox.Show(
            $"⚠️ PERMANENT RESIDUAL DELETION:\n\nAre you sure you want to permanently delete residual folder:\n'{folder.FolderPath}'\n({FormatBytes(folder.EstimatedSizeBytes)}, {folder.FileCount} files)?\n\nThis action cannot be undone!",
            "Confirm Residual Purge",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            bool purged = await Task.Run(() => _uninstallerService.PurgeLeftoverFolder(folder, dryRun: false));
            if (purged)
            {
                LeftoverFolders.Remove(folder);
                TotalLeftoverCount = LeftoverFolders.Count;
                TotalLeftoverSize = LeftoverFolders.Sum(l => l.EstimatedSizeBytes);
                TotalLeftoverSizeFormatted = FormatBytes(TotalLeftoverSize);
                ShowInfoBar($"Purged residual folder '{folder.FolderName}' ({FormatBytes(folder.EstimatedSizeBytes)} reclaimed).", "Success");
            }
            else
            {
                ShowInfoBar($"Could not delete folder. File may be locked by a running process.", "Error");
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Purge error: {ex.Message}", "Error");
        }
    }

    private void ShowInfoBar(string message, string severity)
    {
        InfoBarMessage = message;
        InfoBarSeverity = severity;
        IsInfoBarVisible = true;
    }

    private async Task ExecuteScanLargeFilesAsync()
    {
        IsBusy = true;
        StatusMessage = "Hunting for heavy space hogs (> 100MB)...";
        try
        {
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var results = await _largeFileService.ScanLargeFilesAsync(userDir, 100 * 1024 * 1024, 50);
            LargeFiles.ReplaceRange(results);
            ApplyLargeFileFilter();
            StatusMessage = $"Discovered {LargeFiles.Count} large space hogs in user profile.";
            ShowInfoBar($"Found {LargeFiles.Count} large files (> 100MB) in your user directory.", "Info");
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Large files scan error: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyRuleCategoryFilter()
    {
        var q = Rules.AsEnumerable();
        if (SelectedRuleCategory == "System")
            q = q.Where(r => r.Category == CleanCategory.System || r.Category == CleanCategory.DeepSystem);
        else if (SelectedRuleCategory == "Browsers")
            q = q.Where(r => r.Category == CleanCategory.Browser);
        else if (SelectedRuleCategory == "Development")
            q = q.Where(r => r.Category == CleanCategory.Developer);

        FilteredRules.ReplaceRange(q);
    }

    public void ApplyRulePreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) return;

        foreach (var rule in Rules)
        {
            if (preset == "Safe")
            {
                rule.IsSelected = rule.RiskLevel == CleanRiskLevel.Safe;
            }
            else if (preset == "Dev")
            {
                rule.IsSelected = rule.Category == CleanCategory.Developer;
            }
            else if (preset == "All")
            {
                rule.IsSelected = true;
            }
        }
        UpdateTotals();
        ShowInfoBar($"Applied '{preset}' preset. {Rules.Count(r => r.IsSelected)} cleanup categories selected.", "Info");
    }

    public async Task ExecuteFixRecommendationAsync(CleanRecommendation? rec)
    {
        if (rec == null) return;

        var result = MessageBox.Show(
            $"⚡ QUICK REMEDY CONFIRMATION:\n\nRecommendation: {rec.Title}\nAction: {rec.Description}\nPotential Savings: {FormatBytes(rec.EstimatedSavingsBytes)}\n\nDo you want ZeroClean to scan and resolve this issue now?",
            "Apply Health Recommendation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        foreach (var r in Rules)
        {
            if (rec.ActionCategory.Contains("Dev", StringComparison.OrdinalIgnoreCase))
                r.IsSelected = r.Category == CleanCategory.Developer;
            else if (rec.ActionCategory.Contains("Browser", StringComparison.OrdinalIgnoreCase))
                r.IsSelected = r.Category == CleanCategory.Browser;
            else
                r.IsSelected = r.RiskLevel == CleanRiskLevel.Safe;
        }

        await ExecuteScanAsync();
        if (TotalReclaimableBytes > 0)
        {
            await ExecuteLiveCleanAsync();
        }
        await ExecuteAdvisorAsync();
    }

    public void ApplyLargeFileFilter()
    {
        var q = LargeFiles.AsEnumerable();
        if (SelectedLargeFileType == "Media")
        {
            var exts = new HashSet<string>(new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".mp3", ".wav" }, StringComparer.OrdinalIgnoreCase);
            q = q.Where(f => exts.Contains(System.IO.Path.GetExtension(f.FullPath)));
        }
        else if (SelectedLargeFileType == "DiskImages")
        {
            var exts = new HashSet<string>(new[] { ".iso", ".vhd", ".vhdx", ".vmdk", ".img" }, StringComparer.OrdinalIgnoreCase);
            q = q.Where(f => exts.Contains(System.IO.Path.GetExtension(f.FullPath)));
        }
        else if (SelectedLargeFileType == "Archives")
        {
            var exts = new HashSet<string>(new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }, StringComparer.OrdinalIgnoreCase);
            q = q.Where(f => exts.Contains(System.IO.Path.GetExtension(f.FullPath)));
        }
        else if (SelectedLargeFileType == "Installers")
        {
            var exts = new HashSet<string>(new[] { ".exe", ".msi" }, StringComparer.OrdinalIgnoreCase);
            q = q.Where(f => exts.Contains(System.IO.Path.GetExtension(f.FullPath)));
        }

        FilteredLargeFiles.ReplaceRange(q);
    }

    public void ExecuteOpenLargeFileFolder(LargeFileInfo? file)
    {
        if (file == null || !System.IO.File.Exists(file.FullPath)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file.FullPath}\"");
        }
        catch { }
    }

    public async Task ExecuteDeleteLargeFileAsync(LargeFileInfo? file)
    {
        if (file == null || !System.IO.File.Exists(file.FullPath)) return;

        var res = MessageBox.Show(
            $"⚠️ PERMANENT LARGE FILE DELETION:\n\nAre you sure you want to permanently delete:\n'{file.FullPath}'\n({FormatBytes(file.SizeBytes)})?\n\nThis deletion cannot be undone!",
            "Confirm Large File Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (res != MessageBoxResult.Yes) return;

        try
        {
            await Task.Run(() => System.IO.File.Delete(file.FullPath));
            LargeFiles.Remove(file);
            ApplyLargeFileFilter();
            ShowInfoBar($"Deleted '{file.FileName}' ({FormatBytes(file.SizeBytes)} reclaimed).", "Success");
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Failed to delete file: {ex.Message}", "Error");
        }
    }

    public async Task ExecuteQuickOptimizeAsync()
    {
        ApplyRulePreset("Safe");
        await ExecuteScanAsync();
        if (TotalReclaimableBytes > 0)
        {
            await ExecuteLiveCleanAsync();
        }
        else
        {
            ShowInfoBar("System is already in pristine condition. No clutter found.", "Success");
        }
    }

    private async Task ExecuteScanWslDisksAsync()
    {
        IsBusy = true;
        StatusMessage = "Discovering WSL2 & Docker virtual disks (ext4.vhdx)...";
        try
        {
            WslDisks.Clear();
            var disks = await _dockerWslService.DiscoverWslVdisksAsync();
            foreach (var d in disks)
            {
                WslDisks.Add(d);
            }
            StatusMessage = WslDisks.Count > 0 
                ? $"Found {WslDisks.Count} WSL2/Docker virtual disk(s)." 
                : "No WSL2 or Docker ext4.vhdx virtual disks detected.";
            ShowInfoBar(WslDisks.Count > 0 
                ? $"Detected {WslDisks.Count} virtual disk(s) available for diskpart compaction."
                : "No virtual disks found in default locations.", "Info");
        }
        catch (Exception ex)
        {
            ShowInfoBar($"WSL discovery error: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteAnalyzeDismAsync()
    {
        IsBusy = true;
        StatusMessage = "Analyzing Windows Component Store (WinSxS) via DISM...";
        try
        {
            DismReport = await _dismService.AnalyzeComponentStoreAsync();
            StatusMessage = DismReport.IsCleanupRecommended 
                ? "WinSxS Component Store cleanup is RECOMMENDED." 
                : "WinSxS Component Store analysis completed.";
            ShowInfoBar(DismReport.IsCleanupRecommended
                ? "WinSxS cleanup recommended! Superseded packages detected."
                : "Component store analysis finished. Store is clean.", "Success");
        }
        catch (Exception ex)
        {
            ShowInfoBar($"DISM analysis error: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public static string FormatBytes(long bytes)
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
