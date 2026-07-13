using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

[Collection("SequentialAiTests")]
public class SemanticQueryFunctionalTests
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

    private static (AiQueryService service, AppDbContext db) BuildService(string planJson, AppDbContext db)
    {
        var chatResponse = new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = planJson
                    }
                }
            }
        };
        var responseJson = JsonSerializer.Serialize(chatResponse);
        var httpClient = new HttpClient(new MockHttpMessageHandler(responseJson));
        var aiClient = new LocalAiClient(httpClient);
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });
        return (new AiQueryService(db, aiClient), db);
    }

    [Fact]
    public async Task Functional_LatePaymentCountIsCorrect()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Active Property" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant1 = new Tenant { FullName = "Tenant 1", PropertyId = property.Id };
        var tenant2 = new Tenant { FullName = "Tenant 2", PropertyId = property.Id };
        db.Tenants.AddRange(tenant1, tenant2);
        await db.SaveChangesAsync();

        var pastMonth = DateTime.Today.AddMonths(-1);
        var futureMonth = DateTime.Today.AddMonths(1);

        // 2 late payments (Partial status with 0 paid, in the past)
        db.MonthlyPayments.Add(new MonthlyPayment
        {
            TenantId = tenant1.Id,
            PropertyId = property.Id,
            Year = pastMonth.Year,
            Month = pastMonth.Month,
            ExpectedRentAmount = 400,
            ExpectedExpenseAmount = 0,
            PaidAmount = 0,
            Status = PaymentStatus.Partial
        });
        db.MonthlyPayments.Add(new MonthlyPayment
        {
            TenantId = tenant2.Id,
            PropertyId = property.Id,
            Year = pastMonth.Year,
            Month = pastMonth.Month,
            ExpectedRentAmount = 400,
            ExpectedExpenseAmount = 0,
            PaidAmount = 0,
            Status = PaymentStatus.Partial
        });

        // 1 pending payment (not late, in the future)
        db.MonthlyPayments.Add(new MonthlyPayment
        {
            TenantId = tenant1.Id,
            PropertyId = property.Id,
            Year = futureMonth.Year,
            Month = futureMonth.Month,
            ExpectedRentAmount = 400,
            ExpectedExpenseAmount = 0,
            PaidAmount = 0,
            Status = PaymentStatus.Partial
        });

        await db.SaveChangesAsync();

        var planJson = @"{
            ""language"": ""es"",
            ""resource"": ""payments"",
            ""operation"": ""count"",
            ""filters"": [
                { ""field"": ""late"", ""operator"": ""equals"", ""value"": true }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("¿Hay pagos atrasados?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.Equal("Hay 2 pagos con retraso.", answer);
    }

    [Fact]
    public async Task Functional_ActiveContractCountIsCorrect()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room { Name = "Room", PropertyId = property.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        // 4 active contracts
        for (int i = 0; i < 4; i++)
        {
            var tenant = new Tenant { FullName = $"Active Tenant {i}", PropertyId = property.Id };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            db.RentalContracts.Add(new RentalContract
            {
                TenantId = tenant.Id,
                RoomId = room.Id,
                PropertyId = property.Id,
                StartDate = DateTimeOffset.Now.AddMonths(-1),
                EndDate = DateTimeOffset.Now.AddMonths(5)
            });
        }

        // 1 expired contract
        var expiredTenant = new Tenant { FullName = "Expired Tenant", PropertyId = property.Id };
        db.Tenants.Add(expiredTenant);
        await db.SaveChangesAsync();

        db.RentalContracts.Add(new RentalContract
        {
            TenantId = expiredTenant.Id,
            RoomId = room.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-6),
            EndDate = DateTimeOffset.Now.AddMonths(-2)
        });

        await db.SaveChangesAsync();

        var planJson = @"{
            ""language"": ""es"",
            ""resource"": ""contracts"",
            ""operation"": ""count"",
            ""filters"": [
                { ""field"": ""active"", ""operator"": ""equals"", ""value"": true }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("¿Cuántos contratos están activos?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.Equal("Hay 4 contratos activos.", answer);
    }

    [Fact]
    public async Task Functional_AvailableRoomsListIsCorrect()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "T", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var roomOcc1 = new Room { Name = "Room Occ1", PropertyId = property.Id, IsActive = true };
        var roomOcc2 = new Room { Name = "Room Occ2", PropertyId = property.Id, IsActive = true };
        var roomAvail = new Room { Name = "Room Avail", PropertyId = property.Id, IsActive = true };
        db.Rooms.AddRange(roomOcc1, roomOcc2, roomAvail);
        await db.SaveChangesAsync();

        // Occupy roomOcc1 and roomOcc2
        db.RentalContracts.Add(new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = roomOcc1.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        });
        db.RentalContracts.Add(new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = roomOcc2.Id,
            PropertyId = property.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5)
        });
        await db.SaveChangesAsync();

        var planJson = @"{
            ""language"": ""es"",
            ""resource"": ""rooms"",
            ""operation"": ""list"",
            ""filters"": [
                { ""field"": ""available"", ""operator"": ""equals"", ""value"": true }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("¿Qué habitaciones están libres?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.Contains("Room Avail", answer);
        Assert.DoesNotContain("Room Occ1", answer);
        Assert.DoesNotContain("Room Occ2", answer);
    }

    [Fact]
    public async Task Functional_PendingPaymentSumIsCorrect()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant1 = new Tenant { FullName = "T1", PropertyId = property.Id };
        var tenant2 = new Tenant { FullName = "T2", PropertyId = property.Id };
        db.Tenants.AddRange(tenant1, tenant2);
        await db.SaveChangesAsync();

        var current = DateTime.Today;

        db.MonthlyPayments.Add(new MonthlyPayment
        {
            TenantId = tenant1.Id,
            PropertyId = property.Id,
            Year = current.Year,
            Month = current.Month,
            ExpectedRentAmount = 450,
            ExpectedExpenseAmount = 0,
            PaidAmount = 0,
            Status = PaymentStatus.Partial
        });
        db.MonthlyPayments.Add(new MonthlyPayment
        {
            TenantId = tenant2.Id,
            PropertyId = property.Id,
            Year = current.Year,
            Month = current.Month,
            ExpectedRentAmount = 350,
            ExpectedExpenseAmount = 0,
            PaidAmount = 0,
            Status = PaymentStatus.Partial
        });
        await db.SaveChangesAsync();

        var planJson = @"{
            ""language"": ""es"",
            ""resource"": ""payments"",
            ""operation"": ""sum"",
            ""filters"": [
                { ""field"": ""pending"", ""operator"": ""equals"", ""value"": true },
                { ""field"": ""month"", ""operator"": ""equals"", ""value"": ""current"" }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("¿Cuánto queda por cobrar este mes?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.Contains("800,00", answer);
    }

    [Fact]
    public async Task Functional_MoveOutDateWithExtensionIsCorrect()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var tenant = new Tenant { FullName = "Erik Artigas Reverter", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        var room = new Room { Name = "Room", PropertyId = property.Id, IsActive = true };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new RentalContract
        {
            TenantId = tenant.Id,
            RoomId = room.Id,
            PropertyId = property.Id,
            StartDate = new DateTimeOffset(new DateTime(2026, 1, 1)),
            EndDate = new DateTimeOffset(new DateTime(2026, 6, 30))
        };
        db.RentalContracts.Add(contract);
        await db.SaveChangesAsync();

        db.RentalContractExtensions.Add(new RentalContractExtension
        {
            RentalContractId = contract.Id,
            StartDate = new DateTimeOffset(new DateTime(2026, 7, 1)),
            EndDate = new DateTimeOffset(new DateTime(2026, 8, 31)),
            MonthlyRent = 400
        });
        await db.SaveChangesAsync();

        var planJson = @"{
            ""language"": ""es"",
            ""resource"": ""tenants"",
            ""operation"": ""lookup"",
            ""filters"": [
                { ""field"": ""fullName"", ""operator"": ""contains"", ""value"": ""Erik Artigas"" }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("Cuando se va Erik Artigas?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.Contains("2026-08-31", answer);
    }

    [Fact]
    public async Task Functional_LanguageSelection_EnglishReturnsEnglish()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Active Property" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        db.Rooms.Add(new Room { Name = "Room 101", PropertyId = property.Id, IsActive = true });
        await db.SaveChangesAsync();

        var planJson = @"{
            ""language"": ""en"",
            ""resource"": ""rooms"",
            ""operation"": ""list"",
            ""filters"": [
                { ""field"": ""available"", ""operator"": ""equals"", ""value"": true }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);
        var activePropertyId = property.Id;

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("Which rooms are currently available?", null, activePropertyId);

        // Assert
        Assert.False(isSpanish);
        Assert.Contains("available rooms", answer);
    }

    [Fact]
    public async Task Functional_AmbiguousTenantName_ReturnsClarification()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var property = new Property { Name = "Prop" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        db.Tenants.Add(new Tenant { FullName = "Erik Artigas Reverter", PropertyId = property.Id });
        db.Tenants.Add(new Tenant { FullName = "Erik Smith", PropertyId = property.Id });
        await db.SaveChangesAsync();

        // The planner parses to lookup for "Erik"
        var planJson = @"{
            ""language"": ""es"",
            ""resource"": ""tenants"",
            ""operation"": ""lookup"",
            ""filters"": [
                { ""field"": ""fullName"", ""operator"": ""contains"", ""value"": ""Erik"" }
            ],
            ""confidence"": 0.95
        }";

        var (service, _) = BuildService(planJson, db);

        // Act
        var (answer, isSpanish) = await service.ResolveIntentAndGetDataAsync("Cuando se va Erik?", null, property.Id);

        // Assert
        Assert.True(isSpanish);
        Assert.Contains("¿A cuál de los siguientes inquilinos se refiere?", answer);
        Assert.Contains("Erik Artigas Reverter", answer);
        Assert.Contains("Erik Smith", answer);
    }
}
