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

public class SemanticQuerySafetyTests
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

    [Fact]
    public async Task Safety_TenantSensitiveFields_AreNotExposedInFormattedAnswers()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Safe Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant
        {
            FullName = "John Doe Secure",
            Phone = "+34666666666",
            Email = "john.doe.private@example.com",
            Notes = "Secret: John pays in cash only and has a dog.",
            PropertyId = property.Id
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Query plan for lookup
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Tenants,
            Operation = SemanticQueryOperation.Lookup,
            Language = "es",
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "fullName", Operator = SemanticQueryOperator.Contains, Value = "John" },
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = property.Id }
            }
        };

        // Act
        var executor = new SemanticQueryExecutor(db);
        var queryResult = await executor.ExecuteAsync(plan);
        var formattedAnswer = SemanticAnswerFormatter.Format(plan, queryResult, "es");

        // Assert
        Assert.Contains("John Doe Secure", formattedAnswer);
        Assert.DoesNotContain("+34666666666", formattedAnswer);
        Assert.DoesNotContain("john.doe.private@example.com", formattedAnswer);
        Assert.DoesNotContain("Secret", formattedAnswer);
        Assert.DoesNotContain("dog", formattedAnswer);
    }

    [Fact]
    public async Task Safety_ContractFilePathAndNotes_AreNotExposedInFormattedAnswers()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Safe Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Alice Doe", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        var room = new Room { Name = "Room A", PropertyId = property.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = room.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(11),
            FilePath = "/var/secrets/contracts/alice_contract_signed.pdf",
            Notes = "Internal Note: Alice complained about the kitchen heater.",
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        // Query plan for list of contracts
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Contracts,
            Operation = SemanticQueryOperation.List,
            Language = "en",
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = property.Id }
            }
        };

        // Act
        var executor = new SemanticQueryExecutor(db);
        var queryResult = await executor.ExecuteAsync(plan);
        var formattedAnswer = SemanticAnswerFormatter.Format(plan, queryResult, "en");

        // Assert
        Assert.Contains("Alice Doe", formattedAnswer);
        Assert.Contains("Room A", formattedAnswer);
        Assert.DoesNotContain("secrets", formattedAnswer);
        Assert.DoesNotContain("alice_contract_signed.pdf", formattedAnswer);
        Assert.DoesNotContain("kitchen heater", formattedAnswer);
    }

    [Fact]
    public void Safety_UnknownResource_IsRejectedByValidator()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = (SemanticQueryResource)999, // Unknown enum value
            Operation = SemanticQueryOperation.List,
            Confidence = 0.9
        };

        // Act
        var result = SemanticQueryPlanValidator.Validate(plan, 1);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("unknown resource", result.ErrorMessage);
    }

    [Fact]
    public async Task Safety_ActivePropertyEnforcement_ExcludesOtherProperties()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var p1 = new Property { Name = "Property 1" };
        var p2 = new Property { Name = "Property 2" };
        db.Properties.AddRange(p1, p2);
        await db.SaveChangesAsync();

        db.Rooms.Add(new Room { Name = "Room P1", PropertyId = p1.Id, IsActive = true });
        db.Rooms.Add(new Room { Name = "Room P2", PropertyId = p2.Id, IsActive = true });
        await db.SaveChangesAsync();

        // Plan scoped to Property 1
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = p1.Id }
            }
        };

        // Act
        var executor = new SemanticQueryExecutor(db);
        var result = (List<object>)(await executor.ExecuteAsync(plan))!;

        // Assert
        var roomNames = result.Cast<SemanticRoomResult>().Select(r => r.Name).ToList();
        Assert.Contains("Room P1", roomNames);
        Assert.DoesNotContain("Room P2", roomNames);
    }

    [Fact]
    public async Task Safety_LimitIsStrictlyEnforced()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        // Create 25 rooms
        for (int i = 1; i <= 25; i++)
        {
            db.Rooms.Add(new Room { Name = $"Room {i:D2}", PropertyId = property.Id, IsActive = true });
        }
        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Rooms,
            Operation = SemanticQueryOperation.List,
            Limit = 10,
            Filters = new List<SemanticQueryFilter>
            {
                new SemanticQueryFilter { Field = "propertyId", Operator = SemanticQueryOperator.Equals, Value = property.Id }
            }
        };

        // Act
        var executor = new SemanticQueryExecutor(db);
        var result = (List<object>)(await executor.ExecuteAsync(plan))!;

        // Assert
        Assert.Equal(10, result.Count);
    }
}
