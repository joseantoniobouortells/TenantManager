namespace TenantManager.Core.Services.AI;

/// <summary>
/// Intent of the semantic request from the user.
/// </summary>
public enum SemanticRequestIntent
{
    /// <summary>Standard data query: translate to a SemanticQueryPlan and execute against the DB.</summary>
    DataQuery,
    /// <summary>The user is asking about data from the previous successful result (no new DB query).</summary>
    PreviousResultQuery,
    /// <summary>The LLM could not determine a valid intent.</summary>
    Unknown
}

/// <summary>
/// A requested output field within the same question.
/// For example: "cuánto" maps to "paidAmount" and "qué mes" maps to "period".
/// </summary>
/// <param name="Field">Logical field name as understood by SemanticQueryPlan projections.</param>
/// <param name="Label">Human-readable label to use when formatting the field in the response.</param>
public sealed record RequestedOutput(string Field, string Label);

/// <summary>
/// Semantic period expressed as resolved year/month integers (nulls when not applicable).
/// </summary>
/// <param name="Year">Calendar year, or null.</param>
/// <param name="Month">Calendar month (1-12), or null.</param>
public sealed record SemanticPeriod(int? Year, int? Month)
{
    public static readonly SemanticPeriod Empty = new(null, null);

    public bool HasPeriod => Year.HasValue || Month.HasValue;

    public override string ToString() =>
        (Year, Month) switch
        {
            (int y, int m) => $"{y}-{m:D2}",
            (int y, null) => y.ToString(),
            (null, int m) => $"month {m}",
            _ => string.Empty
        };
}

/// <summary>
/// Preferred presentation style for the assistant's answer.
/// </summary>
public enum ResponsePresentation
{
    /// <summary>Return the primary numeric/text result only.</summary>
    ValueOnly,
    /// <summary>Return all requested outputs listed together.</summary>
    MultiField,
    /// <summary>Return narrative prose.</summary>
    Narrative
}

/// <summary>
/// Immutable high-level semantic request extracted from the user's natural-language question.
/// This is a thin envelope produced by the LLM that feeds into either:
///   (a) the existing SemanticQueryPlan pipeline for DataQuery intents, or
///   (b) the PreviousResultContext resolver for PreviousResultQuery intents.
/// </summary>
/// <param name="Language">ISO 639-1 language code: "es" or "en".</param>
/// <param name="Intent">The resolved intent category.</param>
/// <param name="Resource">Logical resource name (e.g. "payments", "expenses").</param>
/// <param name="Operation">Logical operation name (e.g. "sum", "count").</param>
/// <param name="Filters">Pre-resolved filter key-value pairs (field → value string).</param>
/// <param name="Projection">List of requested projection fields.</param>
/// <param name="Period">Resolved temporal period (year/month).</param>
/// <param name="RequestedOutputs">All outputs explicitly requested within the same question.</param>
/// <param name="Presentation">Preferred answer presentation style.</param>
/// <param name="Confidence">LLM confidence score (0.0–1.0).</param>
public sealed record SemanticRequest(
    string Language,
    SemanticRequestIntent Intent,
    string Resource,
    string Operation,
    System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> Filters,
    System.Collections.Generic.IReadOnlyList<string> Projection,
    SemanticPeriod Period,
    System.Collections.Generic.IReadOnlyList<RequestedOutput> RequestedOutputs,
    ResponsePresentation Presentation,
    decimal Confidence)
{
    /// <summary>Returns an Unknown/empty request used as a null-object fallback.</summary>
    public static SemanticRequest Empty { get; } = new(
        Language: "en",
        Intent: SemanticRequestIntent.Unknown,
        Resource: string.Empty,
        Operation: string.Empty,
        Filters: System.Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>(),
        Projection: System.Array.Empty<string>(),
        Period: SemanticPeriod.Empty,
        RequestedOutputs: System.Array.Empty<RequestedOutput>(),
        Presentation: ResponsePresentation.ValueOnly,
        Confidence: 0m);

    /// <summary>True when the request has a valid intent that can be acted upon.</summary>
    public bool IsActionable =>
        Intent != SemanticRequestIntent.Unknown && Confidence >= 0.5m;
}
