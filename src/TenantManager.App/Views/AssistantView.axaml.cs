using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

    private async void CopyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChatMessageViewModel msg })
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                try
                {
                    await topLevel.Clipboard.SetTextAsync(msg.Content);
                    msg.ShowCopiedMessage = true;
                    await System.Threading.Tasks.Task.Delay(2000);
                    msg.ShowCopiedMessage = false;
                }
                catch { }
            }
        }
    }
}
