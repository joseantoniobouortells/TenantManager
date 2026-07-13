using System;
using System.Collections.Generic;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Definition of a semantic field, including its name, C# data type, and allowed query operators.
/// </summary>
public class SemanticFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(string);
    public HashSet<SemanticQueryOperator> AllowedOperators { get; set; } = new();
}

/// <summary>
/// Definition of a semantic resource, its allowed operations, and its fields.
/// </summary>
public class SemanticResourceDefinition
{
    public SemanticQueryResource Resource { get; set; }
    public HashSet<SemanticQueryOperation> AllowedOperations { get; set; } = new();
    public Dictionary<string, SemanticFieldDefinition> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The allowlist catalog of resources, fields, operations, and operators for the Semantic Query Planner.
/// </summary>
public static class SemanticQueryCatalog
{
    public static IReadOnlyDictionary<SemanticQueryResource, SemanticResourceDefinition> Resources { get; }

    static SemanticQueryCatalog()
    {
        var resources = new Dictionary<SemanticQueryResource, SemanticResourceDefinition>();

        // Operators groups for reuse
        var boolOperators = new HashSet<SemanticQueryOperator>
        {
            SemanticQueryOperator.Equals,
            SemanticQueryOperator.NotEquals
        };

        var stringOperators = new HashSet<SemanticQueryOperator>
        {
            SemanticQueryOperator.Equals,
            SemanticQueryOperator.NotEquals,
            SemanticQueryOperator.Contains,
            SemanticQueryOperator.In
        };

        var numericAndDateOperators = new HashSet<SemanticQueryOperator>
        {
            SemanticQueryOperator.Equals,
            SemanticQueryOperator.NotEquals,
            SemanticQueryOperator.GreaterThan,
            SemanticQueryOperator.GreaterThanOrEqual,
            SemanticQueryOperator.LessThan,
            SemanticQueryOperator.LessThanOrEqual,
            SemanticQueryOperator.In,
            SemanticQueryOperator.Between
        };

        var enumOperators = new HashSet<SemanticQueryOperator>
        {
            SemanticQueryOperator.Equals,
            SemanticQueryOperator.NotEquals,
            SemanticQueryOperator.In
        };

        // 1. Rooms
        resources[SemanticQueryResource.Rooms] = new SemanticResourceDefinition
        {
            Resource = SemanticQueryResource.Rooms,
            AllowedOperations = { SemanticQueryOperation.Count, SemanticQueryOperation.List },
            Fields =
            {
                ["active"] = new SemanticFieldDefinition { Name = "active", Type = typeof(bool), AllowedOperators = boolOperators },
                ["occupied"] = new SemanticFieldDefinition { Name = "occupied", Type = typeof(bool), AllowedOperators = boolOperators },
                ["available"] = new SemanticFieldDefinition { Name = "available", Type = typeof(bool), AllowedOperators = boolOperators },
                ["currentRent"] = new SemanticFieldDefinition { Name = "currentRent", Type = typeof(decimal), AllowedOperators = numericAndDateOperators },
                ["name"] = new SemanticFieldDefinition { Name = "name", Type = typeof(string), AllowedOperators = stringOperators }
            }
        };

        // 2. Tenants
        resources[SemanticQueryResource.Tenants] = new SemanticResourceDefinition
        {
            Resource = SemanticQueryResource.Tenants,
            AllowedOperations = { SemanticQueryOperation.Count, SemanticQueryOperation.List, SemanticQueryOperation.Lookup },
            Fields =
            {
                ["active"] = new SemanticFieldDefinition { Name = "active", Type = typeof(bool), AllowedOperators = boolOperators },
                ["fullName"] = new SemanticFieldDefinition { Name = "fullName", Type = typeof(string), AllowedOperators = stringOperators },
                ["currentRoom"] = new SemanticFieldDefinition { Name = "currentRoom", Type = typeof(string), AllowedOperators = stringOperators },
                ["moveInDate"] = new SemanticFieldDefinition { Name = "moveInDate", Type = typeof(DateTimeOffset), AllowedOperators = numericAndDateOperators },
                ["effectiveMoveOutDate"] = new SemanticFieldDefinition { Name = "effectiveMoveOutDate", Type = typeof(DateTimeOffset), AllowedOperators = numericAndDateOperators }
            }
        };

        // 3. Contracts
        resources[SemanticQueryResource.Contracts] = new SemanticResourceDefinition
        {
            Resource = SemanticQueryResource.Contracts,
            AllowedOperations = { SemanticQueryOperation.Count, SemanticQueryOperation.List },
            Fields =
            {
                ["active"] = new SemanticFieldDefinition { Name = "active", Type = typeof(bool), AllowedOperators = boolOperators },
                ["tenantName"] = new SemanticFieldDefinition { Name = "tenantName", Type = typeof(string), AllowedOperators = stringOperators },
                ["roomName"] = new SemanticFieldDefinition { Name = "roomName", Type = typeof(string), AllowedOperators = stringOperators },
                ["startDate"] = new SemanticFieldDefinition { Name = "startDate", Type = typeof(DateTimeOffset), AllowedOperators = numericAndDateOperators },
                ["baseEndDate"] = new SemanticFieldDefinition { Name = "baseEndDate", Type = typeof(DateTimeOffset), AllowedOperators = numericAndDateOperators },
                ["effectiveEndDate"] = new SemanticFieldDefinition { Name = "effectiveEndDate", Type = typeof(DateTimeOffset), AllowedOperators = numericAndDateOperators },
                ["hasExtensions"] = new SemanticFieldDefinition { Name = "hasExtensions", Type = typeof(bool), AllowedOperators = boolOperators },
                ["missingFile"] = new SemanticFieldDefinition { Name = "missingFile", Type = typeof(bool), AllowedOperators = boolOperators }
            }
        };

        // 4. Payments
        resources[SemanticQueryResource.Payments] = new SemanticResourceDefinition
        {
            Resource = SemanticQueryResource.Payments,
            AllowedOperations = { SemanticQueryOperation.Count, SemanticQueryOperation.List, SemanticQueryOperation.Sum },
            Fields =
            {
                ["status"] = new SemanticFieldDefinition { Name = "status", Type = typeof(string), AllowedOperators = enumOperators },
                ["year"] = new SemanticFieldDefinition { Name = "year", Type = typeof(int), AllowedOperators = numericAndDateOperators },
                ["month"] = new SemanticFieldDefinition { Name = "month", Type = typeof(int), AllowedOperators = numericAndDateOperators },
                ["expectedAmount"] = new SemanticFieldDefinition { Name = "expectedAmount", Type = typeof(decimal), AllowedOperators = numericAndDateOperators },
                ["paidAmount"] = new SemanticFieldDefinition { Name = "paidAmount", Type = typeof(decimal), AllowedOperators = numericAndDateOperators },
                ["tenantName"] = new SemanticFieldDefinition { Name = "tenantName", Type = typeof(string), AllowedOperators = stringOperators },
                ["pending"] = new SemanticFieldDefinition { Name = "pending", Type = typeof(bool), AllowedOperators = boolOperators },
                ["late"] = new SemanticFieldDefinition { Name = "late", Type = typeof(bool), AllowedOperators = boolOperators }
            }
        };

        // 5. Expenses
        resources[SemanticQueryResource.Expenses] = new SemanticResourceDefinition
        {
            Resource = SemanticQueryResource.Expenses,
            AllowedOperations = { SemanticQueryOperation.Count, SemanticQueryOperation.List, SemanticQueryOperation.Sum },
            Fields =
            {
                ["category"] = new SemanticFieldDefinition { Name = "category", Type = typeof(string), AllowedOperators = stringOperators },
                ["amount"] = new SemanticFieldDefinition { Name = "amount", Type = typeof(decimal), AllowedOperators = numericAndDateOperators },
                ["date"] = new SemanticFieldDefinition { Name = "date", Type = typeof(DateTimeOffset), AllowedOperators = numericAndDateOperators }
            }
        };

        // 6. Dashboard
        resources[SemanticQueryResource.Dashboard] = new SemanticResourceDefinition
        {
            Resource = SemanticQueryResource.Dashboard,
            AllowedOperations = { SemanticQueryOperation.Summary },
            Fields =
            {
                ["year"] = new SemanticFieldDefinition { Name = "year", Type = typeof(int), AllowedOperators = numericAndDateOperators },
                ["month"] = new SemanticFieldDefinition { Name = "month", Type = typeof(int), AllowedOperators = numericAndDateOperators }
            }
        };

        Resources = resources;
    }
}
