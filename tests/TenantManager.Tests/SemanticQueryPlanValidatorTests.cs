using System;
using System.Collections.Generic;
using System.Text.Json;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticQueryPlanValidatorTests
{
    [Fact]
    public void Validator_RejectsNullPlan()
    {
        // Act
        var result = SemanticQueryPlanValidator.Validate(null!, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Plan is null", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsLowConfidencePlan()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.3
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("low confidence", result.ErrorMessage);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validator_RejectsInvalidLimit(int limit)
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Limit = limit
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("limit exceeded", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsUnsupportedOperation()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.Lookup, // Rooms only allows Count, List
            Confidence = 0.9,
            Limit = 20
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("unsupported operation", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsUserSuppliedPropertyIdFilter()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Limit = 20,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "propertyId",
                    Operator = SemanticQueryOperator.Equals,
                    Value = 10
                }
            }
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("propertyId filter is not allowed", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsUnknownField()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Limit = 20,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "creditCard",
                    Operator = SemanticQueryOperator.Equals,
                    Value = "Visa"
                }
            }
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("unknown field", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsUnsupportedOperator()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List,
            Confidence = 0.9,
            Limit = 20,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "active",
                    Operator = SemanticQueryOperator.Contains, // contains is not allowed on bool fields
                    Value = true
                }
            }
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("unsupported operator", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsInvalidValue()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Limit = 20,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "year",
                    Operator = SemanticQueryOperator.Equals,
                    Value = "not-a-year" // Expects integer/convertible
                }
            }
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("invalid value", result.ErrorMessage);
    }

    [Fact]
    public void Validator_RejectsInvalidEnumValueForStatus()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Limit = 20,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "status",
                    Operator = SemanticQueryOperator.Equals,
                    Value = "overdue" // expected: pending, paid, late, partial
                }
            }
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("invalid value", result.ErrorMessage);
    }

    [Fact]
    public void Validator_ValidatesAndInjectsPropertyScope()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.List,
            Confidence = 0.9,
            Limit = 20,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "fullName",
                    Operator = SemanticQueryOperator.Contains,
                    Value = "John"
                }
            }
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 5);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);

        // Verify active property scope is injected
        var propertyIdFilter = plan.Filters.FirstOrDefault(f => f.Field.Equals("propertyId", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(propertyIdFilter);
        Assert.Equal(SemanticQueryOperator.Equals, propertyIdFilter.Operator);
        Assert.Equal(5, propertyIdFilter.Value);
    }
}
