using System;
using System.Collections.Generic;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticRequestTests
{
    // ---- SemanticRequest contract ----

    [Fact]
    public void SemanticRequest_Empty_HasUnknownIntent()
    {
        Assert.Equal(SemanticRequestIntent.Unknown, SemanticRequest.Empty.Intent);
        Assert.False(SemanticRequest.Empty.IsActionable);
    }

    [Fact]
    public void SemanticRequest_DataQuery_IsActionable_WhenConfidenceHigh()
    {
        var req = new SemanticRequest(
            Language: "es",
            Intent: SemanticRequestIntent.DataQuery,
            Resource: "payments",
            Operation: "sum",
            Filters: Array.Empty<KeyValuePair<string, string>>(),
            Projection: new[] { "paidAmount" },
            Period: new SemanticPeriod(2026, 6),
            RequestedOutputs: new[] { new RequestedOutput("paidAmount", "Importe") },
            Presentation: ResponsePresentation.ValueOnly,
            Confidence: 0.95m);

        Assert.True(req.IsActionable);
    }

    [Fact]
    public void SemanticPeriod_ToString_YearMonth()
    {
        var p = new SemanticPeriod(2026, 6);
        Assert.Equal("2026-06", p.ToString());
    }

    [Fact]
    public void SemanticPeriod_ToString_YearOnly()
    {
        var p = new SemanticPeriod(2026, null);
        Assert.Equal("2026", p.ToString());
    }

    [Fact]
    public void SemanticPeriod_ToString_MonthOnly()
    {
        var p = new SemanticPeriod(null, 6);
        Assert.Equal("month 6", p.ToString());
    }

    // ---- SemanticRequestBuilder ----

    [Fact]
    public void SemanticRequestBuilder_Build_DataQueryIntent()
    {
        var dto = new SemanticRequestDto
        {
            Language = "es",
            Intent = "data_query",
            Resource = "payments",
            Operation = "sum",
            PeriodYear = 2026,
            PeriodMonth = 6,
            RequestedOutputs = new List<RequestedOutputDto>
            {
                new() { Field = "paidAmount", Label = "Importe" }
            },
            Confidence = 0.95m
        };

        var req = SemanticRequestBuilder.Build(dto);

        Assert.Equal(SemanticRequestIntent.DataQuery, req.Intent);
        Assert.Equal("es", req.Language);
        Assert.Equal(2026, req.Period.Year);
        Assert.Equal(6, req.Period.Month);
        Assert.Single(req.RequestedOutputs);
        Assert.Equal("paidAmount", req.RequestedOutputs[0].Field);
        Assert.Equal(ResponsePresentation.ValueOnly, req.Presentation);
    }

    [Fact]
    public void SemanticRequestBuilder_Build_MultiOutput_SetsMultiFieldPresentation()
    {
        var dto = new SemanticRequestDto
        {
            Language = "es",
            Intent = "data_query",
            Resource = "payments",
            Operation = "sum",
            PeriodYear = 2026,
            PeriodMonth = 6,
            RequestedOutputs = new List<RequestedOutputDto>
            {
                new() { Field = "paidAmount", Label = "Importe" },
                new() { Field = "period", Label = "Mes" }
            },
            Confidence = 0.9m
        };

        var req = SemanticRequestBuilder.Build(dto);

        Assert.Equal(ResponsePresentation.MultiField, req.Presentation);
        Assert.Equal(2, req.RequestedOutputs.Count);
    }

    [Fact]
    public void SemanticRequestBuilder_Build_PreviousResultQueryIntent()
    {
        var dto = new SemanticRequestDto
        {
            Language = "es",
            Intent = "previous_result_query",
            Confidence = 0.9m
        };

        var req = SemanticRequestBuilder.Build(dto);
        Assert.Equal(SemanticRequestIntent.PreviousResultQuery, req.Intent);
    }

    [Fact]
    public void SemanticRequestBuilder_Build_UnknownIntent_WhenUnrecognized()
    {
        var dto = new SemanticRequestDto { Intent = "something_weird", Confidence = 0.5m };
        var req = SemanticRequestBuilder.Build(dto);
        Assert.Equal(SemanticRequestIntent.Unknown, req.Intent);
    }

    // ---- SemanticRequestResolver ----

    [Fact]
    public void Resolver_TryResolvePreviousResult_ReturnsNull_WhenNotPreviousResultQuery()
    {
        var req = SemanticRequest.Empty;
        var result = SemanticRequestResolver.TryResolvePreviousResult(req, context: null);
        Assert.Null(result);
    }

    [Fact]
    public void Resolver_TryResolvePreviousResult_ReturnsNoContext_WhenContextEmpty()
    {
        var req = new SemanticRequest(
            "es", SemanticRequestIntent.PreviousResultQuery, "", "",
            Array.Empty<KeyValuePair<string, string>>(),
            Array.Empty<string>(),
            SemanticPeriod.Empty,
            new[] { new RequestedOutput("period", "Mes") },
            ResponsePresentation.ValueOnly, 0.9m);

        var result = SemanticRequestResolver.TryResolvePreviousResult(req, context: null);
        Assert.Contains("No tengo", result);
    }

    [Fact]
    public void Resolver_TryResolvePreviousResult_ReturnsPeriod_WhenContextHasYearMonth()
    {
        var req = new SemanticRequest(
            "es", SemanticRequestIntent.PreviousResultQuery, "", "",
            Array.Empty<KeyValuePair<string, string>>(),
            new[] { "period" },
            SemanticPeriod.Empty,
            new[] { new RequestedOutput("period", "Mes") },
            ResponsePresentation.ValueOnly, 0.9m);

        var ctx = new AssistantContext
        {
            LastResolvedIntent = "payments_sum",
            LastYear = 2026,
            LastMonth = 6,
            LastResource = "payments",
            LastLanguage = "es"
        };

        var result = SemanticRequestResolver.TryResolvePreviousResult(req, ctx);
        Assert.NotNull(result);
        Assert.Contains("junio", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", result);
    }

    [Fact]
    public void Resolver_TryResolvePreviousResult_ReturnsPeriod_English()
    {
        var req = new SemanticRequest(
            "en", SemanticRequestIntent.PreviousResultQuery, "", "",
            Array.Empty<KeyValuePair<string, string>>(),
            new[] { "period" },
            SemanticPeriod.Empty,
            new[] { new RequestedOutput("period", "Month") },
            ResponsePresentation.ValueOnly, 0.9m);

        var ctx = new AssistantContext
        {
            LastResolvedIntent = "payments_sum",
            LastYear = 2026,
            LastMonth = 6,
            LastResource = "payments",
            LastLanguage = "en"
        };

        var result = SemanticRequestResolver.TryResolvePreviousResult(req, ctx);
        Assert.NotNull(result);
        Assert.Contains("June", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", result);
    }

    // ---- Keyword resolver ----

    [Theory]
    [InlineData("a qué mes corresponde", true)]
    [InlineData("A qué mes corresponde?", true)]
    [InlineData("¿a qué mes corresponde?", true)]
    [InlineData("qué mes era", true)]
    [InlineData("what month was that", false)]
    [InlineData("which month", false)]
    public void Resolver_KeywordHeuristic_DetectsPeriodQuery(string message, bool isSpanish)
    {
        var ctx = new AssistantContext
        {
            LastResolvedIntent = "payments_sum",
            LastYear = 2026,
            LastMonth = 6,
            LastLanguage = isSpanish ? "es" : "en"
        };

        var result = SemanticRequestResolver.TryResolvePreviousResultByKeywords(message, ctx, isSpanish);
        Assert.NotNull(result);
        Assert.Contains("2026", result);
    }

    [Fact]
    public void Resolver_KeywordHeuristic_ReturnsNull_WhenNotPeriodQuery()
    {
        var ctx = new AssistantContext
        {
            LastResolvedIntent = "payments_sum",
            LastYear = 2026,
            LastMonth = 6
        };

        var result = SemanticRequestResolver.TryResolvePreviousResultByKeywords(
            "cuánto se ha ingresado este mes", ctx, isSpanish: true);
        // This question is NOT a period meta-query, it's a data query
        Assert.Null(result);
    }

    [Fact]
    public void Resolver_KeywordHeuristic_ReturnsNull_WhenNoContext()
    {
        var ctx = new AssistantContext(); // no context
        var result = SemanticRequestResolver.TryResolvePreviousResultByKeywords(
            "a qué mes corresponde", ctx, isSpanish: true);
        Assert.Null(result);
    }

    // ---- EnrichFormattedAnswer ----

    [Fact]
    public void Resolver_EnrichFormattedAnswer_AppendsMonthLabel_WhenPeriodRequested()
    {
        var req = new SemanticRequest(
            "es", SemanticRequestIntent.DataQuery, "payments", "sum",
            Array.Empty<KeyValuePair<string, string>>(),
            new[] { "paidAmount", "period" },
            new SemanticPeriod(2026, 6),
            new[]
            {
                new RequestedOutput("paidAmount", "Importe"),
                new RequestedOutput("period", "Mes")
            },
            ResponsePresentation.MultiField, 0.95m);

        var ctx = new AssistantContext
        {
            LastResolvedIntent = "payments_sum",
            LastYear = 2026,
            LastMonth = 6
        };

        var enriched = SemanticRequestResolver.EnrichFormattedAnswer(
            "Se han ingresado 540,00 €.", req, ctx);

        Assert.Contains("540", enriched);
        Assert.Contains("junio", enriched, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", enriched);
    }
}
