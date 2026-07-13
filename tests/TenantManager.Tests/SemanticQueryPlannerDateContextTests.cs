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
/// Focused deterministic tests for the planner date-context injection, planner failure
/// handling, and monthly-income query plan acceptance. No LM Studio calls are made.
/// </summary>
public class SemanticQueryPlannerDateContextTests
{
    // -----------------------------------------------------------------------
    // Infrastructure helpers
    // -----------------------------------------------------------------------

    private class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string? _responseContent;
        public string? LastRequestBody { get; private set; }

        public CapturingHttpMessageHandler(string? responseContent)
        {
            _responseContent = responseContent;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (_responseContent != null)
                response.Content = new StringContent(_responseContent);
            return response;
        }
    }

    private class EmptyContentHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Returns a valid 200 response but with empty string content in the choices message
            var body = """{"choices":[{"message":{"content":""}}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private class InvalidJsonHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = """{"choices":[{"message":{"content":"{ invalid json..."}}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
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

    private static void ConfigureMockSettings(string endpoint = "http://mock")
    {
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = endpoint });
    }

    private static string BuildChoicesResponse(string innerContent)
    {
        var obj = new { choices = new[] { new { message = new { content = innerContent } } } };
        return JsonSerializer.Serialize(obj);
    }

    // -----------------------------------------------------------------------
    // 1. Planner prompt includes injected date, year, and month
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildQueryPlanAsync_InjectsCurrentDateIntoPrompt()
    {
        // Arrange
        var fixedDate = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);
        var handler = new CapturingHttpMessageHandler(BuildChoicesResponse("{}"));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        await aiClient.BuildQueryPlanAsync("test", null, () => fixedDate);

        // Assert: the system prompt sent to LM Studio must contain the injected date
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("2026-07-13", handler.LastRequestBody);
        Assert.Contains("year=2026", handler.LastRequestBody);
        Assert.Contains("month=7", handler.LastRequestBody);
    }

    // -----------------------------------------------------------------------
    // 2. "This month" can be represented using the injected period
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BuildQueryPlanAsync_PromptExplainsThisMonthSemantics()
    {
        // Arrange
        var fixedDate = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);
        var handler = new CapturingHttpMessageHandler(BuildChoicesResponse("{}"));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        // Act
        await aiClient.BuildQueryPlanAsync("Cuales han sido los ingresos de este mes?", null, () => fixedDate);

        // Assert: the prompt must define relative-date semantics using the injected period
        Assert.NotNull(handler.LastRequestBody);
        // The prompt explains that 'este mes' maps to the concrete year and month
        Assert.Contains("este mes", handler.LastRequestBody!.ToLowerInvariant());
        // And the concrete values are embedded
        Assert.Contains("year=2026", handler.LastRequestBody);
        Assert.Contains("month=7", handler.LastRequestBody);
    }

    // -----------------------------------------------------------------------
    // 3. Empty planner content is treated as failure (returns localized error)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveIntent_EmptyPlannerContent_ReturnsLocalizedError_NotDashboardSummary()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "P1" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var aiClient = new LocalAiClient(new HttpClient(new EmptyContentHttpMessageHandler()));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        var (answer, _) = await service.ResolveIntentAndGetDataAsync(
            "Cuales han sido los ingresos de este mes?", null, property.Id);

        // Assert: must NOT be the legacy dashboard summary (rooms + tenants count)
        Assert.NotNull(answer);
        Assert.DoesNotContain("habitaciones", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rooms", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resumen:", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Summary:", answer, StringComparison.OrdinalIgnoreCase);
        // Must be a planner-error message instead
        Assert.True(answer.Contains("interpretar", StringComparison.OrdinalIgnoreCase) ||
                    answer.Contains("interpret", StringComparison.OrdinalIgnoreCase) ||
                    answer.Contains("error", StringComparison.OrdinalIgnoreCase),
                    $"Expected localized error, got: {answer}");
    }

    // -----------------------------------------------------------------------
    // 4. Invalid planner JSON is treated as failure
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveIntent_InvalidPlannerJson_ReturnsLocalizedError_NotDashboardSummary()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "P2" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var aiClient = new LocalAiClient(new HttpClient(new InvalidJsonHttpMessageHandler()));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        var (answer, _) = await service.ResolveIntentAndGetDataAsync(
            "Cuales han sido los ingresos de este mes?", null, property.Id);

        // Assert: must NOT fall through to legacy dashboard_summary
        Assert.NotNull(answer);
        Assert.DoesNotContain("habitaciones", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resumen:", answer, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // 5. Planner failure for monthly-income question does NOT execute dashboard_summary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveIntent_PlannerFailure_DoesNotReturnDashboardSummaryForIncomeQuestion()
    {
        // Arrange: use empty-content handler (timeout simulation)
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "P3" };
        db.Properties.Add(property);
        db.Tenants.Add(new Tenant { FullName = "Test Tenant", PropertyId = property.Id });
        db.Rooms.Add(new Room { Name = "R1", PropertyId = property.Id, IsActive = true });
        await db.SaveChangesAsync();

        var aiClient = new LocalAiClient(new HttpClient(new EmptyContentHttpMessageHandler()));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync(
            "Cuales han sido los ingresos de este mes?", null, property.Id);

        // Assert: dashboard_summary format never returned
        // Legacy dashboard_summary produces: "Resumen: La aplicación tiene X habitaciones..."
        Assert.NotNull(answer);
        Assert.DoesNotContain("Resumen: La aplicación", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Summary: The app", answer, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // 6. A valid monthly-income QueryPlan is accepted (payments/sum/year+month filters)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveIntent_ValidMonthlyIncomePlan_ExecutesAndFormatsCorrectly()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "P4" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "T1", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var year = DateTime.Today.Year;
        var month = DateTime.Today.Month;

        // Add a paid payment for this month
        db.MonthlyPayments.Add(new MonthlyPayment
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            Year = year,
            Month = month,
            ExpectedRentAmount = 500,
            ExpectedExpenseAmount = 50,
            PaidAmount = 500,
            Status = PaymentStatus.Paid
        });
        await db.SaveChangesAsync();

        // Return a valid plan JSON from LLM: payments/sum filtered by current year+month
        var planJson = JsonSerializer.Serialize(new
        {
            language = "es",
            resource = "payments",
            operation = "sum",
            filters = new[]
            {
                new { field = "year", @operator = "equals", value = year },
                new { field = "month", @operator = "equals", value = month }
            },
            projection = new[] { "paidAmount" },
            sort = Array.Empty<object>(),
            limit = 20,
            confidence = 0.95
        });

        var httpContent = BuildChoicesResponse(planJson);
        var handler = new CapturingHttpMessageHandler(httpContent);
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync(
            "Cuales han sido los ingresos de este mes?", null, property.Id);

        // Assert: answer must contain the summed paidAmount
        Assert.NotNull(answer);
        Assert.True(isSpanish);
        // The formatter outputs sum in locale format; 500 should appear in the answer
        Assert.Contains("500", answer);
        // Must NOT be a dashboard summary
        Assert.DoesNotContain("Resumen: La aplicación", answer, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // 7. Existing tenant move-out tests still pass through the semantic planner
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveIntent_TenantMoveOutPlan_StillWorks()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "P5" };
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

        var handler = new CapturingHttpMessageHandler(BuildChoicesResponse(planJson));
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
