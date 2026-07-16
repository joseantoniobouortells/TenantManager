using System.Linq;
using Xunit;
using TenantManager.Evaluation;
using TenantManager.Core.Services.AI;
using System.Collections.Generic;

namespace TenantManager.Evaluation.Tests;

public class EvaluatorTests
{
    [Fact]
    public void AssertOutcome_WithMatchingData_ReturnsEmptyErrors()
    {
        var expected = new ExpectedOutcome
        {
            Resource = "rooms",
            Operation = "count",
            QueryExecution = "required",
            ResolvedYear = 2026,
            ResolvedMonth = 7,
            AnswerContains = new List<string> { "5" }
        };

        var observer = new ExecutionObserver
        {
            LastPlan = new SemanticQueryPlan { Resource = SemanticQueryResource.Rooms, Operation = SemanticQueryOperation.Count },
            QueryExecuted = true,
            ResolvedYear = 2026,
            ResolvedMonth = 7
        };

        var answer = "There are 5 rooms.";

        var errors = Evaluator.AssertOutcome(expected, observer, answer);

        Assert.Empty(errors);
    }

    [Fact]
    public void AssertOutcome_WithMismatchData_ReturnsErrors()
    {
        var expected = new ExpectedOutcome
        {
            Resource = "tenants",
            QueryExecution = "forbidden",
            AnswerNotContains = new List<string> { "error" }
        };

        var observer = new ExecutionObserver
        {
            LastPlan = new SemanticQueryPlan { Resource = SemanticQueryResource.Rooms }, // Mismatch
            QueryExecuted = true // Mismatch
        };

        var answer = "There was an error processing."; // Mismatch

        var errors = Evaluator.AssertOutcome(expected, observer, answer);

        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, e => e.Contains("Expected resource 'tenants'"));
        Assert.Contains(errors, e => e.Contains("Expected query execution to be forbidden"));
        Assert.Contains(errors, e => e.Contains("Expected answer NOT to contain 'error'"));
    }
}
