using System;
using System.IO;
using System.Text.Json;

namespace TenantManager.App.Data;

public static class SettingsPersistence
{
    private static readonly string LogPath = "/Users/joseantoniobouortells/Developer/TenantManager/settings_debug.log";

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    public static string? SettingsFilePath { get; set; }

    public static AppSettings LoadSettings()
    {
        Log("LoadSettings() started");
        try
        {
            var path = SettingsFilePath ?? "settings.json";
            Log($"Settings file path: {path}");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                Log($"Read JSON: {json}");
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                Log($"Deserialized settings - Theme: {settings?.Theme}, Language: {settings?.Language}");
                return settings ?? new AppSettings();
            }
            else
            {
                Log("Settings file does not exist, returning defaults");
            }
        }
        catch (Exception ex)
        {
            Log($"Error loading settings: {ex.Message}\n{ex.StackTrace}");
        }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        Log($"SaveSettings() called - Theme: {settings.Theme}, Language: {settings.Language}");
        try
        {
            var path = SettingsFilePath ?? "settings.json";
            var directory = Path.GetDirectoryName(path);
            Log($"Target directory: {directory}");
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log("Created target directory");
            }
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            Log($"Successfully wrote settings to {path}");
        }
        catch (Exception ex)
        {
            Log($"Error saving settings: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
