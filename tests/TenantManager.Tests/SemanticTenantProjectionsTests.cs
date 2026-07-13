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

[Collection("SequentialAiTests")]
public class SemanticTenantProjectionsTests
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

    [Fact]
    public async Task Projection_EffectiveMoveOutDate_ReturnsDepartureDate()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Prop" };
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
        var answer = SemanticAnswerFormatter.Format(plan, result, plan.Language);

        // Assert
        Assert.NotNull(answer);
        Assert.Contains("Erik Artigas Reverter tiene previsto dejar la habitación el 2026-08-31", answer);
    }

    [Fact]
    public async Task Projection_ContractExtensions_AffectsDepartureDate()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Prop" };
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
            StartDate = DateTimeOffset.Now.AddMonths(-3),
            EndDate = DateTimeOffset.Now.AddMonths(-1) // Originally ended
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        // Add extension extending to 2026-12-31
        var extension = new RentalContractExtension
        {
            RentalContractId = contract.Id,
            StartDate = contract.EndDate.Value.AddDays(1),
            EndDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            MonthlyRent = 550,
            FixedExpenseAmount = 50
        };
        db.RentalContractExtensions.Add(extension);
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Language = "en",
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
        var answer = SemanticAnswerFormatter.Format(plan, result, plan.Language);

        // Assert
        Assert.NotNull(answer);
        Assert.Contains("Erik Artigas Reverter is scheduled to move out on 2026-12-31", answer);
    }

    [Fact]
    public async Task Projection_CurrentRoom_ReturnsRoom()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Prop" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
        var room = new Room { Name = "Room 303", PropertyId = prop.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = room.Id,
            PropertyId = prop.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
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
            Projection = new List<string> { "currentRoom" },
            Limit = 1
        };

        var executor = new SemanticQueryExecutor(db);

        // Act
        var result = await executor.ExecuteAsync(plan);
        var answer = SemanticAnswerFormatter.Format(plan, result, plan.Language);

        // Assert
        Assert.NotNull(answer);
        Assert.Contains("Erik Artigas Reverter está actualmente en la habitación Room 303", answer);
    }

    [Fact]
    public async Task Projection_MoveInDate_ReturnsMoveInDate()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Prop" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            PropertyId = prop.Id,
            StartDate = new DateTimeOffset(2025, 10, 15, 0, 0, 0, TimeSpan.Zero),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Language = "en",
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.List,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = prop.Id },
                new SemanticQueryFilter { Field = "fullName", Operator = SemanticQueryOperator.Equals, Value = "Erik Artigas" }
            },
            Projection = new List<string> { "moveInDate" },
            Limit = 1
        };

        var executor = new SemanticQueryExecutor(db);

        // Act
        var result = await executor.ExecuteAsync(plan);
        var answer = SemanticAnswerFormatter.Format(plan, result, plan.Language);

        // Assert
        Assert.NotNull(answer);
        Assert.Contains("Erik Artigas Reverter moved in on 2025-10-15", answer);
    }

    [Fact]
    public async Task Projection_FullName_RetainsTenantListFactualResponse()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Prop" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
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
            Projection = new List<string> { "fullName" },
            Limit = 1
        };

        var executor = new SemanticQueryExecutor(db);

        // Act
        var result = await executor.ExecuteAsync(plan);
        var answer = SemanticAnswerFormatter.Format(plan, result, plan.Language);

        // Assert
        Assert.NotNull(answer);
        Assert.Contains("El inquilino es Erik Artigas Reverter", answer);
    }

    [Fact]
    public async Task Projection_NullValues_ProduceLocalizedNoValueResponse()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var prop = new Property { Name = "Prop" };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = prop.Id };
        db.Tenants.Add(tenant);
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
        var answer = SemanticAnswerFormatter.Format(plan, result, plan.Language);

        // Assert
        Assert.NotNull(answer);
        Assert.Contains("No hay fecha de salida registrada para Erik Artigas Reverter", answer);
    }
}
