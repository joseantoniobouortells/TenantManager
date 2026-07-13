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
}
