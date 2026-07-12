namespace TenantManager.App.Data;

public class AppSettings
{
    public string Theme { get; set; } = "Default";
    public string Language { get; set; } = "en";
    
    // AI Settings
    public bool IsAiEnabled { get; set; } = false;
    public string AiEndpoint { get; set; } = "http://localhost:1234/v1/chat/completions";
    public string AiModelName { get; set; } = "qwen3.5-4b";
}
