using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Result of a SemanticQueryPlan validation.
/// </summary>
public class SemanticValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Validator for SemanticQueryPlan objects.
/// Ensures security, correctness, type compatibility, and injects property scopes.
/// </summary>
public static class SemanticQueryPlanValidator
{
    public static SemanticValidationResult Validate(
        SemanticQueryPlan plan, 
        int activePropertyId, 
        double minConfidence = 0.6)
    {
        if (plan == null)
        {
            return new SemanticValidationResult { IsValid = false, ErrorMessage = "Plan is null" };
        }

        if (plan.Confidence < minConfidence)
        {
            return new SemanticValidationResult { IsValid = false, ErrorMessage = "low confidence" };
        }

        if (plan.Limit <= 0 || plan.Limit > 50)
        {
            return new SemanticValidationResult { IsValid = false, ErrorMessage = "limit exceeded" };
        }

        if (!SemanticQueryCatalog.Resources.TryGetValue(plan.Resource, out var resourceDef))
        {
            return new SemanticValidationResult { IsValid = false, ErrorMessage = "unknown resource" };
        }

        if (!resourceDef.AllowedOperations.Contains(plan.Operation))
        {
            return new SemanticValidationResult { IsValid = false, ErrorMessage = "unsupported operation" };
        }

        // Security boundary: the plan must not contain a user-supplied propertyId filter
        if (plan.Filters.Any(f => f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase)))
        {
            return new SemanticValidationResult { IsValid = false, ErrorMessage = "propertyId filter is not allowed" };
        }

        foreach (var filter in plan.Filters)
        {
            if (!resourceDef.Fields.TryGetValue(filter.Field, out var fieldDef))
            {
                return new SemanticValidationResult { IsValid = false, ErrorMessage = "unknown field" };
            }

            if (!fieldDef.AllowedOperators.Contains(filter.Operator))
            {
                return new SemanticValidationResult { IsValid = false, ErrorMessage = "unsupported operator" };
            }

            // Validate value types
            if (filter.Value != null)
            {
                if (!ValidateValueType(filter.Value, fieldDef.Type, fieldDef.Name))
                {
                    return new SemanticValidationResult { IsValid = false, ErrorMessage = "invalid value" };
                }
            }
        }

        // Inject active property scope
        plan.Filters.Add(new SemanticQueryFilter
        {
            Field = "propertyId",
            Operator = SemanticQueryOperator.Equals,
            Value = activePropertyId
        });

        return new SemanticValidationResult { IsValid = true };
    }

    private static bool ValidateValueType(object value, Type expectedType, string fieldName)
    {
        // Extract array items recursively
        if (value is JsonElement arrayElement && arrayElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (!ValidateValueType(item, expectedType, fieldName))
                {
                    return false;
                }
            }
            return true;
        }

        // Extract raw value if it is a single JsonElement
        object? rawValue = value;
        if (value is JsonElement jsonElement)
        {
            rawValue = GetJsonElementValue(jsonElement, expectedType);
            if (rawValue == null)
            {
                return false;
            }
        }

        // Enforce enum values for status
        if (fieldName.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var strVal = rawValue?.ToString();
            if (strVal == null) return false;
            var validValues = new[] { "pending", "paid", "late", "partial" };
            return validValues.Contains(strVal.ToLowerInvariant());
        }

        if (rawValue is string dynamicStr && (dynamicStr.Equals("current", StringComparison.OrdinalIgnoreCase) || dynamicStr.Equals("hoy", StringComparison.OrdinalIgnoreCase) || dynamicStr.Equals("ahora", StringComparison.OrdinalIgnoreCase)))
        {
            if (expectedType == typeof(int) || expectedType == typeof(DateTimeOffset))
            {
                return true;
            }
        }

        try
        {
            if (expectedType == typeof(bool))
            {
                if (rawValue is bool) return true;
                Convert.ToBoolean(rawValue);
                return true;
            }
            if (expectedType == typeof(int))
            {
                if (rawValue is int) return true;
                Convert.ToInt32(rawValue);
                return true;
            }
            if (expectedType == typeof(decimal))
            {
                if (rawValue is decimal || rawValue is double || rawValue is float || rawValue is int) return true;
                Convert.ToDecimal(rawValue);
                return true;
            }
            if (expectedType == typeof(DateTimeOffset))
            {
                if (rawValue is DateTimeOffset || rawValue is DateTime) return true;
                var str = rawValue?.ToString();
                if (string.IsNullOrWhiteSpace(str)) return false;
                return DateTimeOffset.TryParse(str, out _) || DateTime.TryParse(str, out _);
            }
            if (expectedType == typeof(string))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static object? GetJsonElementValue(JsonElement element, Type targetType)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (targetType == typeof(int))
                {
                    if (element.TryGetInt32(out int i)) return i;
                }
                else if (targetType == typeof(decimal))
                {
                    if (element.TryGetDecimal(out decimal d)) return d;
                }
                return element.GetDouble();
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Null:
                return null;
            default:
                return null;
        }
    }
}
