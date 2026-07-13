using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

// ---------------------------------------------------------------------------
// Mock HTTP handler — simulates the LLM returning an extraction JSON
// ---------------------------------------------------------------------------
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseJson;

    public MockHttpMessageHandler(string? responseJson = null)
    {
        // Default: valid extraction for "Erik Artigas", English, move-out intent
        _responseJson = responseJson ?? BuildChoicesJson(
            "{\"language\":\"en\",\"intent\":\"tenant_move_out_date\",\"confidence\":0.95,\"entities\":{\"tenantName\":\"Erik Artigas\"}}");
    }

    public static string BuildChoicesJson(string innerContent)
    {
        // Wrap the content inside the expected OpenAI-compatible response shape.
        // innerContent must NOT contain embedded double quotes — caller is responsible.
        return "{\"choices\":[{\"message\":{\"content\":\"" +
               innerContent.Replace("\"", "\\\"") +
               "\"}}]}";
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseJson)
        };
        return Task.FromResult(response);
    }
}

// ---------------------------------------------------------------------------
// Helpers for constructing LLM responses in tests
// ---------------------------------------------------------------------------
internal static class LlmJson
{
    public static string MoveOut(string tenantName, string lang = "en") =>
        MockHttpMessageHandler.BuildChoicesJson(
            $"{{\"language\":\"{lang}\",\"intent\":\"tenant_move_out_date\",\"confidence\":0.95,\"entities\":{{\"tenantName\":\"{tenantName}\"}}}}");

    public static string Unknown(string lang = "es") =>
        MockHttpMessageHandler.BuildChoicesJson(
            $"{{\"language\":\"{lang}\",\"intent\":\"unknown\",\"confidence\":0.1,\"entities\":{{}}}}");
}

