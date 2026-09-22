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
public class SemanticTenantNameResolutionTests
{
    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new();

        public void QueueResponse(string json) => _responses.Enqueue(json);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _responses.Count > 0 ? _responses.Dequeue() : "{}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };
            return Task.FromResult(response);
        }
    }

    private static string MakeChatResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content } }
            }
        });
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

    // Helper to run FindBestTenantMatch quickly in tests
    private static Tenant? Resolve(string requested, List<Tenant> tenants, bool isSpanish, out string? clarification)
    {
        return AiQueryService.FindBestTenantMatch(requested, tenants, isSpanish, out clarification);
    }

    [Fact]
    public void Matching_PartialToken_ResolvesUniquely()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Erik Artigas Reverter" },
            new Tenant { FullName = "Namratha Sharma" }
        };

        // Act
        var match = Resolve("Erik Artigas", tenants, isSpanish: true, out var clarification);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("Erik Artigas Reverter", match.FullName);
        Assert.Null(clarification);
    }

    [Fact]
    public void Matching_ExactName_Succeeds()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Erik Artigas" }
        };

        // Act
        var match = Resolve("Erik Artigas", tenants, isSpanish: true, out var clarification);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("Erik Artigas", match.FullName);
    }

    [Fact]
    public void Matching_CaseInsensitive_Succeeds()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Erik Artigas Reverter" }
        };

        // Act
        var match = Resolve("erik artigas", tenants, isSpanish: true, out var clarification);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("Erik Artigas Reverter", match.FullName);
    }

    [Fact]
    public void Matching_DiacriticInsensitive_Succeeds()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Sebastián Bou" }
        };

        // Act
        var match = Resolve("sebastian", tenants, isSpanish: true, out var clarification);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("Sebastián Bou", match.FullName);
    }

    [Fact]
    public void Matching_ArbitrarySubstring_DoesNotMatch()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Erik Artigas Reverter" }
        };

        // Act
        var match = Resolve("Eri", tenants, isSpanish: true, out var clarification);

        // Assert
        Assert.Null(match);
        Assert.NotNull(clarification);
        Assert.Contains("No encuentro", clarification);
    }

    [Fact]
    public void Matching_AmbiguousMatches_ProducesClarification()
    {
        // Arrange
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Erik Artigas" },
            new Tenant { FullName = "Erik Bou" }
        };

        // Act
        var match = Resolve("Erik", tenants, isSpanish: true, out var clarification);

        // Assert
        Assert.Null(match);
        Assert.NotNull(clarification);
        Assert.Contains("varios inquilinos", clarification);
        Assert.Contains("Erik Artigas", clarification);
        Assert.Contains("Erik Bou", clarification);
    }

    [Fact]
    public async Task Execution_IgnoresTenantsOutsideActiveProperty()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var propActive = new Property { Name = "Active Property" };
        var propOther = new Property { Name = "Other Property" };
        db.Properties.AddRange(propActive, propOther);
        await db.SaveChangesAsync();

        var tenantActive = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = propActive.Id };
        var tenantOther = new Tenant { FullName = "Erik Artigas", PropertyId = propOther.Id };
        db.Tenants.AddRange(tenantActive, tenantOther);
        await db.SaveChangesAsync();

        // Query plan with active property scope
        var plan = new SemanticQueryPlan
        {
            Language = "es",
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.List,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = propActive.Id },
                new SemanticQueryFilter { Field = "fullName", Operator = SemanticQueryOperator.Equals, Value = "Erik" }
            }
        };

        var executor = new SemanticQueryExecutor(db);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result);
        var items = new List<object>();
        foreach (var item in list) items.Add(item);

        Assert.Single(items);
        var first = Assert.IsType<SemanticTenantResult>(items[0]);
        Assert.Equal("Erik Artigas Reverter", first.FullName);
    }

    [Fact]
    public async Task Execution_ValidMoveOutQueryPlan_ReturnsMoveOutDate()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Property" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
        var room = new Room { Name = "Room 1", PropertyId = prop.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = room.Id,
            PropertyId = prop.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero)
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Language = "es",
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.List,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = prop.Id },
                new SemanticQueryFilter { Field = "fullName", Operator = SemanticQueryOperator.Equals, Value = "Erik Artigas" }
            },
            Projection = new List<string> { "effectiveMoveOutDate" },
            Limit = 1
        };

        var executor = new SemanticQueryExecutor(db);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result);
        var items = new List<object>();
        foreach (var item in list) items.Add(item);

        Assert.Single(items);
        var first = Assert.IsType<SemanticTenantResult>(items[0]);
        Assert.Equal("Erik Artigas Reverter", first.FullName);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero), first.EffectiveMoveOutDate);
    }

    [Fact]
    public void Matching_ExactName_Pepe_SucceedsDirectly()
    {
        // Tenant is literally registered as "Pepe" in the database
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "Pepe" },
            new Tenant { FullName = "María García" }
        };

        var match = Resolve("Pepe", tenants, isSpanish: true, out var clarification);

        Assert.NotNull(match);
        Assert.Equal("Pepe", match.FullName);
        Assert.Null(clarification);
    }

    [Fact]
    public void Matching_Hypocoristic_JoseAntonio_ResolvesToPepe()
    {
        // Tenant is registered as "Pepe", user asks for "José" or vice versa
        var tenants1 = new List<Tenant>
        {
            new Tenant { FullName = "José Antonio Bou" }
        };

        var match1 = Resolve("Pepe", tenants1, isSpanish: true, out var clar1);
        Assert.NotNull(match1);
        Assert.Equal("José Antonio Bou", match1.FullName);
        Assert.Null(clar1);

        var tenants2 = new List<Tenant>
        {
            new Tenant { FullName = "Pepe" }
        };

        var match2 = Resolve("José", tenants2, isSpanish: true, out var clar2);
        Assert.NotNull(match2);
        Assert.Equal("Pepe", match2.FullName);
        Assert.Null(clar2);
    }

    [Fact]
    public void Matching_NotFound_ReturnsCleanClarificationWithoutDebugStrings()
    {
        var tenants = new List<Tenant>
        {
            new Tenant { FullName = "María García" },
            new Tenant { FullName = "Erik Artigas" }
        };

        var match = Resolve("Pepe", tenants, isSpanish: true, out var clarification);

        Assert.Null(match);
        Assert.NotNull(clarification);
        Assert.DoesNotContain("Debug", clarification);
        Assert.DoesNotContain("targetNorm", clarification);
        Assert.Contains("No encuentro ningún inquilino llamado Pepe", clarification);
        Assert.Contains("María García, Erik Artigas", clarification);
    }

    [Fact]
    public void Formatting_MultipleTenantsWithCurrentRoom_OutputsBothNameAndRoom()
    {
        var tenants = new List<SemanticTenantResult>
        {
            new SemanticTenantResult { FullName = "María García", CurrentRoom = "Habitación 1", Active = true },
            new SemanticTenantResult { FullName = "Erik Artigas", CurrentRoom = "Habitación 2", Active = true }
        };

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.List,
            Language = "es",
            Projection = new List<string> { "fullName", "currentRoom" },
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "active", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        var answer = SemanticAnswerFormatter.Format(plan, tenants, "es");

        Assert.Contains("Inquilinos actuales:", answer);
        Assert.Contains("María García (Habitación 1)", answer);
        Assert.Contains("Erik Artigas (Habitación 2)", answer);
    }

    [Fact]
    public void Formatting_InactiveTenants_OutputsInactiveHeader()
    {
        var tenants = new List<SemanticTenantResult>
        {
            new SemanticTenantResult { FullName = "Pepe", Active = false },
            new SemanticTenantResult { FullName = "Elena Santos", Active = false }
        };

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.List,
            Language = "es",
            Projection = new List<string> { "fullName" },
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "active", Operator = SemanticQueryOperator.Equals, Value = false }
            }
        };

        var answer = SemanticAnswerFormatter.Format(plan, tenants, "es");

        Assert.Contains("Los inquilinos sin contrato activo son: Pepe, Elena Santos.", answer);
    }

    [Fact]
    public async Task Execution_CurrentTenantsOfRooms_DeterministicInjection()
    {
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Propiedad Centro" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var room1 = new Room { Name = "Habitación 1", PropertyId = prop.Id, IsActive = true };
        var room2 = new Room { Name = "Habitación 2", PropertyId = prop.Id, IsActive = true };
        db.Rooms.AddRange(room1, room2);
        await db.SaveChangesAsync();

        var maria = new Tenant { FullName = "María García", PropertyId = prop.Id };
        var carlos = new Tenant { FullName = "Carlos Ruiz", PropertyId = prop.Id };
        var pepe = new Tenant { FullName = "Pepe", PropertyId = prop.Id };
        db.Tenants.AddRange(maria, carlos, pepe);
        await db.SaveChangesAsync();

        // María has an active contract in Room 1
        var mariaContract = new RentalContract
        {
            TenantId = maria.Id,
            RoomId = room1.Id,
            PropertyId = prop.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-2),
            EndDate = DateTimeOffset.Now.AddMonths(10)
        };
        // Carlos has an active contract in Room 2
        var carlosContract = new RentalContract
        {
            TenantId = carlos.Id,
            RoomId = room2.Id,
            PropertyId = prop.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(11)
        };
        db.RentalContracts.AddRange(mariaContract, carlosContract);
        await db.SaveChangesAsync();

        var mockHandler = new MockHttpHandler();
        // Call 1: Classifier returns semantic request
        mockHandler.QueueResponse(MakeChatResponse("{\"intent\":\"data_query\",\"resource\":\"tenants\",\"operation\":\"list\",\"language\":\"es\",\"confidence\":0.95}"));
        // Call 2: Planner omitted active filter and currentRoom projection (the exact Gemma issue)
        mockHandler.QueueResponse(MakeChatResponse("{\"resource\":\"tenants\",\"operation\":\"list\",\"filters\":[],\"projection\":[\"fullName\"],\"language\":\"es\",\"confidence\":0.95}"));

        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var service = new AiQueryService(db, aiClient);

        // Act: user asks about current tenants of rooms
        var (finalAnswer, isSpanish) = await service.ResolveIntentAndGetDataAsync(
            "Quiénes son los inquilinos actuales de las habitaciones?",
            new AssistantContext(),
            prop.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.NotNull(finalAnswer);
        Assert.Contains("Inquilinos actuales:", finalAnswer);
        Assert.Contains("María García (Habitación 1)", finalAnswer);
        Assert.Contains("Carlos Ruiz (Habitación 2)", finalAnswer);
        Assert.DoesNotContain("Pepe", finalAnswer);
    }

    [Fact]
    public async Task Execution_InactiveTenants_DeterministicInjection()
    {
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Propiedad Centro" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var room = new Room { Name = "Habitación 1", PropertyId = prop.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var maria = new Tenant { FullName = "María García", PropertyId = prop.Id };
        var pepe = new Tenant { FullName = "Pepe", PropertyId = prop.Id };
        db.Tenants.AddRange(maria, pepe);
        await db.SaveChangesAsync();

        // María has active contract, Pepe does not
        var mariaContract = new RentalContract
        {
            TenantId = maria.Id,
            RoomId = room.Id,
            PropertyId = prop.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-2),
            EndDate = DateTimeOffset.Now.AddMonths(10)
        };
        db.RentalContracts.Add(mariaContract);
        await db.SaveChangesAsync();

        var mockHandler = new MockHttpHandler();
        mockHandler.QueueResponse(MakeChatResponse("{\"intent\":\"data_query\",\"resource\":\"tenants\",\"operation\":\"list\",\"language\":\"es\",\"confidence\":0.95}"));
        mockHandler.QueueResponse(MakeChatResponse("{\"resource\":\"tenants\",\"operation\":\"list\",\"filters\":[],\"projection\":[\"fullName\"],\"language\":\"es\",\"confidence\":0.95}"));

        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var service = new AiQueryService(db, aiClient);

        // Act: user asks about tenants without active contract
        var (finalAnswer, isSpanish) = await service.ResolveIntentAndGetDataAsync(
            "Ahí hay gente que no tiene contrato activo, sabes cuáles son?",
            new AssistantContext(),
            prop.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.NotNull(finalAnswer);
        Assert.Contains("Pepe", finalAnswer);
        Assert.DoesNotContain("María García", finalAnswer);
    }

    [Fact]
    public async Task Execution_WhenDoesPepeLeave_WithMultiFieldProjection()
    {
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Propiedad Centro" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var room = new Room { Name = "Habitación 2", PropertyId = prop.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var pepe = new Tenant { FullName = "Pepe", PropertyId = prop.Id };
        db.Tenants.Add(pepe);
        await db.SaveChangesAsync();

        var pepeContract = new RentalContract
        {
            TenantId = pepe.Id,
            RoomId = room.Id,
            PropertyId = prop.Id,
            StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero)
        };
        db.RentalContracts.Add(pepeContract);
        await db.SaveChangesAsync();

        var mockHandler = new MockHttpHandler();
        mockHandler.QueueResponse(MakeChatResponse("{\"intent\":\"data_query\",\"resource\":\"tenants\",\"operation\":\"lookup\",\"language\":\"es\",\"confidence\":0.95}"));
        // LLM generates list with fullName and effectiveMoveOutDate
        mockHandler.QueueResponse(MakeChatResponse("{\"resource\":\"tenants\",\"operation\":\"list\",\"filters\":[{\"field\":\"fullName\",\"operator\":\"equals\",\"value\":\"Pepe\"}],\"projection\":[\"fullName\",\"effectiveMoveOutDate\"],\"language\":\"es\",\"confidence\":0.95}"));

        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var service = new AiQueryService(db, aiClient);

        // Act
        var (finalAnswer, isSpanish) = await service.ResolveIntentAndGetDataAsync(
            "Cuándo deja la habitación Pepe?",
            new AssistantContext(),
            prop.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.NotNull(finalAnswer);
        Assert.Contains("Pepe tiene previsto dejar la habitación el 2026-11-30.", finalAnswer);
    }
}
