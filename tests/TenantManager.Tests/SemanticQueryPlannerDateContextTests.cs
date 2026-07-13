using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

/// <summary>
/// Focused deterministic tests for the planner date-context injection, native chat API calls,
/// url normalization, output parsing, reasoning item ignore logic, and failure handling.
/// </summary>
public class SemanticQueryPlannerDateContextTests
{
    private class DynamicMockHttpMessageHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public List<string> RequestUrls { get; } = new();
        public List<NativeChatRequest?> ParsedRequests { get; } = new();
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> _responses = new();

        public void QueueResponse(HttpStatusCode statusCode, string content)
        {
            _responses.Enqueue((statusCode, content));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUrls.Add(request.RequestUri?.ToString() ?? string.Empty);
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(body);
                try
                {
                    var parsed = JsonSerializer.Deserialize<NativeChatRequest>(body);
                    ParsedRequests.Add(parsed);
                }
                catch
                {
                    ParsedRequests.Add(null);
                }
            }

            if (_responses.TryDequeue(out var res))
            {
                return new HttpResponseMessage(res.StatusCode)
                {
                    Content = new StringContent(res.Content)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static AppDbContext GetMemoryDbContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new AppDbContext(opts);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static void ConfigureMockSettings(string endpoint = "http://mock/v1/chat/completions")
    {
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = endpoint });
    }

    private static string BuildNativeChatResponse(List<NativeChatResponseOutputItem> outputItems)
    {
        var obj = new { output = outputItems };
        return JsonSerializer.Serialize(obj);
    }

