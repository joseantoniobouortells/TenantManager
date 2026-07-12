using System;
using System.Collections.Generic;
using System.Text.Json;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticQueryPlanTests
{
    [Fact]
    public void SemanticQueryPlan_CanBeCreatedAndSerialized()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Language = "es",
            Resource = SemanticQueryResource.Contracts,
            Operation = SemanticQueryOperation.Count,
            Limit = 20,
            Confidence = 0.95,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "active",
                    Operator = SemanticQueryOperator.Equals,
                    Value = true
                }
            },
            Sort = new List<SemanticQuerySort>
            {
                new SemanticQuerySort
                {
                    Field = "effectiveEndDate",
                    Direction = SemanticSortDirection.Asc
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(plan);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("\"language\":\"es\"", json);
        Assert.Contains("\"resource\":\"contracts\"", json);
        Assert.Contains("\"operation\":\"count\"", json);
        Assert.Contains("\"operator\":\"equals\"", json);
        Assert.Contains("\"value\":true", json);
        Assert.Contains("\"direction\":\"asc\"", json);
    }

    [Fact]
    public void SemanticQueryPlan_CanBeDeserialized()
    {
        // Arrange
        var json = @"{
            ""language"": ""es"",
            ""resource"": ""contracts"",
            ""operation"": ""count"",
            ""filters"": [
                {
                    ""field"": ""active"",
                    ""operator"": ""equals"",
                    ""value"": true
                }
            ],
            ""sort"": [
                {
                    ""field"": ""effectiveEndDate"",
                    ""direction"": ""asc""
                }
            ],
            ""limit"": 20,
            ""confidence"": 0.95
        }";

        // Act
        var plan = JsonSerializer.Deserialize<SemanticQueryPlan>(json);

        // Assert
        Assert.NotNull(plan);
        Assert.Equal("es", plan.Language);
        Assert.Equal(SemanticQueryResource.Contracts, plan.Resource);
        Assert.Equal(SemanticQueryOperation.Count, plan.Operation);
        Assert.Equal(20, plan.Limit);
        Assert.Equal(0.95, plan.Confidence);
        
        Assert.Single(plan.Filters);
        Assert.Equal("active", plan.Filters[0].Field);
        Assert.Equal(SemanticQueryOperator.Equals, plan.Filters[0].Operator);
        
        var element = (JsonElement)plan.Filters[0].Value!;
        Assert.True(element.GetBoolean());

        Assert.Single(plan.Sort);
        Assert.Equal("effectiveEndDate", plan.Sort[0].Field);
        Assert.Equal(SemanticSortDirection.Asc, plan.Sort[0].Direction);
    }

    [Fact]
    public void SemanticQueryPlan_JsonRoundTrip_PreservesValues()
    {
        // Arrange
        var original = new SemanticQueryPlan
        {
            Language = "en",
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Sum,
            Limit = 50,
            Confidence = 0.88,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter
                {
                    Field = "expectedAmount",
                    Operator = SemanticQueryOperator.GreaterThanOrEqual,
                    Value = 1500
                }
            },
            Sort = new List<SemanticQuerySort>
            {
                new SemanticQuerySort
                {
                    Field = "tenantName",
                    Direction = SemanticSortDirection.Desc
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SemanticQueryPlan>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Language, deserialized.Language);
        Assert.Equal(original.Resource, deserialized.Resource);
        Assert.Equal(original.Operation, deserialized.Operation);
        Assert.Equal(original.Limit, deserialized.Limit);
        Assert.Equal(original.Confidence, deserialized.Confidence);

        Assert.Single(deserialized.Filters);
        Assert.Equal("expectedAmount", deserialized.Filters[0].Field);
        Assert.Equal(SemanticQueryOperator.GreaterThanOrEqual, deserialized.Filters[0].Operator);
        
        var valElement = (JsonElement)deserialized.Filters[0].Value!;
        Assert.Equal(1500, valElement.GetInt32());

        Assert.Single(deserialized.Sort);
        Assert.Equal("tenantName", deserialized.Sort[0].Field);
        Assert.Equal(SemanticSortDirection.Desc, deserialized.Sort[0].Direction);
    }
}
