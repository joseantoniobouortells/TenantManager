using Avalonia.Controls;
using Avalonia.Input;
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

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
