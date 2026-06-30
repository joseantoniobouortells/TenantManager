using Avalonia.Controls;
using TenantManager.App.ViewModels;

namespace TenantManager.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void TabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl tc && DataContext is MainViewModel vm)
        {
            if (tc.SelectedItem is TabItem tabItem && tabItem.Header != null)
            {
                vm.CurrentPageTitle = tabItem.Header.ToString() ?? "Dashboard";
            }
            vm.RefreshAll();
        }
    }
}
