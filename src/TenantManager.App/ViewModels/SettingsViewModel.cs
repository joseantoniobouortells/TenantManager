using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using TenantManager.App.Data;
using TenantManager.Core.Services.AI;

namespace TenantManager.App.ViewModels;

public class LanguageOption
{
    public string Code { get; }
    public string Name { get; }

    public LanguageOption(string code, string name)
    {
        Code = code;
        Name = name;
    }
}

public class SettingsViewModel : ViewModelBase
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

    private bool _isLight;
    private bool _isDark;
    private bool _isSystem;
    private LanguageOption _selectedLanguage;

    public List<LanguageOption> SupportedLanguages { get; } = new()
    {
        new LanguageOption("en", "English"),
        new LanguageOption("es", "Español")
    };

    private bool _isAiEnabled;
    private string _aiEndpoint = "";
    private string _aiModelName = "";
    private string? _selectedAiModel;
    private bool _isLoadingModels;
    private string _modelsStatusMessage = "";

    public ObservableCollection<string> AvailableModels { get; } = new();

    public string? SelectedAiModel
    {
        get => _selectedAiModel;
        set
        {
            if (SetProperty(ref _selectedAiModel, value) && value != null)
            {
                _aiModelName = value;
                OnPropertyChanged(nameof(AiModelName));
                SaveCurrentSettings();
            }
        }
    }

    public bool IsLoadingModels
    {
        get => _isLoadingModels;
        set => SetProperty(ref _isLoadingModels, value);
    }

    public string ModelsStatusMessage
    {
        get => _modelsStatusMessage;
        set => SetProperty(ref _modelsStatusMessage, value);
    }

    public ICommand LoadModelsCommand { get; }

    public SettingsViewModel()
    {
        Log("SettingsViewModel constructor started");
        var settings = SettingsPersistence.LoadSettings();

        // 1. Initialize Theme
        Log($"Initializing Theme to {settings.Theme}");
        if (settings.Theme == "Light")
        {
            _isLight = true;
            ApplyTheme(ThemeVariant.Light);
        }
        else if (settings.Theme == "Dark")
        {
            _isDark = true;
            ApplyTheme(ThemeVariant.Dark);
        }
        else
        {
            _isSystem = true;
            ApplyTheme(ThemeVariant.Default);
        }

        // 2. Initialize Language
        Log($"Initializing Language to {settings.Language}");
        var lang = SupportedLanguages.Find(l => l.Code == settings.Language) ?? SupportedLanguages[0];
        _selectedLanguage = lang;
        ApplyLanguage(lang.Code);
        
        // 3. Initialize AI Settings
        _isAiEnabled = settings.IsAiEnabled;
        _aiEndpoint = settings.AiEndpoint;
        _aiModelName = settings.AiModelName;
        _selectedAiModel = settings.AiModelName;

        LoadModelsCommand = new RelayCommand(
            _ => { _ = LoadModelsAsync(); },
            _ => !IsLoadingModels && IsAiEnabled && !string.IsNullOrWhiteSpace(AiEndpoint)
        );
    }

    public string DbPath => DatabasePath.FullPath;

    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.2";
        }
    }

    public bool IsLight
    {
        get => _isLight;
        set
        {
            if (SetProperty(ref _isLight, value) && value)
            {
                Log("IsLight set to true");
                ApplyTheme(ThemeVariant.Light);
                SaveCurrentSettings();
            }
        }
    }

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (SetProperty(ref _isDark, value) && value)
            {
                Log("IsDark set to true");
                ApplyTheme(ThemeVariant.Dark);
                SaveCurrentSettings();
            }
        }
    }

    public bool IsSystem
    {
        get => _isSystem;
        set
        {
            if (SetProperty(ref _isSystem, value) && value)
            {
                Log("IsSystem set to true");
                ApplyTheme(ThemeVariant.Default);
                SaveCurrentSettings();
            }
        }
    }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value) && value != null)
            {
                Log($"SelectedLanguage set to {value.Code}");
                ApplyLanguage(value.Code);
                SaveCurrentSettings();
            }
        }
    }

    public bool IsAiEnabled
    {
        get => _isAiEnabled;
        set
        {
            if (SetProperty(ref _isAiEnabled, value))
                SaveCurrentSettings();
        }
    }

    public string AiEndpoint
    {
        get => _aiEndpoint;
        set
        {
            if (SetProperty(ref _aiEndpoint, value))
            {
                SaveCurrentSettings();
                (LoadModelsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string AiModelName
    {
        get => _aiModelName;
        set
        {
            if (SetProperty(ref _aiModelName, value))
            {
                _selectedAiModel = value;
                OnPropertyChanged(nameof(SelectedAiModel));
                SaveCurrentSettings();
            }
        }
    }

    private async Task LoadModelsAsync()
    {
        IsLoadingModels = true;
        ModelsStatusMessage = "";
        (LoadModelsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        try
        {
            var client = new LocalAiClient();
            var models = await client.GetAvailableModelsAsync();
            AvailableModels.Clear();
            if (models.Count == 0)
            {
                ModelsStatusMessage = "No models found. Check endpoint.";
            }
            else
            {
                foreach (var m in models)
                    AvailableModels.Add(m);

                // Auto-select the currently saved model if it exists in the list,
                // otherwise select the first available one.
                if (!string.IsNullOrWhiteSpace(_aiModelName) && AvailableModels.Contains(_aiModelName))
                    SelectedAiModel = _aiModelName;
                else
                    SelectedAiModel = AvailableModels[0];

                ModelsStatusMessage = $"{models.Count} model(s) loaded.";
            }
        }
        catch
        {
            ModelsStatusMessage = "Error connecting to server.";
        }
        finally
        {
            IsLoadingModels = false;
            (LoadModelsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void SaveCurrentSettings()
    {
        var themeStr = "Default";
        if (IsLight) themeStr = "Light";
        else if (IsDark) themeStr = "Dark";

        Log($"SaveCurrentSettings - Theme: {themeStr}, Language: {SelectedLanguage?.Code}");
        var settings = new AppSettings
        {
            Theme = themeStr,
            Language = SelectedLanguage?.Code ?? "en",
            IsAiEnabled = this.IsAiEnabled,
            AiEndpoint = this.AiEndpoint,
            AiModelName = this.AiModelName
        };
        SettingsPersistence.SaveSettings(settings);
    }

    private void ApplyTheme(ThemeVariant theme)
    {
        Log($"ApplyTheme called with {theme}");
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = theme;
            Log("Applied theme to Application.Current");
        }
        else
        {
            Log("Warning: Application.Current is null in ApplyTheme");
        }
    }

    private void ApplyLanguage(string langCode)
    {
        Log($"ApplyLanguage called with {langCode}");
        if (Application.Current == null)
        {
            Log("Warning: Application.Current is null in ApplyLanguage");
            return;
        }
        
        var dicts = Application.Current.Resources.MergedDictionaries;
        Log($"MergedDictionaries count: {dicts.Count}");
        
        bool replaced = false;

        // App.axaml wraps resources in an inner ResourceDictionary
        foreach (var dict in dicts)
        {
            if (dict is ResourceDictionary rd)
            {
                for (int i = 0; i < rd.MergedDictionaries.Count; i++)
                {
                    if (rd.MergedDictionaries[i] is Avalonia.Markup.Xaml.Styling.ResourceInclude ri)
                    {
                        var sourceStr = ri.Source?.OriginalString ?? "null";
                        if (sourceStr.Contains("i18n") || sourceStr.EndsWith("en.axaml") || sourceStr.EndsWith("es.axaml"))
                        {
                            Log($"Matching dictionary found in inner ResourceDictionary at index {i}. Replacing with {langCode}.axaml");
                            rd.MergedDictionaries[i] = new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://TenantManager.App/App.axaml"))
                            {
                                Source = new Uri($"avares://TenantManager.App/Assets/i18n/{langCode}.axaml")
                            };
                            replaced = true;
                            break;
                        }
                    }
                }
            }
            if (replaced) break;
        }

        if (!replaced)
        {
            for (int i = 0; i < dicts.Count; i++)
            {
                var dict = dicts[i];
                Log($"Dict {i} type: {dict.GetType().FullName}");
                
                if (dict is Avalonia.Markup.Xaml.Styling.ResourceInclude ri)
                {
                    var sourceStr = ri.Source?.OriginalString ?? "null";
                    Log($"ResourceInclude Source: {sourceStr}");
                    
                    if (sourceStr.Contains("i18n") || sourceStr.EndsWith("en.axaml") || sourceStr.EndsWith("es.axaml"))
                    {
                        Log($"Matching dictionary found at index {i}. Replacing with {langCode}.axaml");
                        var newRi = new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://TenantManager.App/App.axaml"))
                        {
                            Source = new Uri($"avares://TenantManager.App/Assets/i18n/{langCode}.axaml")
                        };
                        dicts[i] = newRi;
                        replaced = true;
                        Log("Replacement successful");
                        break;
                    }
                }
            }
        }

        if (!replaced)
        {
            Log("Warning: No matching dictionary found in MergedDictionaries, adding as new");
            var newRi = new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://TenantManager.App/App.axaml"))
            {
                Source = new Uri($"avares://TenantManager.App/Assets/i18n/{langCode}.axaml")
            };
            dicts.Add(newRi);
            Log("Added dictionary as new merged dictionary successfully");
        }
    }
}
