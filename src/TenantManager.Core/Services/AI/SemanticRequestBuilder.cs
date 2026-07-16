using System;
using System.Collections.Generic;
using System.Linq;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Converts a raw <see cref="SemanticRequestDto"/> into the typed, immutable <see cref="SemanticRequest"/> record.
/// </summary>
public static class SemanticRequestBuilder
{
    public static SemanticRequest Build(SemanticRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var intent = dto.Intent?.ToLowerInvariant() switch
        {
            "data_query" => SemanticRequestIntent.DataQuery,
            "previous_result_query" => SemanticRequestIntent.PreviousResultQuery,
            _ => SemanticRequestIntent.Unknown
        };

        var outputs = (dto.RequestedOutputs ?? new List<RequestedOutputDto>())
            .Select(o => new RequestedOutput(o.Field ?? string.Empty, o.Label ?? string.Empty))
            .ToArray();

        // Derive presentation from outputs count
        var presentation = outputs.Length > 1
            ? ResponsePresentation.MultiField
            : ResponsePresentation.ValueOnly;

        return new SemanticRequest(
            Language: dto.Language ?? "en",
            Intent: intent,
            Resource: dto.Resource ?? string.Empty,
            Operation: dto.Operation ?? string.Empty,
            Filters: Array.Empty<KeyValuePair<string, string>>(),
            Projection: Array.Empty<string>(),
            Period: new SemanticPeriod(dto.PeriodYear, dto.PeriodMonth),
            RequestedOutputs: outputs,
            Presentation: presentation,
            Confidence: dto.Confidence);
    }
}
