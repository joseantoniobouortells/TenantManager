using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Supported semantic query resource types in the catalog.
/// </summary>
[JsonConverter(typeof(SemanticQueryEnumConverter<SemanticQueryResource>))]
public enum SemanticQueryResource
{
    Rooms,
    Tenants,
    Contracts,
    Payments,
    Expenses,
    Dashboard
}

/// <summary>
/// Supported operations for a semantic query resource.
/// </summary>
[JsonConverter(typeof(SemanticQueryEnumConverter<SemanticQueryOperation>))]
public enum SemanticQueryOperation
{
    Count,
    List,
    Lookup,
    Sum,
    Summary
}

/// <summary>
/// Allowed comparison and matching operators for semantic filters.
/// </summary>
[JsonConverter(typeof(SemanticQueryEnumConverter<SemanticQueryOperator>))]
public enum SemanticQueryOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    In,
    Between
}

/// <summary>
/// Sort direction for query results.
/// </summary>
[JsonConverter(typeof(SemanticQueryEnumConverter<SemanticSortDirection>))]
public enum SemanticSortDirection
{
    Asc,
    Desc
}

/// <summary>
/// A generic converter that serializes enums using snake_case and deserializes case-insensitively.
/// </summary>
public class SemanticQueryEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (string.IsNullOrWhiteSpace(str))
        {
            return default;
        }
        
        var normalized = str.Replace("_", "").ToLowerInvariant();
        foreach (T val in Enum.GetValues<T>())
        {
            if (val.ToString().ToLowerInvariant() == normalized)
            {
                return val;
            }
        }
        
        throw new JsonException($"Unable to convert \"{str}\" to enum {typeof(T).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var str = value.ToString();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < str.Length; i++)
        {
            if (i > 0 && char.IsUpper(str[i]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(str[i]));
        }
        writer.WriteStringValue(sb.ToString());
    }
}

/// <summary>
/// Represents a single filter condition applied to a resource field.
/// </summary>
public class SemanticQueryFilter
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public SemanticQueryOperator Operator { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

/// <summary>
/// Defines sorting criteria for semantic query results.
/// </summary>
public class SemanticQuerySort
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public SemanticSortDirection Direction { get; set; } = SemanticSortDirection.Asc;
}

/// <summary>
/// Represents a validated semantic query plan created from natural language.
/// </summary>
public class SemanticQueryPlan
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("resource")]
    public SemanticQueryResource? Resource { get; set; }

    [JsonPropertyName("operation")]
    public SemanticQueryOperation? Operation { get; set; }

    [JsonPropertyName("filters")]
    public List<SemanticQueryFilter> Filters { get; set; } = new();

    [JsonPropertyName("projection")]
    public List<string> Projection { get; set; } = new();

    [JsonPropertyName("sort")]
    public List<SemanticQuerySort> Sort { get; set; } = new();

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 20;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.0;
}