    // -----------------------------------------------------------------------
    // URL Normalization Tests
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("http://localhost:1234", "http://localhost:1234/api/v1/chat")]
    [InlineData("http://localhost:1234/", "http://localhost:1234/api/v1/chat")]
    [InlineData("http://localhost:1234/v1/chat/completions", "http://localhost:1234/api/v1/chat")]
    [InlineData("http://172.20.10.11:1234/v1/chat/completions", "http://172.20.10.11:1234/api/v1/chat")]
    [InlineData("http://my-lm-studio.local:8080/v1/chat/completions", "http://my-lm-studio.local:8080/api/v1/chat")]
    public void EndpointNormalization_ResolvesToNativeChatUrl(string configured, string expected)
    {
        var normalized = LocalAiClient.NormalizeNativeChatEndpoint(configured);
        Assert.Equal(expected, normalized);
    }

    // -----------------------------------------------------------------------
    // Injected Date & Prompt Semantic Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildQueryPlanAsync_InjectsDateAndPlannerPrompt()
    {
        // Arrange
        var fixedDate = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);
        var handler = new DynamicMockHttpMessageHandler();
        var validPlan = "{\"language\":\"es\",\"resource\":\"payments\",\"operation\":\"sum\",\"confidence\":0.95}";
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "message", Content = validPlan }
        }));
        
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        await aiClient.BuildQueryPlanAsync("Cuales han sido los ingresos de este mes?", null, () => fixedDate);

        // Assert
        Assert.Single(handler.Requests);
        var req = handler.ParsedRequests[0];
        Assert.NotNull(req);
        Assert.Contains("2026-07-13", req.SystemPrompt);
        Assert.Contains("year=2026", req.SystemPrompt);
        Assert.Contains("month=7", req.SystemPrompt);
        Assert.Contains("este mes", req.SystemPrompt.ToLowerInvariant());
    }

    // -----------------------------------------------------------------------
    // Native Chat Endpoint & Request Configuration Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildQueryPlanAsync_CallsNativeChatUrlWithCorrectParameters()
    {
        // Arrange
        var handler = new DynamicMockHttpMessageHandler();
        var validPlan = "{\"language\":\"es\",\"resource\":\"payments\",\"operation\":\"sum\",\"confidence\":0.95}";
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "message", Content = validPlan }
        }));

        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings("http://127.0.0.1:1234/v1/chat/completions");

        // Act
        await aiClient.BuildQueryPlanAsync("Question");

        // Assert
        Assert.Single(handler.RequestUrls);
        Assert.Equal("http://127.0.0.1:1234/api/v1/chat", handler.RequestUrls[0]);

        var req = handler.ParsedRequests[0];
        Assert.NotNull(req);
        Assert.Equal("off", req.Reasoning);
        Assert.Equal(512, req.MaxOutputTokens);
        Assert.False(req.Stream);
        Assert.False(req.Store);
        Assert.Equal(0.0, req.Temperature);
    }

    // -----------------------------------------------------------------------
    // Message parsing and reasoning ignore tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildQueryPlanAsync_IgnoresReasoningItems_ExtractsMessageContent()
    {
        // Arrange
        var handler = new DynamicMockHttpMessageHandler();
        var validPlan = "{\"language\":\"es\",\"resource\":\"payments\",\"operation\":\"sum\",\"confidence\":0.95}";
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "reasoning", Content = "<think>Calculating sum...</think>" },
            new NativeChatResponseOutputItem { Type = "message", Content = validPlan }
        }));

        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        var result = await aiClient.BuildQueryPlanAsync("Question");

        // Assert
        Assert.Equal(validPlan, result);
    }

    // -----------------------------------------------------------------------
    // Failure Paths: single request, missing outputs, empty content, invalid JSON
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildQueryPlanAsync_MissingOutput_ReturnsNullWithoutRetry()
    {
        // Arrange
        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, "{}"); // Empty response body, missing "output"
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        var result = await aiClient.BuildQueryPlanAsync("Question");

        // Assert
        Assert.Null(result);
        Assert.Single(handler.Requests); // Only one request is made
    }

    [Fact]
    public async Task BuildQueryPlanAsync_EmptyMessageContent_ReturnsNullWithoutRetry()
    {
        // Arrange
        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "message", Content = "" } // Empty message content
        }));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        var result = await aiClient.BuildQueryPlanAsync("Question");

        // Assert
        Assert.Null(result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task BuildQueryPlanAsync_NoMessageItem_ReturnsNullWithoutRetry()
    {
        // Arrange
        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "reasoning", Content = "Just thinking..." } // No message item at all
        }));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        var result = await aiClient.BuildQueryPlanAsync("Question");

        // Assert
        Assert.Null(result);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ResolveIntent_PlannerFailure_ReturnsPlannerError_DoesNotExecuteDashboardSummary()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "P6" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "message", Content = "{ invalid json }" }
        }));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        var (answer, _) = await service.ResolveIntentAndGetDataAsync(
            "Cuales han sido los ingresos de este mes?", null, property.Id);

        // Assert
        Assert.NotNull(answer);
        Assert.DoesNotContain("Resumen:", answer); // Does not fallback to dashboard summary
        Assert.True(answer.Contains("interpretar") || answer.Contains("interpret") || answer.Contains("error"));
    }

    // -----------------------------------------------------------------------
    // Move-out query for Erik Artigas reaches semantic planner path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveIntent_TenantMoveOutPlan_ResolvesThroughSemanticPlanner()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        var room = new Room { Name = "Room 1", PropertyId = property.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = room.Id,
            PropertyId = property.Id,
            StartDate = new DateTimeOffset(new DateTime(2026, 1, 1)),
            EndDate = new DateTimeOffset(new DateTime(2026, 8, 31))
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var planJson = JsonSerializer.Serialize(new
        {
            language = "es",
            resource = "tenants",
            operation = "lookup",
            filters = new[] { new { field = "fullName", @operator = "contains", value = "Erik Artigas" } },
            projection = Array.Empty<object>(),
            sort = Array.Empty<object>(),
            limit = 20,
            confidence = 0.95
        });

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildNativeChatResponse(new List<NativeChatResponseOutputItem>
        {
            new NativeChatResponseOutputItem { Type = "message", Content = planJson }
        }));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync(
            "¿Cuándo se va Erik Artigas?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.NotNull(answer);
        Assert.Contains("2026-08-31", answer);
    }
}
