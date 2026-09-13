using System.Windows;
using CleanTool.UI.ViewModels;

namespace CleanTool.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TabCleaner.IsChecked = true;
    }

    private void OnCleanerTabClicked(object sender, RoutedEventArgs e)
    {
        SetVisibleView(0);
    }

    private void OnAdvisorTabClicked(object sender, RoutedEventArgs e)
    {
        SetVisibleView(1);
        if (DataContext is MainViewModel vm && vm.Recommendations.Count == 0)
        {
            vm.RunAdvisorCommand.Execute(null);
        }
    }

    private void OnUninstallerTabClicked(object sender, RoutedEventArgs e)
    {
        SetVisibleView(2);
        if (DataContext is MainViewModel vm && vm.InstalledApps.Count == 0)
        {
            vm.LoadAppsCommand.Execute(null);
        }
    }

    private void OnAnalyzerTabClicked(object sender, RoutedEventArgs e)
    {
        SetVisibleView(3);
        if (DataContext is MainViewModel vm)
        {
            vm.LoadDrives();
        }
    }

    private void SetVisibleView(int tabIndex)
    {
        ViewCleaner.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        ViewAdvisor.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        ViewUninstaller.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        ViewAnalyzer.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        if (DataContext is MainViewModel vm)
        {
            vm.SelectedTabIndex = tabIndex;
        }
    }
}
