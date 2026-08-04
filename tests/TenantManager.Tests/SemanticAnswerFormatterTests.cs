using System;
using System.Collections.Generic;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticAnswerFormatterTests
{
    [Fact]
    public void Format_CountAnswerInSpanish_Positive()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "late", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        // Act
        var result = SemanticAnswerFormatter.Format(plan, 3, "es");

        // Assert
        Assert.Equal("Hay 3 pagos con retraso.", result);
    }

    [Fact]
    public void Format_CountAnswerInEnglish_Positive()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "late", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        // Act
        var result = SemanticAnswerFormatter.Format(plan, 3, "en");

        // Assert
        Assert.Equal("There are 3 late payments.", result);
    }

    [Fact]
    public void Format_CountAnswerInSpanish_Zero()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "late", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        // Act
        var result = SemanticAnswerFormatter.Format(plan, 0, "es");

        // Assert
        Assert.Equal("No hay pagos con retraso.", result);
    }

    [Fact]
    public void Format_ListAnswerInSpanish()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "available", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };
        var list = new List<SemanticRoomResult>
        {
            new SemanticRoomResult { Name = "Habitación 1", Available = true },
            new SemanticRoomResult { Name = "Habitación 2", Available = true }
        };

        // Act
        var result = SemanticAnswerFormatter.Format(plan, list, "es");

        // Assert
        Assert.Contains("Habitación 1", result);
        Assert.Contains("Habitación 2", result);
        Assert.Contains("libres", result);
    }

    [Fact]
    public void Format_SumAnswerInSpanish()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Sum,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "pending", Operator = SemanticQueryOperator.Equals, Value = true },
                new SemanticQueryFilter { Field = "month", Operator = SemanticQueryOperator.Equals, Value = "current" }
            }
        };

        // Act
        var result = SemanticAnswerFormatter.Format(plan, 800.00m, "es");

        // Assert
        Assert.Contains("800,00", result); // format 800.00 with N2
        Assert.Contains("pagos pendientes", result);
    }

    [Fact]
    public void Format_NoDataAnswer()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List
        };

        // Act
        var result = SemanticAnswerFormatter.Format(plan, new List<SemanticRoomResult>(), "es");

        // Assert
        Assert.Equal("No se encontraron registros de habitaciones que coincidan con los criterios.", result);
    }

    [Fact]
    public void FormatValidationError_Spanish()
    {
        // Arrange
        var validationResult = new SemanticValidationResult
        {
            IsValid = false,
            ErrorMessage = "low confidence"
        };

        // Act
        var result = SemanticAnswerFormatter.FormatValidationError(validationResult, "es");

        // Assert
        Assert.Contains("suficiente confianza", result);
    }
}
