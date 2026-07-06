using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TenantManager.App.Services;

public static class NativeNotificationService
{
    public static void ShowNotification(string title, string message)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ShowWindowsNotification(title, message);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ShowMacNotification(title, message);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ShowLinuxNotification(title, message);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing notification: {ex.Message}");
        }
    }

    private static void ShowWindowsNotification(string title, string message)
    {
        var escapedTitle = title.Replace("\"", "'");
        var escapedMessage = message.Replace("\"", "'");
        
        var psScript = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null;
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null;
$ToastXml = [Windows.Data.Xml.Dom.XmlDocument]::new();
$ToastXml.LoadXml('<toast><visual><binding template=""ToastGeneric""><text>{escapedTitle}</text><text>{escapedMessage}</text></binding></visual></toast>');
$AppId = '{{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}}\WindowsPowerShell\v1.0\powershell.exe';
$Toast = [Windows.UI.Notifications.ToastNotification]::new($ToastXml);
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($AppId).Show($Toast);
";
        var base64Script = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {base64Script}",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void ShowMacNotification(string title, string message)
    {
        var escapedTitle = title.Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\"", "\\\"");
        var script = $"display notification \"{escapedMessage}\" with title \"{escapedTitle}\"";
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e '{script}'",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void ShowLinuxNotification(string title, string message)
    {
        var escapedTitle = title.Replace("\"", "\\\"");
        var escapedMessage = message.Replace("\"", "\\\"");
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "notify-send",
            Arguments = $"\"{escapedTitle}\" \"{escapedMessage}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}

