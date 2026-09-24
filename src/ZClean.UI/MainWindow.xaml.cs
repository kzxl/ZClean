using System.Windows;
using ZClean.UI.ViewModels;
using ZeroUI.Wpf.Navigation;

namespace ZClean.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetupSideNavigation();
    }

    private void SetupSideNavigation()
    {
        MainNav.Items.Clear();
        MainNav.Items.Add(new SideNavItem("cleaner", "System Cleaner", "⚡", "CLEANUP"));
        MainNav.Items.Add(new SideNavItem("advisor", "Health Advisor", "🎯", "DIAGNOSTICS"));
        MainNav.Items.Add(new SideNavItem("uninstaller", "Software Manager", "📦", "APPLICATIONS"));
        MainNav.Items.Add(new SideNavItem("sentinel", "Storage Sentinel", "📊", "STORAGE"));
        MainNav.Items.Add(new SideNavItem("tools", "Advanced Utilities", "🛠️", "SYSTEM TOOLS"));

        MainNav.ItemSelected += (s, e) =>
        {
            SwitchSection(e.Index);
        };

        SwitchSection(0);
    }

    private void SwitchSection(int index)
    {
        ViewCleaner.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewAdvisor.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewUninstaller.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewAnalyzer.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        ViewTools.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

        if (DataContext is MainViewModel vm)
        {
            vm.SelectedTabIndex = index;
            (vm.ActiveSectionTitle, vm.ActiveSectionSubtitle) = index switch
            {
                0 => ("⚡ System Cleaner", "Sweeps junk files, dev caches, crash dumps, and browser cache data."),
                1 => ("🎯 Health Advisor", "Evaluates system clutter, orphaned startup programs, and generates health scores."),
                2 => ("📦 Software Manager", "Uninstalls software and traces post-uninstall registry & file residuals."),
                3 => ("📊 Storage Sentinel", "Real-time disk margin watchdog monitoring partitions against low free thresholds."),
                4 => ("🛠️ Advanced Utilities", "Deep tools: Large file hunter, WinSxS Component Store analysis & WSL2 compaction."),
                _ => ("⚡ System Cleaner", "System optimization suite.")
            };

            if (index == 1 && vm.Recommendations.Count == 0)
            {
                vm.RunAdvisorCommand.Execute(null);
            }
            else if (index == 2 && vm.InstalledApps.Count == 0)
            {
                _ = vm.ExecuteLoadAppsAsync();
            }
            else if (index == 3)
            {
                vm.LoadDrives();
            }
            else if (index == 4 && vm.LargeFiles.Count == 0)
            {
                vm.ScanLargeFilesCommand.Execute(null);
            }
        }
    }
}
