using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class AiQueryServiceTests
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
    public async Task ResolveIntentAndGetDataAsync_MoveOutDate_ReturnsContextStringWithoutThrowing()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var service = new AiQueryService(db);

        var property = new Property { Name = "Test Property" };
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

        // Act
        var resultEnglish = await service.ResolveIntentAndGetDataAsync("When does Erik Artigas move out?");
        var resultSpanish = await service.ResolveIntentAndGetDataAsync("Cuando Erik Artigas deja la habitación?");

        // Assert
        Assert.NotNull(resultEnglish);
        Assert.Contains("Erik Artigas", resultEnglish);
        Assert.Contains(contract.EndDate.Value.ToString("yyyy-MM-dd"), resultEnglish);
        
        Assert.NotNull(resultSpanish);
        Assert.Contains("Erik Artigas", resultSpanish);
        Assert.Contains(contract.EndDate.Value.ToString("yyyy-MM-dd"), resultSpanish);
    }
}
