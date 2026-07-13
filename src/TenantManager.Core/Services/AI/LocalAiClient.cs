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

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
}

public class ChatResponseChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
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
        var result = await SendChatCompletionInternalAsync(systemPrompt, userMessage, isSpanish, cancellationToken);

        // If content is completely empty, it might be stuck reasoning. Retry once with a harder prompt.
        if (result == "The AI returned an empty response." || result == "La IA devolvió una respuesta vacía." || string.IsNullOrWhiteSpace(result))
        {
            var harderPrompt = systemPrompt + "\n\nCRITICAL: DO NOT use reasoning. DO NOT output <think>. Provide ONLY the final answer in 1-2 sentences max.";
            var retryResult = await SendChatCompletionInternalAsync(harderPrompt, userMessage, isSpanish, cancellationToken);
            
            if (retryResult == "The AI returned an empty response." || retryResult == "La IA devolvió una respuesta vacía." || string.IsNullOrWhiteSpace(retryResult))
            {
                return isSpanish 
                    ? "Lo siento, la IA no devolvió una respuesta válida. Es posible que el modelo esté generando un razonamiento excesivo."
                    : "Sorry, the AI did not return a valid response. The model might be generating excessive reasoning.";
            }
            return retryResult;
        }

        return result;
    }

    private async Task<string> SendChatCompletionInternalAsync(string systemPrompt, string userMessage, bool isSpanish = false, CancellationToken cancellationToken = default)
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
            Temperature = 0.0, // Keep it deterministic
            MaxTokens = 150, // Limit to avoid long reasoning output
            Stream = false
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
                var content = chatResponse.Choices[0].Message?.Content;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
                return isSpanish ? "La IA devolvió una respuesta vacía." : "The AI returned an empty response.";
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

    public async Task<string?> ExtractIntentAsync(string userMessage, AssistantContext? context = null, CancellationToken cancellationToken = default)
    {
        var settings = SettingsPersistence.LoadSettings();

        if (!settings.IsAiEnabled || string.IsNullOrWhiteSpace(settings.AiEndpoint))
        {
            return null;
        }

        var contextHint = "";
        if (context != null && context.HasContext)
        {
            contextHint = $"\nConversation context: previous_intent={context.LastResolvedIntent}, previous_language={context.LastLanguage ?? "unknown"}.\nUse this context to interpret short follow-up questions that provide only a new name.\n";
        }

        string extractionPrompt = $@"Extract the user's intent and entities into JSON.
Return JSON ONLY. Do not use markdown. Do not include explanations.{contextHint}

{{
  ""language"": ""es"", // or en
  ""intent"": ""tenant_move_out_date"", // or tenant_current_room, dashboard_summary, available_rooms, pending_or_late_payments, missing_contract_files, unknown
  ""entities"": {{
    ""tenantName"": ""Erik Artigas"" // if present
  }},
  ""confidence"": 0.92 // 0.0 to 1.0
}}";

        var requestBody = new ChatRequest
        {
            Model = settings.AiModelName ?? string.Empty,
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = extractionPrompt },
                new ChatMessage { Role = "user", Content = userMessage }
            },
            Temperature = 0.0
        };

        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(settings.AiEndpoint, httpContent, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);

            return chatResponse?.Choices?[0]?.Message?.Content;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            throw new InvalidOperationException("AI_OFFLINE", ex);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> BuildQueryPlanAsync(string userMessage, AssistantContext? context = null, CancellationToken cancellationToken = default)
    {
        return await BuildQueryPlanAsync(userMessage, context, clock: null, cancellationToken);
    }

    /// <summary>
    /// Overload that accepts an injectable date provider for deterministic testing.
    /// </summary>
    public async Task<string?> BuildQueryPlanAsync(string userMessage, AssistantContext? context, Func<DateTimeOffset>? clock, CancellationToken cancellationToken = default)
    {
        var settings = SettingsPersistence.LoadSettings();

        if (!settings.IsAiEnabled || string.IsNullOrWhiteSpace(settings.AiEndpoint))
        {
            return null;
        }

        var now = (clock ?? (() => DateTimeOffset.Now))();
        var currentDate = now.ToString("yyyy-MM-dd");
        var currentYear = now.Year;
        var currentMonth = now.Month;

        var contextHint = "";
        if (context != null && context.HasContext)
        {
            contextHint = $"\nConversation context: previous_intent={context.LastResolvedIntent}, previous_language={context.LastLanguage ?? "unknown"}.\nUse this to resolve follow-up questions.\n";
        }

        string plannerPrompt = $@"You are a Semantic Query Planner. Translate the user question into one SemanticQueryPlan JSON object.
Return JSON ONLY. No markdown. No explanation. No reasoning tokens.

Today is {currentDate} (year={currentYear}, month={currentMonth}).

Relative-date rules (apply automatically, do not reason about them):
- 'this month' / 'este mes' -> year={currentYear}, month={currentMonth}
- 'this year' / 'este año' -> year={currentYear}
- 'last month' / 'mes pasado' -> year={(currentMonth == 1 ? currentYear - 1 : currentYear)}, month={(currentMonth == 1 ? 12 : currentMonth - 1)}

Domain rules:
- 'ingresos' / 'income' / 'collected' / 'cobrado' means paidAmount (operation: sum, resource: payments, field: paidAmount).
- 'pendiente' / 'pending' means unpaid amounts (use pending filter).
- 'atrasado' / 'late' means overdue (use late filter).

Allowed resources and fields:
- rooms: active(bool), occupied(bool), available(bool), currentRent(decimal), name(string) -> count, list
- tenants: active(bool), fullName(string), currentRoom(string), moveInDate(date), effectiveMoveOutDate(date) -> count, list, lookup
- contracts: active(bool), tenantName(string), roomName(string), startDate(date), baseEndDate(date), effectiveEndDate(date), hasExtensions(bool), missingFile(bool) -> count, list
- payments: status(string), year(int), month(int), expectedAmount(decimal), paidAmount(decimal), tenantName(string), pending(bool), late(bool) -> count, list, sum
- expenses: category(string), amount(decimal), date(date) -> count, list, sum
- dashboard: -> summary

Allowed operators: equals, not_equals, greater_than, greater_than_or_equal, less_than, less_than_or_equal, contains, in, between

JSON schema:
{{""language"": ""es"", ""resource"": ""payments"", ""operation"": ""sum"", ""filters"": [{{""field"": ""year"", ""operator"": ""equals"", ""value"": {currentYear}}}, {{""field"": ""month"", ""operator"": ""equals"", ""value"": {currentMonth}}}], ""projection"": [""paidAmount""], ""sort"": [], ""limit"": 20, ""confidence"": 0.95}}{contextHint}";

        var requestBody = new ChatRequest
        {
            Model = settings.AiModelName ?? string.Empty,
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = plannerPrompt },
                new ChatMessage { Role = "user", Content = userMessage }
            },
            Temperature = 0.0,
            MaxTokens = 1024, // Conservative default to allow reasoning tokens + JSON plan
            Stream = false
        };

        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(settings.AiEndpoint, httpContent, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);

            var choice = chatResponse?.Choices?[0];
            if (choice != null)
            {
                var isTruncated = choice.FinishReason == "length";
                if (isTruncated)
                {
                    // Retry once with a larger token budget (2048)
                    requestBody.MaxTokens = 2048;
                    jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
                    httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    response = await _httpClient.PostAsync(settings.AiEndpoint, httpContent, cancellationToken);
                    if (!response.IsSuccessStatusCode) return null;

                    responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);
                    choice = chatResponse?.Choices?[0];

                    if (choice != null && choice.FinishReason == "length")
                    {
                        // A second truncated response stops and returns planner failure
                        return null;
                    }
                }

                return choice?.Message?.Content;
            }

            return null;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            throw new InvalidOperationException("AI_OFFLINE", ex);
        }
        catch
        {
            return null;
        }
    }
}
