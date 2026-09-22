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
    private readonly IAssistantExecutionObserver? _observer;

    public AiQueryService(AppDbContext dbContext, LocalAiClient aiClient, IAssistantExecutionObserver? observer = null)
    {
        _dbContext = dbContext;
        _aiClient = aiClient;
        _observer = observer;
    }

    /// <summary>
    /// Resolves the user's question using LLM-based intent/entity extraction
    /// plus deterministic DB lookup. Updates conversation context on success.
    /// </summary>
    public async Task<(string? FinalAnswer, bool IsSpanish)> ResolveIntentAndGetDataAsync(
        string userMessage, AssistantContext? context = null, int propertyId = 0, Action<AiProcessingStage>? onProgress = null, Func<DateTimeOffset>? clock = null)
    {
        if (context != null)
        {
            if (context.LastPropertyId.HasValue && context.LastPropertyId.Value != propertyId)
            {
                context.Reset();
            }
            context.LastPropertyId = propertyId;
        }

        bool isSpanish = IsSpanishQuery(userMessage, context?.LastLanguage);
        _observer?.OnRequestReceived(userMessage);

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
                
                SemanticRequest? semanticRequest = null;
                var requestDto = await _aiClient.BuildSemanticRequestAsync(userMessage, context, clock);
                if (requestDto != null)
                {
                    semanticRequest = SemanticRequestBuilder.Build(requestDto);
                    var fastAnswer = SemanticRequestResolver.TryResolvePreviousResult(semanticRequest, context);
                    if (fastAnswer != null)
                    {
                        onProgress?.Invoke(AiProcessingStage.Completed);
                        return (fastAnswer, semanticRequest.Language.Equals("es", StringComparison.OrdinalIgnoreCase));
                    }
                }

                // If not resolved by context, we need the full QueryPlan
                var rawResponse = await _aiClient.BuildQueryPlanAsync(userMessage, context, clock);

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    onProgress?.Invoke(AiProcessingStage.Failed);
                    var plannerErrorMsg = (semanticRequest?.Language ?? "en") == "es"
                        ? "Lo siento, no he podido interpretar tu pregunta. Inténtalo de nuevo o simplifica la consulta."
                        : "Sorry, I could not interpret your question. Please try again or simplify your query.";
                    return (plannerErrorMsg, (semanticRequest?.Language ?? "en") == "es");
                }

                onProgress?.Invoke(AiProcessingStage.ParsingPlan);

                SemanticQueryPlan? rawPlan = null;
                try
                {
                    string cleanedResponse = rawResponse.Trim();
                    var firstBackticks = cleanedResponse.IndexOf("```", StringComparison.Ordinal);
                    var lastBackticks = cleanedResponse.LastIndexOf("```", StringComparison.Ordinal);
                    if (firstBackticks != -1 && lastBackticks != -1 && lastBackticks > firstBackticks)
                    {
                        var firstNewline = cleanedResponse.IndexOf('\n', firstBackticks);
                        if (firstNewline != -1 && firstNewline < lastBackticks)
                        {
                            cleanedResponse = cleanedResponse.Substring(firstNewline + 1, lastBackticks - firstNewline - 1);
                        }
                    }
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    rawPlan = JsonSerializer.Deserialize<SemanticQueryPlan>(cleanedResponse, options);
                }
                catch (Exception ex)
                {
                    return ($"DESERIALIZE ERROR: {ex.Message} \n {rawResponse}", (semanticRequest?.Language ?? "en") == "es");
                }

                if (rawPlan != null)
                {
                    if (semanticRequest != null)
                    {
                        rawPlan = SemanticRequestResolver.EnrichPlanWithPeriod(rawPlan, semanticRequest, context, userMessage);
                        rawPlan.Language = semanticRequest.Language;
                    }

                    // Deterministic year resolution: if plan has no year filter, inspect userMessage or inherit from context
                    bool hasYear = rawPlan.Filters.Exists(f => f.Field.Equals("year", StringComparison.OrdinalIgnoreCase));
                    if (!hasYear)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(userMessage, @"\b(20\d{2})\b");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var explicitYear))
                        {
                            rawPlan.Filters.Add(new SemanticQueryFilter
                            {
                                Field = "year",
                                Operator = SemanticQueryOperator.Equals,
                                Value = explicitYear
                            });
                        }
                        else if (SemanticRequestResolver.IsAnnualQuery(userMessage))
                        {
                            int defaultYear = clock != null ? clock().Year : DateTime.Today.Year;
                            rawPlan.Filters.Add(new SemanticQueryFilter
                            {
                                Field = "year",
                                Operator = SemanticQueryOperator.Equals,
                                Value = defaultYear
                            });
                        }
                        else if (context?.LastYear.HasValue == true)
                        {
                            rawPlan.Filters.Add(new SemanticQueryFilter
                            {
                                Field = "year",
                                Operator = SemanticQueryOperator.Equals,
                                Value = context.LastYear.Value
                            });
                        }
                    }

                    if (SemanticRequestResolver.IsAnnualQuery(userMessage))
                    {
                        rawPlan.Filters.RemoveAll(f => f.Field.Equals("month", StringComparison.OrdinalIgnoreCase));
                    }

                    // Canonicalize planner mistakes: follow-ups sometimes use "tenantName" instead of "fullName" for the tenants resource
                    if (rawPlan.Resource == SemanticQueryResource.Tenants)
                    {
                        foreach (var filter in rawPlan.Filters)
                        {
                            if (filter.Field.Equals("tenantName", StringComparison.OrdinalIgnoreCase))
                            {
                                filter.Field = "fullName";
                            }
                            else if (filter.Field.Equals("contracts", StringComparison.OrdinalIgnoreCase))
                            {
                                filter.Field = "active";
                                if (filter.Operator == SemanticQueryOperator.NotEquals && (filter.Value?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true))
                                {
                                    filter.Operator = SemanticQueryOperator.Equals;
                                    filter.Value = false;
                                }
                                else if (filter.Value?.ToString()?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    filter.Operator = SemanticQueryOperator.Equals;
                                    filter.Value = false;
                                }
                            }
                        }

                        // Deterministic active vs inactive resolution from user message
                        var normMsg = NormalizeString(userMessage);
                        bool wantsInactive = normMsg.Contains("sin contrato") || 
                                            normMsg.Contains("no tienen contrato") || 
                                            normMsg.Contains("no tiene contrato") || 
                                            normMsg.Contains("no son actuales") || 
                                            normMsg.Contains("no activos") || 
                                            normMsg.Contains("inactivos") || 
                                            normMsg.Contains("antiguos") || 
                                            normMsg.Contains("pasados") || 
                                            normMsg.Contains("no estan activos") || 
                                            normMsg.Contains("without contract") || 
                                            normMsg.Contains("no active contract") || 
                                            normMsg.Contains("inactive") || 
                                            normMsg.Contains("former");

                        bool wantsActive = !wantsInactive && (
                                            normMsg.Contains("actual") || 
                                            normMsg.Contains("actuales") || 
                                            normMsg.Contains("vigente") || 
                                            normMsg.Contains("vigentes") || 
                                            normMsg.Contains("en este momento") || 
                                            normMsg.Contains("current") || 
                                            normMsg.Contains("active") || 
                                            normMsg.Contains("now"));

                        if (wantsInactive)
                        {
                            rawPlan.Filters.RemoveAll(f => f.Field.Equals("active", StringComparison.OrdinalIgnoreCase));
                            rawPlan.Filters.Add(new SemanticQueryFilter
                            {
                                Field = "active",
                                Operator = SemanticQueryOperator.Equals,
                                Value = false
                            });
                        }
                        else if (wantsActive)
                        {
                            if (!rawPlan.Filters.Any(f => f.Field.Equals("active", StringComparison.OrdinalIgnoreCase)))
                            {
                                rawPlan.Filters.Add(new SemanticQueryFilter
                                {
                                    Field = "active",
                                    Operator = SemanticQueryOperator.Equals,
                                    Value = true
                                });
                            }
                        }

                        // If query asks about rooms for tenants, ensure currentRoom is projected
                        bool mentionsRooms = normMsg.Contains("habitacion") || 
                                             normMsg.Contains("habitaciones") || 
                                             normMsg.Contains("cuarto") || 
                                             normMsg.Contains("cuartos") || 
                                             normMsg.Contains("room") || 
                                             normMsg.Contains("rooms");
                        if (mentionsRooms && !rawPlan.Projection.Contains("currentRoom", StringComparer.OrdinalIgnoreCase))
                        {
                            rawPlan.Projection.Add("currentRoom");
                        }
                        if (mentionsRooms && !rawPlan.Projection.Contains("fullName", StringComparer.OrdinalIgnoreCase))
                        {
                            rawPlan.Projection.Insert(0, "fullName");
                        }
                    }

                    // Canonicalize planner mistakes: profit queries might use dashboard + sum instead of dashboard + summary
                    if (rawPlan.Resource == SemanticQueryResource.Dashboard && rawPlan.Operation == SemanticQueryOperation.Sum && rawPlan.Projection.Contains("profit", StringComparer.OrdinalIgnoreCase))
                    {
                        rawPlan.Operation = SemanticQueryOperation.Summary;
                    }

                    // Canonicalize executive report requests
                    var normUserMsg = NormalizeString(userMessage);
                    bool isReportRequest = normUserMsg.Contains("informe") ||
                                          normUserMsg.Contains("reporte") ||
                                          normUserMsg.Contains("situacion financiera") ||
                                          normUserMsg.Contains("balance") ||
                                          normUserMsg.Contains("grafico") ||
                                          normUserMsg.Contains("graficos") ||
                                          normUserMsg.Contains("executive report") ||
                                          normUserMsg.Contains("financial report");

                    if (isReportRequest)
                    {
                        rawPlan.Resource = SemanticQueryResource.Dashboard;
                        rawPlan.Operation = SemanticQueryOperation.Summary;

                        bool asksForHistoryOnly = normUserMsg.Contains("grafico") || normUserMsg.Contains("graficos") || normUserMsg.Contains("evolucion") || normUserMsg.Contains("historico") || normUserMsg.Contains("history");
                        bool asksForExpensesOnly = normUserMsg.Contains("gasto") || normUserMsg.Contains("gastos") || normUserMsg.Contains("expense") || normUserMsg.Contains("expenses");
                        bool asksForOccupancyOnly = normUserMsg.Contains("ocupacion") || normUserMsg.Contains("inquilino") || normUserMsg.Contains("inquilinos") || normUserMsg.Contains("habitacion") || normUserMsg.Contains("habitaciones");

                        if (asksForHistoryOnly && !asksForExpensesOnly && !asksForOccupancyOnly)
                        {
                            rawPlan.Projection.Clear();
                            rawPlan.Projection.Add("monthlyHistory");
                            rawPlan.Projection.Add("profit");
                        }
                        else if (asksForExpensesOnly && !normUserMsg.Contains("financier") && !normUserMsg.Contains("global"))
                        {
                            rawPlan.Projection.Clear();
                            rawPlan.Projection.Add("totalExpenses");
                            rawPlan.Projection.Add("expenseCategories");
                        }
                        else if (asksForOccupancyOnly && !normUserMsg.Contains("financier") && !normUserMsg.Contains("global"))
                        {
                            rawPlan.Projection.Clear();
                            rawPlan.Projection.Add("occupancyRate");
                            rawPlan.Projection.Add("leases");
                        }
                        else
                        {
                            if (!rawPlan.Projection.Contains("totalIncome", StringComparer.OrdinalIgnoreCase))
                                rawPlan.Projection.Add("totalIncome");
                            if (!rawPlan.Projection.Contains("totalExpenses", StringComparer.OrdinalIgnoreCase))
                                rawPlan.Projection.Add("totalExpenses");
                            if (!rawPlan.Projection.Contains("profit", StringComparer.OrdinalIgnoreCase))
                                rawPlan.Projection.Add("profit");
                            if (!rawPlan.Projection.Contains("pendingAmount", StringComparer.OrdinalIgnoreCase))
                                rawPlan.Projection.Add("pendingAmount");
                            if (!rawPlan.Projection.Contains("occupancyRate", StringComparer.OrdinalIgnoreCase))
                                rawPlan.Projection.Add("occupancyRate");
                        }

                        if (!rawPlan.Filters.Any(f => f.Field.Equals("year", StringComparison.OrdinalIgnoreCase)))
                        {
                            int defaultYear = clock != null ? clock().Year : DateTime.Today.Year;
                            rawPlan.Filters.Add(new SemanticQueryFilter
                            {
                                Field = "year",
                                Operator = SemanticQueryOperator.Equals,
                                Value = defaultYear
                            });
                        }
                    }

                    // Canonicalize planner mistakes: if query asks for payments per tenant ("cada uno") and planner used sum + tenantName projection, switch to list to show breakdown
                    if (rawPlan.Resource == SemanticQueryResource.Payments && rawPlan.Operation == SemanticQueryOperation.Sum && rawPlan.Projection.Contains("tenantName", StringComparer.OrdinalIgnoreCase))
                    {
                        rawPlan.Operation = SemanticQueryOperation.List;
                    }

                        _observer?.OnPlanGenerated(rawPlan);

                        onProgress?.Invoke(AiProcessingStage.ValidatingPlan);
                        var validationResult = SemanticQueryPlanValidator.Validate(rawPlan, propertyId);
                        if (validationResult.IsValid)
                        {
                            onProgress?.Invoke(AiProcessingStage.ExecutingQuery);
                            var executor = new SemanticQueryExecutor(_dbContext);
                            var executionResult = await executor.ExecuteAsync(rawPlan);
                            _observer?.OnQueryExecuted(executionResult is not string);
                            if (executionResult is string errorMsg)
                            {
                                onProgress?.Invoke(AiProcessingStage.Failed);
                                return (errorMsg, rawPlan.Language.Equals("es", StringComparison.OrdinalIgnoreCase));
                            }
                            
                            onProgress?.Invoke(AiProcessingStage.FormattingResponse);
                            string formattedAnswer = SemanticAnswerFormatter.Format(rawPlan, executionResult, rawPlan.Language);
                            _observer?.OnResponseFormatted(formattedAnswer);

                            if (context != null)
                            {
                                UpdateSemanticContext(context, rawPlan, propertyId);
                                _observer?.OnPeriodResolved(context.LastYear, context.LastMonth);
                                context.LastFormattedAnswer = formattedAnswer;
                                context.LastExecutionResult = executionResult;

                                if (semanticRequest != null)
                                {
                                    formattedAnswer = SemanticRequestResolver.EnrichFormattedAnswer(formattedAnswer, semanticRequest, context);
                                }
                                else if (rawPlan.Projection.Contains("period", StringComparer.OrdinalIgnoreCase) && context.LastYear.HasValue)
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
            isSpanish = IsSpanishQuery(userMessage, extraction.Language);
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

    private static readonly Dictionary<string, string[]> SpanishHypocoristics = new(StringComparer.OrdinalIgnoreCase)
    {
        { "pepe", new[] { "jose", "jose antonio", "jose maria", "jose manuel", "jose luis" } },
        { "jose", new[] { "pepe" } },
        { "paco", new[] { "francisco", "francisco javier" } },
        { "curro", new[] { "francisco" } },
        { "fran", new[] { "francisco" } },
        { "francisco", new[] { "paco", "curro", "fran" } },
        { "nacho", new[] { "ignacio" } },
        { "ignacio", new[] { "nacho" } },
        { "lola", new[] { "dolores" } },
        { "lolita", new[] { "dolores" } },
        { "dolores", new[] { "lola", "lolita" } },
        { "javi", new[] { "javier" } },
        { "javier", new[] { "javi" } },
        { "dani", new[] { "daniel" } },
        { "daniel", new[] { "dani" } },
        { "alex", new[] { "alejandro" } },
        { "alejandro", new[] { "alex" } },
        { "manu", new[] { "manuel" } },
        { "manolo", new[] { "manuel" } },
        { "manuel", new[] { "manu", "manolo" } },
        { "quique", new[] { "enrique" } },
        { "enrique", new[] { "quique" } },
        { "rafa", new[] { "rafael" } },
        { "rafael", new[] { "rafa" } },
        { "toni", new[] { "antonio", "jose antonio" } },
        { "toño", new[] { "antonio" } },
        { "antonio", new[] { "toni", "toño" } },
        { "concha", new[] { "concepcion" } },
        { "conchita", new[] { "concepcion" } },
        { "concepcion", new[] { "concha", "conchita" } },
        { "merche", new[] { "mercedes" } },
        { "mercedes", new[] { "merche" } },
        { "maite", new[] { "maria teresa" } },
        { "chema", new[] { "jose maria" } }
    };

    /// <summary>
    /// Safe token-based tenant name matching with clarification on ambiguity and Spanish hypocoristic fallback.
    /// </summary>
    public static Tenant? FindBestTenantMatch(
        string requestedName, System.Collections.Generic.List<Tenant> tenants,
        bool isSpanish, out string? clarification)
    {
        clarification = null;
        var targetNorm = NormalizeString(requestedName);
        var targetTokens = targetNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // 1. Exact match on normalized full name
        var exactMatch = tenants.FirstOrDefault(t => NormalizeString(t.FullName) == targetNorm);
        if (exactMatch != null) return exactMatch;

        // 2. Token containment match
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

        // 3. Fallback: Spanish hypocoristics / nicknames match
        var hypocoristicMatches = tenants.Where(t =>
        {
            var tNorm = NormalizeString(t.FullName);
            var tTokens = tNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return MatchesWithHypocoristics(targetTokens, tTokens);
        }).Distinct().ToList();

        if (hypocoristicMatches.Count == 1) return hypocoristicMatches[0];

        if (hypocoristicMatches.Count > 1)
        {
            var names = string.Join(", ", hypocoristicMatches.Select(p => p.FullName));
            clarification = isSpanish
                ? $"He encontrado varios inquilinos parecidos: {names}. ¿A cuál te refieres?"
                : $"I found multiple similar tenants: {names}. Which one do you mean?";
            return null;
        }

        // 4. No matches found: clean, human-friendly clarification without debug strings
        if (tenants.Count == 0)
        {
            clarification = isSpanish
                ? "No hay inquilinos registrados en esta propiedad."
                : "There are no tenants registered in this property.";
        }
        else
        {
            var allNames = string.Join(", ", tenants.Select(t => t.FullName));
            clarification = isSpanish
                ? $"No encuentro ningún inquilino llamado {requestedName}. Los inquilinos registrados son: {allNames}."
                : $"I cannot find a tenant named {requestedName}. Registered tenants are: {allNames}.";
        }
        return null;
    }

    private static bool MatchesWithHypocoristics(string[] requestedTokens, string[] candidateTokens)
    {
        if (requestedTokens.Length == 0 || candidateTokens.Length == 0) return false;
        foreach (var req in requestedTokens)
        {
            bool tokenMatched = false;
            foreach (var cand in candidateTokens)
            {
                if (req.Equals(cand, StringComparison.OrdinalIgnoreCase))
                {
                    tokenMatched = true;
                    break;
                }
            }
            if (tokenMatched) continue;

            // Check if req is an alias for a name or phrase, and all tokens of that alias are present in candidateTokens
            if (SpanishHypocoristics.TryGetValue(req, out var reqAliases))
            {
                foreach (var alias in reqAliases)
                {
                    var aliasTokens = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (aliasTokens.All(at => candidateTokens.Contains(at, StringComparer.OrdinalIgnoreCase)))
                    {
                        tokenMatched = true;
                        break;
                    }
                }
            }
            if (tokenMatched) continue;

            // Check reverse: if a candidate token is an alias, and all tokens of that alias are present in requestedTokens
            foreach (var cand in candidateTokens)
            {
                if (SpanishHypocoristics.TryGetValue(cand, out var candAliases))
                {
                    foreach (var alias in candAliases)
                    {
                        var aliasTokens = alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (aliasTokens.All(at => requestedTokens.Contains(at, StringComparer.OrdinalIgnoreCase)))
                        {
                            tokenMatched = true;
                            break;
                        }
                    }
                }
                if (tokenMatched) break;
            }

            if (!tokenMatched) return false;
        }
        return true;
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
        if (yearFilter != null)
        {
            var strVal = yearFilter.Value?.ToString();
            Console.WriteLine($"[UpdateSemanticContext] Found year filter: {strVal}");
            if (int.TryParse(strVal, out var y))
            {
                context.LastYear = y;
            }
            else
            {
                Console.WriteLine($"[UpdateSemanticContext] TryParse failed for: {strVal}");
                context.LastYear = null;
            }
        }
        else
        {
            Console.WriteLine("[UpdateSemanticContext] No year filter found in plan.");
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

        _observer?.OnPeriodResolved(context.LastYear, context.LastMonth);

        // Find tenant name filter
        string? tenantName = null;
        foreach (var filter in plan.Filters)
        {
            if (filter.Field.Equals("tenantName", StringComparison.OrdinalIgnoreCase) || 
                filter.Field.Equals("fullName", StringComparison.OrdinalIgnoreCase))
            {
                tenantName = filter.Value?.ToString();
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(tenantName))
        {
            var tenants = _dbContext.Tenants.AsNoTracking().Where(t => t.PropertyId == propertyId).ToList();
            bool isSpanish = plan.Language.Equals("es", StringComparison.OrdinalIgnoreCase);
            var bestMatch = FindBestTenantMatch(tenantName, tenants, isSpanish, out _);
            
            if (bestMatch != null)
            {
                context.LastTenantId = bestMatch.Id;
                context.LastTenantDisplayName = bestMatch.FullName;
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

    public static bool IsSpanishQuery(string userMessage, string? defaultLanguage = null)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return defaultLanguage?.StartsWith("es", StringComparison.OrdinalIgnoreCase) ?? false;

        var lower = userMessage.ToLowerInvariant();
        if (lower.Contains('¿') || lower.Contains('á') || lower.Contains('é') || lower.Contains('í') || lower.Contains('ó') || lower.Contains('ú') || lower.Contains('ñ'))
            return true;

        var englishMarkers = new[] { "when", "does", "how much", "how many", "what is", "what was", "what were", "what about", "is there", "who", "which", "move out", "collected", "profit", "expenses", "income", "tenant", "room" };
        if (englishMarkers.Any(m => lower.Contains(m)))
            return false;

        var spanishMarkers = new[] { "cuándo", "cuando", "cuánto", "cuanto", "ingresó", "ingreso", "gastos", "beneficio", "mes", "año", "quién", "quien", "dónde", "donde", "inquilino", "habitacion", "habitación" };
        if (spanishMarkers.Any(m => lower.Contains(m)))
            return true;

        return defaultLanguage?.StartsWith("es", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
