using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using TenantManager.App.ViewModels;

namespace TenantManager.App.Views;

public partial class AssistantView : UserControl
{
    private AssistantViewModel? _vm;
    private bool _autoScrollEnabled = true;

    public AssistantView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (ChatScrollViewer != null)
        {
            ChatScrollViewer.ScrollChanged += ChatScrollViewer_ScrollChanged;
        }
        
        var itemsControl = this.FindControl<ItemsControl>("ChatItemsControl");
        if (itemsControl != null)
        {
            itemsControl.SizeChanged += ChatItemsControl_SizeChanged;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (ChatScrollViewer != null)
        {
            ChatScrollViewer.ScrollChanged -= ChatScrollViewer_ScrollChanged;
        }
        
        var itemsControl = this.FindControl<ItemsControl>("ChatItemsControl");
        if (itemsControl != null)
        {
            itemsControl.SizeChanged -= ChatItemsControl_SizeChanged;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.ScrollRequested -= OnScrollRequested;
        _vm = DataContext as AssistantViewModel;
        if (_vm != null) _vm.ScrollRequested += OnScrollRequested;
    }

    private void OnScrollRequested(object? sender, EventArgs e)
    {
        _autoScrollEnabled = true;
        RequestScroll();
    }

    private void ChatItemsControl_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_autoScrollEnabled)
        {
            RequestScroll();
        }
    }

    private void ChatScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Disable auto-scroll if user manually scrolled up
        if (e.ExtentDelta.Y == 0 && e.ViewportDelta.Y == 0 && e.OffsetDelta.Y < 0)
        {
            var isAtBottom = ChatScrollViewer.Offset.Y >= (ChatScrollViewer.Extent.Height - ChatScrollViewer.Viewport.Height - 10);
            if (!isAtBottom)
            {
                _autoScrollEnabled = false;
            }
        }
        else if (e.ExtentDelta.Y == 0 && e.ViewportDelta.Y == 0 && e.OffsetDelta.Y > 0)
        {
            var isAtBottom = ChatScrollViewer.Offset.Y >= (ChatScrollViewer.Extent.Height - ChatScrollViewer.Viewport.Height - 10);
            if (isAtBottom)
            {
                _autoScrollEnabled = true;
            }
        }
    }

    private void RequestScroll()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ChatScrollViewer?.ScrollToEnd();
        }, Avalonia.Threading.DispatcherPriority.Background);
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
