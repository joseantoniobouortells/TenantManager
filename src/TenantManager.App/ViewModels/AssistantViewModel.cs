using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using TenantManager.App.Data;
using TenantManager.Core.Services.AI;

namespace TenantManager.App.ViewModels;

public class ChatMessageViewModel : ViewModelBase
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsUser => Role == "user";
}

public class AssistantViewModel : ViewModelBase
{
    private string _inputText = string.Empty;
    private bool _isLoading;
    
    // AI Services
    private readonly AiQueryService _queryService;
    private readonly LocalAiClient _aiClient;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    public string InputText
    {
        get => _inputText;
        set 
        {
            if (SetProperty(ref _inputText, value))
            {
                (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set 
        {
            if (SetProperty(ref _isLoading, value))
            {
                (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAiEnabled
    {
        get
        {
            var settings = SettingsPersistence.LoadSettings();
            return settings.IsAiEnabled;
        }
    }

    public ICommand SendCommand { get; }

    public AssistantViewModel()
    {
        // For DI, in a real app these would be injected. We instantiate directly for now.
        _queryService = new AiQueryService(new AppDbContext());
        _aiClient = new LocalAiClient();

        SendCommand = new RelayCommand(
            _ => { _ = SendMessageAsync(); },
            _ => !IsLoading && !string.IsNullOrWhiteSpace(InputText)
        );
    }

    public void RefreshSettings()
    {
        OnPropertyChanged(nameof(IsAiEnabled));
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userText = InputText;
        InputText = string.Empty;

        Messages.Add(new ChatMessageViewModel { Role = "user", Content = userText });
        
        if (!IsAiEnabled)
        {
            Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = "AI Assistant is disabled." });
            return;
        }

        IsLoading = true;

        try
        {
            // 1. Deterministic Intent & Data Resolution
            var contextDataStr = await _queryService.ResolveIntentAndGetDataAsync(userText);

            string finalResponse;

            if (string.IsNullOrWhiteSpace(contextDataStr))
            {
                // Fallback for unhandled intents
                finalResponse = "I can only answer specific questions about the data, such as a tenant's move-out date.";
            }
            else
            {
                // 2. Build Safe Prompt
                var systemPrompt = SafeContextBuilder.BuildSystemPrompt(contextDataStr);

                // 3. Request LLM Completion
                finalResponse = await _aiClient.SendChatCompletionAsync(systemPrompt, userText);
            }

            Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = finalResponse });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = $"Error processing request: {ex.Message}" });
        }
        finally
        {
            IsLoading = false;
        }
    }
}
