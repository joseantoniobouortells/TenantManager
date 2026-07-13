using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticTenantNameResolutionTests
{
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
}
