using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TenantManager.App.Services;

public static class NativeNotificationService
{
    public static Action? OnNotificationClicked;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void NotificationClickedCallback();

    private static NotificationClickedCallback? _macCallbackDelegate;

    [DllImport("MacNotifier", CallingConvention = CallingConvention.Cdecl)]
    private static extern void show_mac_notification(string title, string body, string actionButtonTitle);

    [DllImport("MacNotifier", CallingConvention = CallingConvention.Cdecl)]
    private static extern void init_mac_notifier(NotificationClickedCallback callback);

    public static void Initialize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Keep the delegate alive to prevent GC collection
            _macCallbackDelegate = () => 
            {
                OnNotificationClicked?.Invoke();
            };
            
            try
            {
                init_mac_notifier(_macCallbackDelegate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NOTIFICATIONS] Failed to init mac notifier callback: {ex.Message}");
            }
        }
    }

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
                Console.WriteLine($"[NOTIF] Calling show_mac_notification: {title}");
                string btnStr = "Show";
                if (Avalonia.Application.Current != null && 
                    Avalonia.Application.Current.TryGetResource("NotificationActionButton", Avalonia.Styling.ThemeVariant.Default, out var btnObj) &&
                    btnObj is string s)
                {
                    btnStr = s;
                }
                show_mac_notification(title, message, btnStr);
                Console.WriteLine("[NOTIF] show_mac_notification returned OK");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ShowLinuxNotification(title, message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NOTIFICATIONS] Error showing notification: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
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

    private static void ShowLinuxNotification(string title, string message)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "notify-send",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(title);
        startInfo.ArgumentList.Add(message);
        
        Process.Start(startInfo);
    }
}

