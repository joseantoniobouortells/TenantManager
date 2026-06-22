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
        if (e.Source is TabControl && DataContext is MainViewModel vm)
        {
            vm.Dashboard.Refresh();
        }
    }
}
