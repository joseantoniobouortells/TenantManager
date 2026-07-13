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

    private bool _showCopiedMessage;
    public bool ShowCopiedMessage
    {
        get => _showCopiedMessage;
        set => SetProperty(ref _showCopiedMessage, value);
    }
}

public class AssistantViewModel : ViewModelBase
{
    private string _inputText = string.Empty;
    private bool _isLoading;
    private AiProcessingStage _currentStage = AiProcessingStage.None;

    // AI services
    private readonly AiQueryService _queryService;
    private readonly Func<int> _propertyIdProvider;

    // Conversation state — lives in the session, not persisted to DB
    private readonly AssistantContext _conversationContext = new();

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
                (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public AiProcessingStage CurrentProcessingStage
    {
        get => _currentStage;
        set => SetProperty(ref _currentStage, value);
    }

    public bool IsProcessing => CurrentProcessingStage != AiProcessingStage.None && CurrentProcessingStage != AiProcessingStage.Completed && CurrentProcessingStage != AiProcessingStage.Failed;

    public string ProcessingStageKey => $"Stage_{CurrentProcessingStage}";

    public bool IsAiEnabled
    {
        get
        {
            var settings = SettingsPersistence.LoadSettings();
            return settings.IsAiEnabled;
        }
    }

    public ICommand SendCommand { get; }

    public event EventHandler? ScrollRequested;

    public AssistantViewModel(Func<int>? propertyIdProvider = null)
    {
        _propertyIdProvider = propertyIdProvider ?? (() => 0);
        var aiClient = new LocalAiClient();
        _queryService = new AiQueryService(new AppDbContext(), aiClient);

        // Default conversation language to Spanish (app UI language)
        _conversationContext.LastLanguage = "es";

        SendCommand = new RelayCommand(
            _ => { _ = SendMessageAsync(); },
            _ => !IsLoading && !string.IsNullOrWhiteSpace(InputText)
        );
    }

    public void RefreshSettings()
    {
        OnPropertyChanged(nameof(IsAiEnabled));
    }

    private void UpdateStage(AiProcessingStage stage)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CurrentProcessingStage = stage;
            OnPropertyChanged(nameof(IsProcessing));
            OnPropertyChanged(nameof(ProcessingStageKey));
            if (IsProcessing)
            {
                ScrollRequested?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userText = InputText;
        InputText = string.Empty;

        Messages.Add(new ChatMessageViewModel { Role = "user", Content = userText });
        ScrollRequested?.Invoke(this, EventArgs.Empty);

        // Determine current language from context (default = Spanish per app UI)
        bool isSpanish = _conversationContext.LastLanguage == "es";

        if (!IsAiEnabled)
        {
            Messages.Add(new ChatMessageViewModel
            {
                Role = "assistant",
                Content = isSpanish
                    ? "El asistente de IA está desactivado en la configuración."
                    : "AI Assistant is disabled."
            });
            ScrollRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsLoading = true;
        UpdateStage(AiProcessingStage.PreparingRequest);

        try
        {
            var propertyId = _propertyIdProvider();
            var (finalAnswer, answerIsSpanish) =
                await _queryService.ResolveIntentAndGetDataAsync(userText, _conversationContext, propertyId, UpdateStage);

            // Update conversation language from detected language
            isSpanish = answerIsSpanish;

            string finalResponse;
            if (!string.IsNullOrWhiteSpace(finalAnswer))
            {
                finalResponse = finalAnswer;
            }
            else
            {
                // Unsupported intent fallback — use conversation language
                finalResponse = isSpanish
                    ? "Puedo responder preguntas concretas sobre los datos, por ejemplo la fecha de salida de un inquilino, su habitación, pagos pendientes o habitaciones disponibles."
                    : "I can answer specific questions about the data, such as a tenant's move-out date, current room, pending payments, or available rooms.";
            }

            Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = finalResponse });
            ScrollRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is InvalidOperationException && ex.Message == "AI_OFFLINE" || ex.InnerException is System.Net.Http.HttpRequestException)
        {
            UpdateStage(AiProcessingStage.Failed);
            Messages.Add(new ChatMessageViewModel
            {
                Role = "assistant",
                Content = isSpanish
                    ? "No se pudo conectar con el servidor local de IA. Asegúrese de que está iniciado en la configuración."
                    : "Could not connect to the local AI server. Please ensure it is running in your settings."
            });
            ScrollRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            UpdateStage(AiProcessingStage.Failed);
            Messages.Add(new ChatMessageViewModel
            {
                Role = "assistant",
                Content = isSpanish
                    ? $"Error al procesar la consulta: {ex.Message}"
                    : $"Error processing request: {ex.Message}"
            });
            ScrollRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoading = false;
            UpdateStage(AiProcessingStage.None);
        }
    }
}
