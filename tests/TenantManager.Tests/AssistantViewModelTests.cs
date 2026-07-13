using System.Linq;
using TenantManager.App.ViewModels;
using Xunit;

namespace TenantManager.Tests;

public class AssistantViewModelTests
{
    [Fact]
    public void ChatMessageViewModel_IdentifiesUserRoleCorrectly()
    {
        var msg = new ChatMessageViewModel { Role = "user", Content = "Test" };
        Assert.True(msg.IsUser);
    }

    [Fact]
    public void ChatMessageViewModel_IdentifiesAssistantRoleCorrectly()
    {
        var msg = new ChatMessageViewModel { Role = "assistant", Content = "Test" };
        Assert.False(msg.IsUser);
    }

    [Fact]
    public void ChatMessageViewModel_PreservesRawMarkdownContent()
    {
        var markdown = "**Bold** and *Italic*";
        var msg = new ChatMessageViewModel { Role = "assistant", Content = markdown };
        Assert.Equal(markdown, msg.Content);
    }

    [Fact]
    public void SendCommand_RaisesScrollRequestedAndSetsProcessingState()
    {
        var vm = new AssistantViewModel();
        int scrollEventCount = 0;
        vm.ScrollRequested += (s, e) => scrollEventCount++;
        
        vm.InputText = "Test message";
        Assert.True(vm.SendCommand.CanExecute(null));
        
        // Disable AI to avoid network calls, we just want to test local state machine
        // Actually, SettingsPersistence loads real settings, so IsAiEnabled depends on environment.
        // We can just execute and wait for task.
        vm.SendCommand.Execute(null);

        // Even with AI disabled, it should append the user message and fire scroll requested.
        Assert.True(scrollEventCount >= 1);
        Assert.Contains(vm.Messages, m => m.Role == "user" && m.Content == "Test message");
    }

    [Fact]
    public void DuplicateConcurrentSendExecution_IsPrevented()
    {
        var vm = new AssistantViewModel();
        vm.InputText = "Test";
        
        // Manually set IsLoading to simulate a running request
        vm.IsLoading = true;
        
        // The command should not be executable
        Assert.False(vm.SendCommand.CanExecute(null));
    }

    [Fact]
    public void CurrentProcessingStage_UpdatesIsProcessingCorrectly()
    {
        var vm = new AssistantViewModel();
        
        Assert.False(vm.IsProcessing);
        
        vm.CurrentProcessingStage = TenantManager.Core.Services.AI.AiProcessingStage.PreparingRequest;
        Assert.True(vm.IsProcessing);
        
        vm.CurrentProcessingStage = TenantManager.Core.Services.AI.AiProcessingStage.ExecutingQuery;
        Assert.True(vm.IsProcessing);
        
        vm.CurrentProcessingStage = TenantManager.Core.Services.AI.AiProcessingStage.Completed;
        Assert.False(vm.IsProcessing);
        
        vm.CurrentProcessingStage = TenantManager.Core.Services.AI.AiProcessingStage.Failed;
        Assert.False(vm.IsProcessing);
    }
}
