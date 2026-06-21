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
}
