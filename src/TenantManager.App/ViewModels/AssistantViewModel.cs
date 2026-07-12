using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using TenantManager.App.Data;

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
        IsLoading = true;

        // Mock network delay
        await Task.Delay(1000);

        // Mock LLM Response for Phase 5
        var mockResponse = "This is a mocked response from the Local AI Assistant. Integration with the real LLM will happen in Phase 6.";
        
        Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = mockResponse });
        IsLoading = false;
    }
}
