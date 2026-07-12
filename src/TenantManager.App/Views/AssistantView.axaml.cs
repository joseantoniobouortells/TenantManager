using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TenantManager.App.ViewModels;

namespace TenantManager.App.Views;

public partial class AssistantView : UserControl
{
    public AssistantView()
    {
        InitializeComponent();
    }

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers != KeyModifiers.Shift)
        {
            e.Handled = true;
            if (DataContext is AssistantViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
        }
    }
}
