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

public class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var responseContent = @"{
            ""choices"": [
                {
                    ""message"": {
                        ""content"": ""{\""language\"": \""en\"", \""intent\"": \""tenant_move_out_date\"", \""confidence\"": 0.95, \""entities\"": { \""tenantName\"": \""Erik Artigas\"" }}""
                    }
                }
            ]
        }";

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent)
        };
        return Task.FromResult(response);
    }
}

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
        
        var httpClient = new HttpClient(new MockHttpMessageHandler());
        var aiClient = new LocalAiClient(httpClient);
        var service = new AiQueryService(db, aiClient);

        // Mock Settings
        var tempSettingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "settings.json");
        // We assume IsAiEnabled = true because the mock handler doesn't read the file directly, but LocalAiClient does.
        // If settings disable it, ExtractIntentAsync returns null.
        // For testing, we ensure settings are enabled.
        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

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
        var (resultEnglish, isEs1, clar1) = await service.ResolveIntentAndGetDataAsync("When does Erik Artigas move out?");

        // Assert
        Assert.NotNull(resultEnglish);
        Assert.Contains("Erik Artigas", resultEnglish);
        Assert.Contains(contract.EndDate.Value.ToString("yyyy-MM-dd"), resultEnglish);
    }
}
