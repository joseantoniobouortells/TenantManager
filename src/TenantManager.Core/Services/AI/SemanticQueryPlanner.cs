using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Service responsible for calling LM Studio to extract a structured query plan
/// and parsing the response safely.
/// </summary>
public class SemanticQueryPlanner
{
    private readonly LocalAiClient _aiClient;

    public SemanticQueryPlanner(LocalAiClient aiClient)
    {
        _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
    }

    /// <summary>
    /// Translates a natural language message into a SemanticQueryPlan by querying the local LLM.
    /// Returns null if the LLM is unreachable or returned invalid JSON.
    /// </summary>
    public async Task<SemanticQueryPlan?> PlanQueryAsync(
        string userMessage, 
        AssistantContext? context = null, 
        CancellationToken cancellationToken = default)
    {
        var rawResponse = await _aiClient.BuildQueryPlanAsync(userMessage, context, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        var cleanedJson = CleanMarkdownJson(rawResponse);

        try
        {
            return JsonSerializer.Deserialize<SemanticQueryPlan>(cleanedJson);
        }
        catch
        {
            return null;
        }
    }

    private static string CleanMarkdownJson(string rawJson)
    {
        var cleaned = rawJson.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastBackticks = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline != -1 && lastBackticks != -1 && lastBackticks > firstNewline)
            {
                cleaned = cleaned.Substring(firstNewline + 1, lastBackticks - firstNewline - 1);
            }
        }
        return cleaned.Trim();
    }
}
