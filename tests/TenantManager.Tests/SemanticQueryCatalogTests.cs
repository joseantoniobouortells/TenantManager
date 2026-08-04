using System;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticQueryCatalogTests
{
    [Fact]
    public void Catalog_ContainsAllInitialResources()
    {
        // Assert
        Assert.True(SemanticQueryCatalog.Resources.ContainsKey(SemanticQueryResource.Rooms));
        Assert.True(SemanticQueryCatalog.Resources.ContainsKey(SemanticQueryResource.Tenants));
        Assert.True(SemanticQueryCatalog.Resources.ContainsKey(SemanticQueryResource.Contracts));
        Assert.True(SemanticQueryCatalog.Resources.ContainsKey(SemanticQueryResource.Payments));
        Assert.True(SemanticQueryCatalog.Resources.ContainsKey(SemanticQueryResource.Expenses));
        Assert.True(SemanticQueryCatalog.Resources.ContainsKey(SemanticQueryResource.Dashboard));
        Assert.Equal(6, SemanticQueryCatalog.Resources.Count);
    }

    [Theory]
    [InlineData(SemanticQueryResource.Rooms, SemanticQueryOperation.Count, SemanticQueryOperation.List)]
    [InlineData(SemanticQueryResource.Tenants, SemanticQueryOperation.Count, SemanticQueryOperation.List, SemanticQueryOperation.Lookup)]
    [InlineData(SemanticQueryResource.Contracts, SemanticQueryOperation.Count, SemanticQueryOperation.List)]
    [InlineData(SemanticQueryResource.Payments, SemanticQueryOperation.Count, SemanticQueryOperation.List, SemanticQueryOperation.Sum)]
    [InlineData(SemanticQueryResource.Expenses, SemanticQueryOperation.Count, SemanticQueryOperation.List, SemanticQueryOperation.Sum)]
    [InlineData(SemanticQueryResource.Dashboard, SemanticQueryOperation.Summary)]
    public void Resource_DefinesAllowedOperations(SemanticQueryResource resource, params SemanticQueryOperation[] expectedOps)
    {
        // Arrange
        var definition = SemanticQueryCatalog.Resources[resource];

        // Assert
        Assert.Equal(expectedOps.Length, definition.AllowedOperations.Count);
        foreach (var op in expectedOps)
        {
            Assert.Contains(op, definition.AllowedOperations);
        }
    }

    [Fact]
    public void Resource_DefinesExpectedSemanticFields()
    {
        // Arrange
        var payments = SemanticQueryCatalog.Resources[SemanticQueryResource.Payments];

        // Assert
        Assert.True(payments.Fields.ContainsKey("status"));
        Assert.True(payments.Fields.ContainsKey("year"));
        Assert.True(payments.Fields.ContainsKey("month"));
        Assert.True(payments.Fields.ContainsKey("expectedAmount"));
        Assert.True(payments.Fields.ContainsKey("paidAmount"));
        Assert.True(payments.Fields.ContainsKey("tenantName"));
        Assert.True(payments.Fields.ContainsKey("pending"));
        Assert.True(payments.Fields.ContainsKey("late"));
    }

    [Fact]
    public void Fields_DefineCompatibleOperators()
    {
        // Arrange
        var payments = SemanticQueryCatalog.Resources[SemanticQueryResource.Payments];
        var statusField = payments.Fields["status"];
        var yearField = payments.Fields["year"];
        var pendingField = payments.Fields["pending"];

        // Assert for status (enum)
        Assert.Contains(SemanticQueryOperator.Equals, statusField.AllowedOperators);
        Assert.Contains(SemanticQueryOperator.NotEquals, statusField.AllowedOperators);
        Assert.Contains(SemanticQueryOperator.In, statusField.AllowedOperators);
        Assert.DoesNotContain(SemanticQueryOperator.GreaterThan, statusField.AllowedOperators);

        // Assert for year (int / numeric)
        Assert.Contains(SemanticQueryOperator.Equals, yearField.AllowedOperators);
        Assert.Contains(SemanticQueryOperator.GreaterThan, yearField.AllowedOperators);
        Assert.Contains(SemanticQueryOperator.LessThan, yearField.AllowedOperators);
        Assert.Contains(SemanticQueryOperator.Between, yearField.AllowedOperators);

        // Assert for pending (bool)
        Assert.Contains(SemanticQueryOperator.Equals, pendingField.AllowedOperators);
        Assert.Contains(SemanticQueryOperator.NotEquals, pendingField.AllowedOperators);
        Assert.DoesNotContain(SemanticQueryOperator.Contains, pendingField.AllowedOperators);
        Assert.DoesNotContain(SemanticQueryOperator.Between, pendingField.AllowedOperators);
    }
}