// ---------------------------------------------------------------------------
// AiQueryService Tests
// ---------------------------------------------------------------------------
[Collection("SequentialAiTests")]
public class AiQueryServiceTests
{
    private static AppDbContext GetMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var db = new AppDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    private static (AiQueryService service, AppDbContext db) BuildService(string? llmJson = null)
    {
        var db = GetMemoryDbContext();
        var httpClient = new HttpClient(new MockHttpMessageHandler(llmJson));
        var aiClient = new LocalAiClient(httpClient);
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });
        return (new AiQueryService(db, aiClient), db);
    }

    // -----------------------------------------------------------------------
    // 1. Move-out date without extensions uses contract EndDate
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MoveOutDate_WithoutExtensions_UsesContractEndDate()
    {
        var (service, db) = BuildService(LlmJson.MoveOut("Erik Artigas"));

        var property = new Property { Name = "P" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var (result, _) = await service.ResolveIntentAndGetDataAsync("When does Erik Artigas move out?");

        Assert.NotNull(result);
        Assert.Contains("Erik Artigas", result);
        Assert.Contains("move out", result);
        Assert.Contains(contract.EndDate!.Value.ToString("yyyy-MM-dd"), result);
    }

    // -----------------------------------------------------------------------
    // 2. Move-out date with extensions uses latest extension date
    // -----------------------------------------------------------------------
    [Fact]
    public async Task MoveOutDate_WithExtensions_UsesLatestExtensionDate()
    {
        var (service, db) = BuildService(LlmJson.MoveOut("Erik Artigas"));

        var property = new Property { Name = "P" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-12),
            EndDate = DateTimeOffset.Now.AddMonths(-6)
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        db.RentalContractExtensions.Add(new RentalContractExtension
        {
            RentalContractId = contract.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-6),
            EndDate = DateTimeOffset.Now.AddMonths(-1)
        });

        var latestExt = new RentalContractExtension
        {
            RentalContractId = contract.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        };
        db.RentalContractExtensions.Add(latestExt);
        await db.SaveChangesAsync();

        var (result, _) = await service.ResolveIntentAndGetDataAsync("When does Erik Artigas move out?");

        Assert.NotNull(result);
        Assert.Contains(latestExt.EndDate!.Value.ToString("yyyy-MM-dd"), result);
        Assert.DoesNotContain(contract.EndDate!.Value.ToString("yyyy-MM-dd"), result);
    }

    // -----------------------------------------------------------------------
    // 3. Spanish follow-up "Y Namratha?" inherits previous move-out intent
    // -----------------------------------------------------------------------
    [Fact]
    public async Task FollowUp_Spanish_YNamratha_InheritsMoveOutIntent()
    {
        // LLM returns "unknown" for the short follow-up
        var (service, db) = BuildService(LlmJson.Unknown("es"));

        var property = new Property { Name = "P" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Namratha Sharma", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-3),
            EndDate = DateTimeOffset.Now.AddMonths(3)
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var ctx = new AssistantContext
        {
            LastResolvedIntent = "tenant_move_out_date",
            LastLanguage = "es",
            LastEntityType = "tenantName"
        };

        var (result, isEs) = await service.ResolveIntentAndGetDataAsync("Y Namratha?", ctx);

        Assert.NotNull(result);
        Assert.True(isEs, "Should respond in Spanish");
        Assert.Contains("Namratha Sharma", result);
        Assert.Contains("deja", result); // Spanish template
        Assert.Contains(contract.EndDate!.Value.ToString("yyyy-MM-dd"), result);
    }

    // -----------------------------------------------------------------------
    // 4. English follow-up "And Namratha?" inherits previous move-out intent
    // -----------------------------------------------------------------------
    [Fact]
    public async Task FollowUp_English_AndNamratha_InheritsMoveOutIntent()
    {
        var (service, db) = BuildService(LlmJson.Unknown("en"));

        var property = new Property { Name = "P" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Namratha Sharma", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-3),
            EndDate = DateTimeOffset.Now.AddMonths(3)
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var ctx = new AssistantContext
        {
            LastResolvedIntent = "tenant_move_out_date",
            LastLanguage = "en"
        };

        var (result, isEs) = await service.ResolveIntentAndGetDataAsync("And Namratha?", ctx);

        Assert.NotNull(result);
        Assert.False(isEs, "Should respond in English");
        Assert.Contains("Namratha Sharma", result);
        Assert.Contains("move out", result); // English template
    }

    // -----------------------------------------------------------------------
    // 5. Context is updated after a successful answer
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Context_IsUpdatedAfterSuccessfulAnswer()
    {
        var (service, db) = BuildService(LlmJson.MoveOut("Erik Artigas"));

        var property = new Property { Name = "P" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        db.RentalContracts.Add(new RentalContract
        {
            TenantId = tenant.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        });
        await db.SaveChangesAsync();

        var ctx = new AssistantContext();
        await service.ResolveIntentAndGetDataAsync("When does Erik Artigas move out?", ctx);

        Assert.Equal("tenant_move_out_date", ctx.LastResolvedIntent);
        Assert.Equal("en", ctx.LastLanguage);
    }

    // -----------------------------------------------------------------------
    // 6. No context — short follow-up returns null (not wrong intent)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task NoContext_ShortFollowUp_DoesNotInheritIntent()
    {
        var (service, _) = BuildService(LlmJson.Unknown("es"));

        var (result, _) = await service.ResolveIntentAndGetDataAsync("Y Namratha?", null);

        // Without a previous context the follow-up cannot be resolved
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // 7. Prompt hardening instructions are present
    // -----------------------------------------------------------------------
    [Fact]
    public void BuildSystemPrompt_ContainsHardeningInstructions()
    {
        var prompt = SafeContextBuilder.BuildSystemPrompt("Context Data");

        Assert.Contains("Return ONLY the final answer.", prompt);
        Assert.Contains("Do NOT include reasoning, analysis, or chain-of-thought.", prompt);
        Assert.Contains("one or two sentences maximum", prompt);
    }
}
