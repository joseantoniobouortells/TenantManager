using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.Core.Services.AI;

public class IntentExtractionResult
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "unknown";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.0;

    [JsonPropertyName("entities")]
    public JsonElement Entities { get; set; }
}

public class AiQueryService
{
    private readonly AppDbContext _dbContext;
    private readonly LocalAiClient _aiClient;

    public AiQueryService(AppDbContext dbContext, LocalAiClient aiClient)
    {
        _dbContext = dbContext;
        _aiClient = aiClient;
    }

    /// <summary>
    /// Resolves the user's question using LLM-based intent/entity extraction
    /// plus deterministic DB lookup. Updates conversation context on success.
    /// </summary>
    public async Task<(string? FinalAnswer, bool IsSpanish)> ResolveIntentAndGetDataAsync(
        string userMessage, AssistantContext? context = null, int propertyId = 0, Action<AiProcessingStage>? onProgress = null)
    {
        if (context != null)
        {
            if (context.LastPropertyId.HasValue && context.LastPropertyId.Value != propertyId)
            {
                context.Reset();
            }
            context.LastPropertyId = propertyId;
        }

        bool isSpanish = context?.LastLanguage == "es";

        // ---- Fast path: PreviousResultQuery resolution (no LLM query plan needed) ----
        // Check if the user is simply asking about the previous result's metadata (period, label, etc.)
        // using lightweight keyword heuristics. This avoids an unnecessary LLM call.
        if (context?.HasContext == true)
        {
            var previousAnswer = SemanticRequestResolver.TryResolvePreviousResultByKeywords(
                userMessage, context, isSpanish: isSpanish);
            if (previousAnswer != null)
            {
                onProgress?.Invoke(AiProcessingStage.Completed);
                return (previousAnswer, isSpanish);
            }
        }

        onProgress?.Invoke(AiProcessingStage.PreparingRequest);

        // ---- Primary Path: Semantic Query Planner ----
        bool plannerAttempted = false;
        try
        {
            var settings = SettingsPersistence.LoadSettings();
            if (settings.IsAiEnabled && !string.IsNullOrWhiteSpace(settings.AiEndpoint))
            {
                plannerAttempted = true;
                onProgress?.Invoke(AiProcessingStage.SendingToServer);
                
                // Set to WaitingForModel immediately after triggering the request logic, 
                // but since LocalAiClient might take a while, we'll assume WaitingForModel happens implicitly or we can set it here.
                onProgress?.Invoke(AiProcessingStage.WaitingForModel);
                
                var rawResponse = await _aiClient.BuildQueryPlanAsync(userMessage, context);

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    onProgress?.Invoke(AiProcessingStage.Failed);
                    // Timeout or empty content
                    var plannerErrorMsg = isSpanish
                        ? "Lo siento, no he podido interpretar tu pregunta. Inténtalo de nuevo o simplifica la consulta."
                        : "Sorry, I could not interpret your question. Please try again or simplify your query.";
                    return (plannerErrorMsg, isSpanish);
                }

                onProgress?.Invoke(AiProcessingStage.ParsingPlan);

                bool isLegacyJson = false;
                try
                {
                    using var doc = JsonDocument.Parse(rawResponse);
                    if (doc.RootElement.TryGetProperty("intent", out _))
                    {
                        isLegacyJson = true;
                    }
                }
                catch
                {
                    // Invalid/incomplete JSON -> Return planner error
                    var plannerErrorMsg = isSpanish
                        ? "Lo siento, no he podido interpretar tu pregunta. Inténtalo de nuevo o simplifica la consulta."
                        : "Sorry, I could not interpret your question. Please try again or simplify your query.";
                    return (plannerErrorMsg, isSpanish);
                }

                if (!isLegacyJson)
                {
                    SemanticQueryPlan? rawPlan = null;
                    try
                    {
                        rawPlan = JsonSerializer.Deserialize<SemanticQueryPlan>(rawResponse);
                    }
                    catch
                    {
                        // Incomplete/malformed JSON
                        var plannerErrorMsg = isSpanish
                            ? "Lo siento, no he podido interpretar tu pregunta. Inténtalo de nuevo o simplifica la consulta."
                            : "Sorry, I could not interpret your question. Please try again or simplify your query.";
                        return (plannerErrorMsg, isSpanish);
                    }

                    if (rawPlan != null)
                    {
                        // Canonicalize planner mistakes: follow-ups sometimes use "tenantName" instead of "fullName" for the tenants resource
                        if (rawPlan.Resource == SemanticQueryResource.Tenants)
                        {
                            foreach (var filter in rawPlan.Filters)
                            {
                                if (filter.Field == "tenantName")
                                {
                                    filter.Field = "fullName";
                                }
                            }
                        }

                        // Canonicalize planner mistakes: profit queries might use dashboard + sum instead of dashboard + summary
                        if (rawPlan.Resource == SemanticQueryResource.Dashboard && rawPlan.Operation == SemanticQueryOperation.Sum && rawPlan.Projection.Contains("profit", StringComparer.OrdinalIgnoreCase))
                        {
                            rawPlan.Operation = SemanticQueryOperation.Summary;
                        }

                        onProgress?.Invoke(AiProcessingStage.ValidatingPlan);
                        var validationResult = SemanticQueryPlanValidator.Validate(rawPlan, propertyId);
                        if (validationResult.IsValid)
                        {
                            onProgress?.Invoke(AiProcessingStage.ExecutingQuery);
                            var executor = new SemanticQueryExecutor(_dbContext);
                            var executionResult = await executor.ExecuteAsync(rawPlan);
                            if (executionResult is string errorMsg)
                            {
                                onProgress?.Invoke(AiProcessingStage.Failed);
                                return (errorMsg, rawPlan.Language.Equals("es", StringComparison.OrdinalIgnoreCase));
                            }
                            
                            onProgress?.Invoke(AiProcessingStage.FormattingResponse);
                            string formattedAnswer = SemanticAnswerFormatter.Format(rawPlan, executionResult, rawPlan.Language);

                            if (context != null)
                            {
                                UpdateSemanticContext(context, rawPlan, propertyId);
                                // Store the last formatted answer and execution result for PreviousResultQuery resolution
                                context.LastFormattedAnswer = formattedAnswer;
                                context.LastExecutionResult = executionResult;

                                // Multi-output: if projection requests 'period', append the period to the answer
                                if (rawPlan.Projection.Contains("period", StringComparer.OrdinalIgnoreCase)
                                    && context.LastYear.HasValue)
                                {
                                    bool es = rawPlan.Language.Equals("es", StringComparison.OrdinalIgnoreCase);
                                    var ci = System.Globalization.CultureInfo.GetCultureInfo(es ? "es-ES" : "en-US");
                                    if (context.LastMonth.HasValue)
                                    {
                                        var monthName = ci.DateTimeFormat.GetMonthName(context.LastMonth.Value);
                                        var periodLine = es
                                            ? $"Período: {monthName} de {context.LastYear}"
                                            : $"Period: {monthName} {context.LastYear}";
                                        if (!formattedAnswer.Contains(periodLine))
                                            formattedAnswer += "\n" + periodLine;
                                    }
                                }
                            }

                            onProgress?.Invoke(AiProcessingStage.Completed);
                            return (formattedAnswer, rawPlan.Language.Equals("es", StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            onProgress?.Invoke(AiProcessingStage.Failed);
                            var errAnswer = SemanticAnswerFormatter.FormatValidationError(validationResult, rawPlan.Language);
                            return (errAnswer, rawPlan.Language.Equals("es", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }
            }
        }
        catch (InvalidOperationException ex) when (ex.Message == "AI_OFFLINE")
        {
            onProgress?.Invoke(AiProcessingStage.Failed);
            throw;
        }
        catch
        {
            onProgress?.Invoke(AiProcessingStage.Failed);
            if (plannerAttempted)
            {
                var plannerErrorMsg = isSpanish
                    ? "Lo siento, se ha producido un error al procesar tu consulta."
                    : "Sorry, an error occurred while processing your query.";
                return (plannerErrorMsg, isSpanish);
            }
        }

        // ---- Fallback Path: Legacy Intent Extraction ----
        onProgress?.Invoke(AiProcessingStage.SendingToServer);
        var json = await _aiClient.ExtractIntentAsync(userMessage, context);
        onProgress?.Invoke(AiProcessingStage.ParsingPlan);
        IntentExtractionResult? extraction = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (json.StartsWith("```"))
                {
                    var firstNewline = json.IndexOf('\n');
                    var lastBackticks = json.LastIndexOf("```", StringComparison.Ordinal);
                    if (firstNewline != -1 && lastBackticks != -1 && lastBackticks > firstNewline)
                        json = json.Substring(firstNewline + 1, lastBackticks - firstNewline - 1);
                }
                extraction = JsonSerializer.Deserialize<IntentExtractionResult>(json);
            }
            catch { }
        }

        // ---- 2. Resolve intent & language ----
        string intent = "unknown";
        string? tenantName = null;
        double confidence = 0;

        if (extraction != null)
        {
            isSpanish = extraction.Language.StartsWith("es", StringComparison.InvariantCultureIgnoreCase);
            confidence = extraction.Confidence;
            intent = extraction.Intent;

            if (extraction.Entities.ValueKind == JsonValueKind.Object &&
                extraction.Entities.TryGetProperty("tenantName", out var nameProp))
                tenantName = nameProp.GetString();
        }
        else
        {
            // ---- FALLBACK: isolated keyword routing (only when LLM unavailable) ----
            var lowerMsg = NormalizeString(userMessage);
            bool isMoveOut = lowerMsg.Contains("move out") || lowerMsg.Contains("leave")
                || lowerMsg.Contains("deja") || lowerMsg.Contains("se va") || lowerMsg.Contains("sale");
            bool isRoom = lowerMsg.Contains("room") || lowerMsg.Contains("habitacion") || lowerMsg.Contains("cuarto");

            if (isMoveOut) intent = "tenant_move_out_date";
            else if (isRoom) intent = "tenant_current_room";

            tenantName = userMessage;
            confidence = 1.0;
        }

        // ---- 3. Follow-up inference ----
        // If intent is unknown and we have a previous context, and the message looks like a short follow-up,
        // inherit the previous intent.
        if ((intent == "unknown" || confidence < 0.5) && context?.HasContext == true)
        {
            var followUpTenantName = TryExtractFollowUpName(userMessage);
            if (!string.IsNullOrWhiteSpace(followUpTenantName))
            {
                intent = context.LastResolvedIntent!;
                tenantName = followUpTenantName;
                confidence = 0.85;
                // Reuse last language when message is too short to reliably detect
                isSpanish = context.LastLanguage == "es";
            }
        }

        // ---- 4. Gate on confidence ----
        if (confidence < 0.5 && extraction != null)
        {
            onProgress?.Invoke(AiProcessingStage.Failed);
            return (null, isSpanish);
        }

        onProgress?.Invoke(AiProcessingStage.ExecutingQuery);

        // ---- 5. Dispatch by intent ----
        if (intent == "tenant_move_out_date" || intent == "tenant_current_room")
        {
            if (string.IsNullOrWhiteSpace(tenantName)) 
            {
                onProgress?.Invoke(AiProcessingStage.Failed);
                return (null, isSpanish);
            }

            var tenants = await _dbContext.Tenants.ToListAsync();
            var bestMatch = FindBestTenantMatch(tenantName, tenants, isSpanish, out string? clarification);
            if (bestMatch == null)
            {
                onProgress?.Invoke(AiProcessingStage.Failed);
                return (clarification, isSpanish);
            }

            var contracts = await _dbContext.RentalContracts
                .Where(c => c.TenantId == bestMatch.Id)
                .ToListAsync();
            var latestContract = contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();
            var room = latestContract != null
                ? await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == latestContract.RoomId)
                : null;

            onProgress?.Invoke(AiProcessingStage.FormattingResponse);
            string answer;
            if (intent == "tenant_move_out_date")
            {
                DateTimeOffset? effectiveEndDate = latestContract?.EndDate;
                if (latestContract != null)
                {
                    var validExtensions = (await _dbContext.RentalContractExtensions
                        .Where(e => e.RentalContractId == latestContract.Id && e.EndDate.HasValue)
                        .ToListAsync())
                        .OrderByDescending(e => e.EndDate)
                        .ToList();
                    if (validExtensions.Any())
                        effectiveEndDate = validExtensions.First().EndDate;
                }

                if (effectiveEndDate.HasValue)
                {
                    var dateStr = effectiveEndDate.Value.ToString("yyyy-MM-dd");
                    answer = isSpanish
                        ? $"{bestMatch.FullName} deja la habitación el {dateStr}."
                        : $"{bestMatch.FullName} is scheduled to move out on {dateStr}.";
                }
                else
                {
                    answer = isSpanish
                        ? $"No hay fecha de salida registrada para {bestMatch.FullName}."
                        : $"There is no move-out date registered for {bestMatch.FullName}.";
                }
            }
            else // tenant_current_room
            {
                var rName = room?.Name ?? (isSpanish ? "ninguna habitación" : "no room");
                answer = isSpanish
                    ? $"{bestMatch.FullName} está en la habitación {rName}."
                    : $"{bestMatch.FullName} is assigned to {rName}.";
            }

            // Update context after a successful supported answer
            if (context != null)
            {
                context.LastResolvedIntent = intent;
                context.LastLanguage = isSpanish ? "es" : "en";
                context.LastEntityType = "tenantName";
            }
            onProgress?.Invoke(AiProcessingStage.Completed);
            return (answer, isSpanish);
        }
        else if (intent == "dashboard_summary" || intent == "available_rooms" || intent == "pending_or_late_payments")
        {
            var rooms = await _dbContext.Rooms.ToListAsync();
            var tenantsCount = await _dbContext.Tenants.CountAsync();
            onProgress?.Invoke(AiProcessingStage.FormattingResponse);
            var ans = isSpanish
                ? $"Resumen: La aplicación tiene {rooms.Count} habitaciones y {tenantsCount} inquilinos."
                : $"Summary: The app has {rooms.Count} rooms and {tenantsCount} tenants.";

            if (context != null)
            {
                context.LastResolvedIntent = intent;
                context.LastLanguage = isSpanish ? "es" : "en";
            }
            onProgress?.Invoke(AiProcessingStage.Completed);
            return (ans, isSpanish);
        }

        onProgress?.Invoke(AiProcessingStage.Failed);
        return (null, isSpanish);
    }

    // ----- Private helpers -----

    /// <summary>
    /// Looks for patterns like "Y Nombre?", "¿Y Nombre?", "And Name?", "What about Name?"
    /// and returns the bare name, or null if this doesn't look like a follow-up.
    /// </summary>
    private static string? TryExtractFollowUpName(string userMessage)
    {
        var msg = userMessage.Trim().TrimStart('¿').TrimEnd('?', '.', '!').Trim();

        // ES: "Y Nombre" / "y nombre"
        if (msg.StartsWith("y ", StringComparison.OrdinalIgnoreCase) ||
            msg.StartsWith("¿y ", StringComparison.OrdinalIgnoreCase))
        {
            var name = System.Text.RegularExpressions.Regex.Replace(
                msg, @"^[¿y\s]+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(name) && name.Split(' ').Length <= 4)
                return name;
        }

        // EN: "And Name" / "What about Name"
        if (msg.StartsWith("and ", StringComparison.OrdinalIgnoreCase))
        {
            var name = msg.Substring(4).Trim();
            if (!string.IsNullOrWhiteSpace(name) && name.Split(' ').Length <= 4)
                return name;
        }
        if (msg.StartsWith("what about ", StringComparison.OrdinalIgnoreCase))
        {
            var name = msg.Substring(11).Trim();
            if (!string.IsNullOrWhiteSpace(name) && name.Split(' ').Length <= 4)
                return name;
        }

        return null;
    }

    /// <summary>
    /// Safe token-based tenant name matching with clarification on ambiguity.
    /// </summary>
    public static Tenant? FindBestTenantMatch(
        string requestedName, System.Collections.Generic.List<Tenant> tenants,
        bool isSpanish, out string? clarification)
    {
        clarification = null;
        var targetNorm = NormalizeString(requestedName);
        var targetTokens = targetNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var exactMatch = tenants.FirstOrDefault(t => NormalizeString(t.FullName) == targetNorm);
        if (exactMatch != null) return exactMatch;

        var partialMatches = tenants.Where(t =>
        {
            var tNorm = NormalizeString(t.FullName);
            var tTokens = tNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return targetTokens.All(token => tTokens.Contains(token));
        }).ToList();

        if (partialMatches.Count == 1) return partialMatches[0];

        if (partialMatches.Count > 1)
        {
            var names = string.Join(", ", partialMatches.Select(p => p.FullName));
            clarification = isSpanish
                ? $"He encontrado varios inquilinos parecidos: {names}. ¿A cuál te refieres?"
                : $"I found multiple similar tenants: {names}. Which one do you mean?";
            return null;
        }

        clarification = isSpanish
            ? $"No encuentro un inquilino llamado {requestedName}."
            : $"I cannot find a tenant named {requestedName}.";
        return null;
    }

    public static string NormalizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = input.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var noAccents = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        return string.Join(" ", noAccents.Split(
            new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private void UpdateSemanticContext(AssistantContext context, SemanticQueryPlan plan, int propertyId)
    {
        context.LastResolvedIntent = plan.Resource!.Value.ToString().ToLowerInvariant() + "_" + plan.Operation!.Value.ToString().ToLowerInvariant();
        context.LastLanguage = plan.Language;
        context.LastResource = plan.Resource?.ToString().ToLowerInvariant();
        context.LastOperation = plan.Operation?.ToString().ToLowerInvariant();
        context.LastProjection = plan.Projection;

        // Extract period values if present in filters
        var yearFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("year", StringComparison.OrdinalIgnoreCase));
        if (yearFilter != null && int.TryParse(yearFilter.Value?.ToString(), out var y))
        {
            context.LastYear = y;
        }
        else
        {
            context.LastYear = null;
        }

        var monthFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("month", StringComparison.OrdinalIgnoreCase));
        if (monthFilter != null && int.TryParse(monthFilter.Value?.ToString(), out var m))
        {
            context.LastMonth = m;
        }
        else
        {
            context.LastMonth = null;
        }

        // Find tenant name filter
        string? tenantName = null;
        foreach (var filter in plan.Filters)
        {
            bool isTenantNameFilter = false;
            if (plan.Resource == SemanticQueryResource.Tenants && filter.Field.Equals("fullName", StringComparison.OrdinalIgnoreCase)) isTenantNameFilter = true;
            else if (plan.Resource == SemanticQueryResource.Contracts && filter.Field.Equals("tenantName", StringComparison.OrdinalIgnoreCase)) isTenantNameFilter = true;
            else if (plan.Resource == SemanticQueryResource.Payments && filter.Field.Equals("tenantName", StringComparison.OrdinalIgnoreCase)) isTenantNameFilter = true;

            if (isTenantNameFilter && filter.Value != null)
            {
                tenantName = filter.Value.ToString();
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(tenantName))
        {
            var tenant = _dbContext.Tenants.AsNoTracking()
                .FirstOrDefault(t => t.PropertyId == propertyId && t.FullName == tenantName);
            if (tenant != null)
            {
                context.LastTenantId = tenant.Id;
                context.LastTenantDisplayName = tenant.FullName;
            }
        }
    }

    private static string CleanJsonOutput(string rawJson)
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
