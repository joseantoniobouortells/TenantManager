using System;
using System.Collections.Generic;

namespace TenantManager.Evaluation;

public class EvaluationScenario
{
    public string Id { get; set; } = "";
    public string Language { get; set; } = "en";
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public string? ReferenceDate { get; set; }
    public List<ScenarioMessage> Messages { get; set; } = new();
}

public class ScenarioMessage
{
    public string Text { get; set; } = "";
    public ExpectedOutcome Expected { get; set; } = new();
}

public class ExpectedOutcome
{
    public string? Intent { get; set; }
    public string? Resource { get; set; }
    public string? Operation { get; set; }
    public List<string>? Projection { get; set; }
    public List<string>? RequestedOutputs { get; set; }
    public int? ResolvedYear { get; set; }
    public int? ResolvedMonth { get; set; }
    public string? ResolvedStartDate { get; set; }
    public string? ResolvedEndDate { get; set; }
    public string? QueryExecution { get; set; }
    public List<string>? AnswerContains { get; set; }
    public List<string>? AnswerNotContains { get; set; }
}
