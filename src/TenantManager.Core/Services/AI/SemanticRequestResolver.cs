using System;
using System.Collections.Generic;
using System.Globalization;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Deterministically resolves a SemanticRequest against the conversation context.
/// For PreviousResultQuery intents, answers directly from the stored last result.
/// For DataQuery intents, produces the enriched SemanticQueryPlan filters (year/month from Period).
/// </summary>
public static class SemanticRequestResolver
{
    /// <summary>
    /// Attempts to answer a PreviousResultQuery without hitting the database.
    /// Returns null if the intent is not PreviousResultQuery or there is no usable context.
    /// </summary>
    public static string? TryResolvePreviousResult(SemanticRequest request, AssistantContext? context)
    {
        if (request.Intent != SemanticRequestIntent.PreviousResultQuery)
            return null;

        if (context == null || !context.HasContext)
            return request.Language.Equals("es", StringComparison.OrdinalIgnoreCase)
                ? "No tengo información de una consulta anterior para responder a eso."
                : "I don't have information from a previous query to answer that.";

        bool isEs = request.Language.Equals("es", StringComparison.OrdinalIgnoreCase);

        // If user is asking about the period/month/year of the last result
        bool askingPeriod = IsPeriodQuery(request);
        if (askingPeriod && context.LastYear.HasValue)
        {
            var monthName = context.LastMonth.HasValue
                ? CultureInfo.GetCultureInfo(isEs ? "es-ES" : "en-US")
                    .DateTimeFormat.GetMonthName(context.LastMonth.Value)
                : null;

            if (monthName != null)
            {
                return isEs
                    ? $"La consulta anterior correspondía a {monthName} de {context.LastYear}."
                    : $"The previous query corresponded to {monthName} {context.LastYear}.";
            }
            return isEs
                ? $"La consulta anterior correspondía al año {context.LastYear}."
                : $"The previous query corresponded to year {context.LastYear}.";
        }

        // Generic fallback: return the last formatted answer if available
        if (!string.IsNullOrWhiteSpace(context.LastFormattedAnswer))
        {
            return isEs
                ? $"Según mi última consulta: {context.LastFormattedAnswer}"
                : $"Based on my last query: {context.LastFormattedAnswer}";
        }

        return isEs
            ? "No tengo suficiente contexto para responder a esa pregunta."
            : "I don't have enough context to answer that question.";
    }

    /// <summary>
    /// Enriches a SemanticQueryPlan's filter list with the period from a SemanticRequest
    /// when the plan is missing year/month filters that can be inferred from the request.
    /// </summary>
    public static SemanticQueryPlan EnrichPlanWithPeriod(SemanticQueryPlan plan, SemanticRequest request, AssistantContext context)
    {
        if (plan == null || request == null)
            return plan;

        bool hasYear = plan.Filters.Exists(f => f.Field.Equals("year", StringComparison.OrdinalIgnoreCase));
        bool hasMonth = plan.Filters.Exists(f => f.Field.Equals("month", StringComparison.OrdinalIgnoreCase));

        int? yearToApply = request.Period.Year ?? context?.LastYear;
        int? monthToApply = request.Period.Month ?? context?.LastMonth;

        if (yearToApply.HasValue && !hasYear)
        {
            plan.Filters.Add(new SemanticQueryFilter
            {
                Field = "year",
                Operator = SemanticQueryOperator.Equals,
                Value = yearToApply.Value
            });
        }

        if (monthToApply.HasValue && !hasMonth)
        {
            plan.Filters.Add(new SemanticQueryFilter
            {
                Field = "month",
                Operator = SemanticQueryOperator.Equals,
                Value = monthToApply.Value
            });
        }

        return plan;
    }

