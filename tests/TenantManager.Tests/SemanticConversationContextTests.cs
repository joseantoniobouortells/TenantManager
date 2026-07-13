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

[Collection("SequentialAiTests")]
public class SemanticConversationContextTests
{
    private class DynamicMockHttpMessageHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public List<string> RequestUrls { get; } = new();
        public List<ChatRequest?> ParsedRequests { get; } = new();
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
                    var parsed = JsonSerializer.Deserialize<ChatRequest>(body);
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

    private static string BuildChatResponse(string content)
    {
        var obj = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = content
                    }
                }
            }
        };
        return JsonSerializer.Serialize(obj);
    }

    [Fact]
    public async Task Context_SuccessfulTenantQuery_StoresCanonicalTenantIdentity()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Active Property" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Alice Marchegiani", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var planJson = JsonSerializer.Serialize(new
        {
            language = "es",
            resource = "tenants",
            operation = "lookup",
            filters = new[] { new { field = "fullName", @operator = "equals", value = "Alice" } },
            projection = new[] { "effectiveMoveOutDate" },
            sort = Array.Empty<object>(),
            limit = 20,
            confidence = 0.95
        });

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildChatResponse(planJson));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);
        var context = new AssistantContext();

        // Act
        var (answer, _) = await service.ResolveIntentAndGetDataAsync("Cuando se fue Alice?", context, prop.Id);

        // Assert
        Assert.NotNull(context.LastTenantId);
        Assert.Equal(tenant.Id, context.LastTenantId.Value);
        Assert.Equal("Alice Marchegiani", context.LastTenantDisplayName);
        Assert.Equal("tenants", context.LastResource);
        Assert.Contains("effectiveMoveOutDate", context.LastProjection);
    }

    [Fact]
    public async Task Context_FollowUpQuery_ReusesAliceAndReplacesProjection()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Active Property" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Alice Marchegiani", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var context = new AssistantContext
        {
            LastResolvedIntent = "tenants_lookup",
            LastLanguage = "es",
            LastResource = "tenants",
            LastOperation = "lookup",
            LastTenantId = tenant.Id,
            LastTenantDisplayName = tenant.FullName,
            LastPropertyId = prop.Id
        };
        context.LastProjection.Add("effectiveMoveOutDate");

        // The next plan expected for "En que fecha entró?"
        var planJson = JsonSerializer.Serialize(new
        {
            language = "es",
            resource = "tenants",
            operation = "list",
            filters = new[] { new { field = "fullName", @operator = "equals", value = "Alice Marchegiani" } },
            projection = new[] { "moveInDate" },
            sort = Array.Empty<object>(),
            limit = 20,
            confidence = 0.95
        });

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildChatResponse(planJson));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        await service.ResolveIntentAndGetDataAsync("En que fecha entró en la habitación?", context, prop.Id);

        // Assert
        Assert.Single(handler.Requests);
        var req = handler.ParsedRequests[0];
        Assert.NotNull(req);
        var sysContent = req.Messages[0].Content;
        
        // Assert the prompt contextHint contains the compact structured block
        Assert.Contains("Previous successful query:", sysContent);
        Assert.Contains("tenantName=Alice Marchegiani", sysContent);
        Assert.Contains("projection=effectiveMoveOutDate", sysContent);
    }

    [Fact]
    public async Task Context_NewExplicitTenant_OverridesPreviousTenant()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Active Property" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenantAlice = new Tenant { FullName = "Alice Marchegiani", PropertyId = prop.Id };
        var tenantErik = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = prop.Id };
        db.Tenants.AddRange(tenantAlice, tenantErik);
        await db.SaveChangesAsync();

        var context = new AssistantContext
        {
            LastResolvedIntent = "tenants_lookup",
            LastLanguage = "es",
            LastResource = "tenants",
            LastOperation = "lookup",
            LastTenantId = tenantAlice.Id,
            LastTenantDisplayName = tenantAlice.FullName,
            LastPropertyId = prop.Id
        };
        context.LastProjection.Add("effectiveMoveOutDate");

        // The plan for the override query "Y Erik Artigas?"
        var planJson = JsonSerializer.Serialize(new
        {
            language = "es",
            resource = "tenants",
            operation = "lookup",
            filters = new[] { new { field = "fullName", @operator = "equals", value = "Erik Artigas" } },
            projection = new[] { "effectiveMoveOutDate" },
            sort = Array.Empty<object>(),
            limit = 20,
            confidence = 0.95
        });

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildChatResponse(planJson));
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        await service.ResolveIntentAndGetDataAsync("Y Erik Artigas?", context, prop.Id);

        // Assert
        Assert.Equal(tenantErik.Id, context.LastTenantId.Value);
        Assert.Equal("Erik Artigas Reverter", context.LastTenantDisplayName);
    }

    [Fact]
    public async Task Context_FailedQuery_DoesNotEraseOrReplacePreviousContext()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Active Property" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Alice Marchegiani", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var context = new AssistantContext
        {
            LastResolvedIntent = "tenants_lookup",
            LastLanguage = "es",
            LastResource = "tenants",
            LastOperation = "lookup",
            LastTenantId = tenant.Id,
            LastTenantDisplayName = tenant.FullName,
            LastPropertyId = prop.Id
        };
        context.LastProjection.Add("effectiveMoveOutDate");

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildChatResponse("{ invalid json }")); // Failed query
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        await service.ResolveIntentAndGetDataAsync("Invalid Query", context, prop.Id);

        // Assert
        Assert.Equal(tenant.Id, context.LastTenantId.Value);
        Assert.Equal("Alice Marchegiani", context.LastTenantDisplayName);
    }

    [Fact]
    public async Task Context_CrossingActivePropertyBoundaries_ClearsContext()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop1 = new Property { Name = "Prop 1" };
        var prop2 = new Property { Name = "Prop 2" };
        db.Properties.AddRange(prop1, prop2);
        await db.SaveChangesAsync();

        var context = new AssistantContext
        {
            LastResolvedIntent = "tenants_lookup",
            LastLanguage = "es",
            LastResource = "tenants",
            LastOperation = "lookup",
            LastTenantId = 1,
            LastTenantDisplayName = "Alice Marchegiani",
            LastPropertyId = prop1.Id
        };

        var handler = new DynamicMockHttpMessageHandler();
        handler.QueueResponse(HttpStatusCode.OK, BuildChatResponse("{}")); // Empty response
        var aiClient = new LocalAiClient(new HttpClient(handler));
        ConfigureMockSettings();

        var service = new AiQueryService(db, aiClient);

        // Act
        await service.ResolveIntentAndGetDataAsync("Some Query", context, prop2.Id);

        // Assert
        Assert.Null(context.LastTenantId);
        Assert.Null(context.LastTenantDisplayName);
        Assert.Equal(prop2.Id, context.LastPropertyId.Value);
    }
}
