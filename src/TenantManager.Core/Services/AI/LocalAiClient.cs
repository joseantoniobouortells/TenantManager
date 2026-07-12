using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TenantManager.App.Data;

namespace TenantManager.Core.Services.AI;

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.0;
}

public class ChatResponseChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

public class ChatResponse
{
    [JsonPropertyName("choices")]
    public List<ChatResponseChoice> Choices { get; set; } = new();
}

public class LocalAiClient
{
    private readonly HttpClient _httpClient;

    public LocalAiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> SendChatCompletionAsync(string systemPrompt, string userMessage, bool isSpanish = false, CancellationToken cancellationToken = default)
    {
        var settings = SettingsPersistence.LoadSettings();

        if (!settings.IsAiEnabled)
        {
            return isSpanish ? "El asistente de IA está desactivado en la configuración." : "AI Assistant is currently disabled in settings.";
        }

        if (string.IsNullOrWhiteSpace(settings.AiEndpoint))
        {
            return isSpanish ? "El endpoint de IA no está configurado." : "AI Endpoint is not configured.";
        }

        var requestBody = new ChatRequest
        {
            Model = settings.AiModelName ?? string.Empty,
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userMessage }
            },
            Temperature = 0.0 // Keep it deterministic
        };

        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(settings.AiEndpoint, httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return isSpanish 
                    ? $"Error: El servidor de IA local respondió con código {response.StatusCode}."
                    : $"Error: The local AI server responded with status code {response.StatusCode}.";
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);

            if (chatResponse?.Choices != null && chatResponse.Choices.Count > 0)
            {
                return chatResponse.Choices[0].Message?.Content ?? (isSpanish ? "La IA devolvió una respuesta vacía." : "The AI returned an empty response.");
            }

            return isSpanish ? "Formato de respuesta inesperado del servidor local." : "Unexpected response format from the local AI server.";
        }
        catch (HttpRequestException)
        {
            return isSpanish 
                ? "Error: No se pudo conectar al servidor de IA local. Asegúrate de que LM Studio está ejecutándose y accesible."
                : "Error: Could not connect to the local AI server. Please ensure LM Studio (or your configured server) is running and accessible.";
        }
        catch (TaskCanceledException)
        {
            return isSpanish ? "Error: La petición al servidor de IA local agotó el tiempo de espera." : "Error: The request to the local AI server timed out or was canceled.";
        }
        catch (Exception ex)
        {
            return isSpanish ? $"Error inesperado: {ex.Message}" : $"Error: An unexpected error occurred: {ex.Message}";
        }
    }
}
