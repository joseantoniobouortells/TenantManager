using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticQueryExecutorTests
{
    private AppDbContext GetMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var db = new AppDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Executor_CountsLatePaymentsForActiveProperty()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var property = new Property { Name = "Active Property" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Tenant A", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Late payment (unpaid, year/month in the past)
        var pastDate = DateTime.Today.AddMonths(-2);
        var contract = new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            StartDate = pastDate,
            EndDate = pastDate.AddMonths(12),
            MonthlyRent = 500m
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "late", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        // Inject propertyId scope via validator
        SemanticQueryPlanValidator.Validate(plan, property.Id);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var count = Assert.IsType<int>(result);
        Assert.True(count > 0, $"Expected at least 1 late payment, got {count}");
    }

    [Fact]
    public async Task Executor_CountsActiveContractsForActiveProperty()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var property = new Property { Name = "Active Property" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Tenant A", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Active contract
        db.RentalContracts.Add(new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        });

        // Expired contract
        db.RentalContracts.Add(new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-12),
            EndDate = DateTimeOffset.Now.AddMonths(-6)
        });

        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Contracts,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "active", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        SemanticQueryPlanValidator.Validate(plan, property.Id);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var count = Assert.IsType<int>(result);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Executor_ListsAvailableRoomsCorrectly()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var roomAvailable = new Room { Name = "101", PropertyId = property.Id, IsActive = true };
        var roomOccupied = new Room { Name = "102", PropertyId = property.Id, IsActive = true };
        db.Rooms.AddRange(roomAvailable, roomOccupied);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Tenant A", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Contract occupies room 102
        db.RentalContracts.Add(new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            RoomId = roomOccupied.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        });
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List,
            Confidence = 0.9,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "available", Operator = SemanticQueryOperator.Equals, Value = true }
            }
        };

        SemanticQueryPlanValidator.Validate(plan, property.Id);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(result).ToList();
        Assert.Single(list);

        var firstRoom = Assert.IsType<SemanticRoomResult>(list[0]);
        Assert.Equal("101", firstRoom.Name);
        Assert.True(firstRoom.Available);
        Assert.False(firstRoom.Occupied);
    }

    [Fact]
    public async Task Executor_SumsPendingPaymentAmountsForCurrentMonth()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Tenant A", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Contract covers current month
        var current = DateTimeOffset.Now;
        var contract = new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            StartDate = current.AddMonths(-1),
            EndDate = current.AddMonths(3),
            MonthlyRent = 500m,
            FixedExpenseAmount = 40m,
            ExpensePaymentType = ExpensePaymentType.Fixed
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        // Expected pending amount for current month: 500 + 40 = 540
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Payments,
            Operation = SemanticQueryOperation.Sum,
            Confidence = 0.9,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "pending", Operator = SemanticQueryOperator.Equals, Value = true },
                new SemanticQueryFilter { Field = "year", Operator = SemanticQueryOperator.Equals, Value = "current" },
                new SemanticQueryFilter { Field = "month", Operator = SemanticQueryOperator.Equals, Value = "current" }
            }
        };

        SemanticQueryPlanValidator.Validate(plan, property.Id);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var sum = Assert.IsType<decimal>(result);
        Assert.Equal(540m, sum);
    }

    [Fact]
    public async Task Executor_ScopesResultsToActiveProperty()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var prop1 = new Property { Name = "P1" };
        var prop2 = new Property { Name = "P2" };
        db.Properties.AddRange(prop1, prop2);
        await db.SaveChangesAsync();

        db.Rooms.Add(new Room { Name = "101", PropertyId = prop1.Id, IsActive = true });
        db.Rooms.Add(new Room { Name = "201", PropertyId = prop2.Id, IsActive = true });
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.Count,
            Confidence = 0.9
        };

        // Validate scopes count to prop1
        SemanticQueryPlanValidator.Validate(plan, prop1.Id);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var count = Assert.IsType<int>(result);
        Assert.Equal(1, count); // Only rooms of property 1 counted
    }

    [Fact]
    public async Task Executor_ListResultRespectsLimit()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var property = new Property { Name = "P" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        for (int i = 1; i <= 10; i++)
        {
            db.Rooms.Add(new Room { Name = $"Room {i}", PropertyId = property.Id, IsActive = true });
        }
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List,
            Confidence = 0.9,
            Limit = 3
        };

        SemanticQueryPlanValidator.Validate(plan, property.Id);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(result).ToList();
        Assert.Equal(3, list.Count);
    }
}
