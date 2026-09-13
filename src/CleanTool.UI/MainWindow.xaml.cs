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
        ViewCleaner.Visibility = Visibility.Visible;
        ViewAnalyzer.Visibility = Visibility.Collapsed;
        if (DataContext is MainViewModel vm)
        {
            vm.SelectedTabIndex = 0;
        }
    }

    private void OnAnalyzerTabClicked(object sender, RoutedEventArgs e)
    {
        ViewCleaner.Visibility = Visibility.Collapsed;
        ViewAnalyzer.Visibility = Visibility.Visible;
        if (DataContext is MainViewModel vm)
        {
            vm.SelectedTabIndex = 1;
            vm.LoadDrives();
        }
    }
}