    /// <summary>
    /// Extends the formatted answer to include all RequestedOutputs from the SemanticRequest
    /// that were not already included in the primary answer.
    /// </summary>
    public static string EnrichFormattedAnswer(
        string primaryAnswer,
        SemanticRequest request,
        AssistantContext? context)
    {
        bool isEs = request.Language.Equals("es", StringComparison.OrdinalIgnoreCase);
        var extras = new List<string>();

        foreach (var output in request.RequestedOutputs)
        {
            // Skip the primary field already covered in the answer
            if (IsPrimaryField(output.Field, request))
                continue;

            // Period field: inject month/year from context
            if (IsPeriodField(output.Field) && context != null)
            {
                if (context.LastMonth.HasValue && context.LastYear.HasValue)
                {
                    var monthName = CultureInfo.GetCultureInfo(isEs ? "es-ES" : "en-US")
                        .DateTimeFormat.GetMonthName(context.LastMonth.Value);
                    extras.Add(isEs
                        ? $"{output.Label}: {monthName} de {context.LastYear}"
                        : $"{output.Label}: {monthName} {context.LastYear}");
                }
            }
        }

        if (extras.Count == 0)
            return primaryAnswer;

        return primaryAnswer + "\n" + string.Join("\n", extras);
    }

    /// <summary>
    /// Lightweight keyword-based heuristic to detect "what period was that?" type questions
    /// without a full LLM round-trip. Returns null if no match.
    /// </summary>
    public static string? TryResolvePreviousResultByKeywords(string userMessage, AssistantContext context, bool isSpanish)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || !context.HasContext)
            return null;

        var msg = userMessage.Trim().ToLowerInvariant();
        // Remove punctuation
        msg = System.Text.RegularExpressions.Regex.Replace(msg, @"[¿?!.]", " ").Trim();

        // Spanish period queries
        bool isPeriodQueryEs = msg.Contains("a qué mes") || msg.Contains("a que mes")
            || msg.Contains("qué mes") || msg.Contains("que mes")
            || msg.Contains("de qué mes") || msg.Contains("de que mes")
            || msg.Contains("qué periodo") || msg.Contains("qué período")
            || msg.Contains("que periodo") || msg.Contains("que período")
            || (msg.Contains("mes") && msg.Contains("corresponde"))
            || (msg.Contains("mes") && (msg.Length < 30));

        // English period queries
        bool isPeriodQueryEn = msg.Contains("what month") || msg.Contains("which month")
            || msg.Contains("what period") || msg.Contains("which period")
            || (msg.Contains("month") && msg.Contains("correspond"));

        bool isPeriodQuery = isSpanish ? isPeriodQueryEs : isPeriodQueryEn;
        // Also accept cross-language short queries
        if (!isPeriodQuery) isPeriodQuery = isPeriodQueryEs || isPeriodQueryEn;

        if (!isPeriodQuery) return null;
        if (!context.LastYear.HasValue && !context.LastMonth.HasValue) return null;

        var lang = isSpanish ? "es-ES" : "en-US";
        var ci = CultureInfo.GetCultureInfo(lang);

        if (context.LastMonth.HasValue && context.LastYear.HasValue)
        {
            var monthName = ci.DateTimeFormat.GetMonthName(context.LastMonth.Value);
            return isSpanish
                ? $"La consulta anterior correspondía a {monthName} de {context.LastYear}."
                : $"The previous query corresponded to {monthName} {context.LastYear}.";
        }
        if (context.LastYear.HasValue)
        {
            return isSpanish
                ? $"La consulta anterior correspondía al año {context.LastYear}."
                : $"The previous query corresponded to year {context.LastYear}.";
        }

        return null;
    }

    // ----- Private helpers -----

    private static bool IsPeriodQuery(SemanticRequest request)
    {
        foreach (var output in request.RequestedOutputs)
        {
            if (IsPeriodField(output.Field)) return true;
        }
        // Also check projection
        foreach (var proj in request.Projection)
        {
            if (IsPeriodField(proj)) return true;
        }
        return false;
    }

    private static bool IsPeriodField(string field) =>
        field.Equals("period", StringComparison.OrdinalIgnoreCase)
        || field.Equals("month", StringComparison.OrdinalIgnoreCase)
        || field.Equals("year", StringComparison.OrdinalIgnoreCase)
        || field.Equals("mes", StringComparison.OrdinalIgnoreCase)
        || field.Equals("año", StringComparison.OrdinalIgnoreCase)
        || field.Equals("fecha", StringComparison.OrdinalIgnoreCase)
        || field.Equals("date", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrimaryField(string field, SemanticRequest request)
    {
        // Consider primary the first projection or the operation's main output field
        if (request.Projection.Count > 0 && request.Projection[0].Equals(field, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
