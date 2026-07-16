using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Raw JSON-deserialized DTO produced by the LLM for a SemanticRequest.
/// Converted to the immutable SemanticRequest record by SemanticRequestBuilder.
/// </summary>
public class SemanticRequestDto
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "unknown";

    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("period_year")]
    public int? PeriodYear { get; set; }

    [JsonPropertyName("period_month")]
    public int? PeriodMonth { get; set; }

    [JsonPropertyName("requested_outputs")]
    public List<RequestedOutputDto> RequestedOutputs { get; set; } = new();

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; } = 0m;
}

public class RequestedOutputDto
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}
