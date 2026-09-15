using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.App.ViewModels;
using Xunit;

namespace TenantManager.Tests;

public class ContractListViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ContractListViewModelTests()
    {
        AppDbContext.DefaultConnectionString = "Data Source=:memory:";
        SettingsPersistence.SettingsFilePath = "settings_test_contracts.json";
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private AppDbContext CreateContext() => new AppDbContext(_options);

    [Fact]
    public void LoadContracts_DefaultsToActiveOnly_AndFiltersCorrectly()
    {
        using var db = CreateContext();
        var prop = new Property { Name = "Test Prop", Address = "123 St" };
        db.Properties.Add(prop);
        db.SaveChanges();

        var tenant1 = new Tenant { PropertyId = prop.Id, FullName = "Active Tenant" };
        var tenant2 = new Tenant { PropertyId = prop.Id, FullName = "Expired Tenant" };
        db.Tenants.AddRange(tenant1, tenant2);
        db.SaveChanges();

        var room = new Room { PropertyId = prop.Id, Name = "Room 101" };
        db.Rooms.Add(room);
        db.SaveChanges();

        // 1 active contract
        var activeContract = new RentalContract
        {
            PropertyId = prop.Id,
            TenantId = tenant1.Id,
            RoomId = room.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(5),
            MonthlyRent = 500m,
            DepositAmount = 1000m,
            PaymentDay = 5
        };

        // 1 expired contract
        var expiredContract = new RentalContract
        {
            PropertyId = prop.Id,
            TenantId = tenant2.Id,
            RoomId = room.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-12),
            EndDate = DateTimeOffset.Now.AddMonths(-2),
            MonthlyRent = 450m,
            DepositAmount = 900m,
            PaymentDay = 1
        };

        db.RentalContracts.AddRange(activeContract, expiredContract);
        db.SaveChanges();

        var vm = new ContractListViewModel(db);
        vm.LoadContracts(prop.Id);

        // 1. By default, SelectedStatusFilter is Active
        Assert.Equal(ContractStatusFilter.Active, vm.SelectedStatusFilter);
        Assert.Single(vm.Contracts);
        Assert.Equal("Active Tenant", vm.Contracts[0].TenantName);
        Assert.Equal(500m, vm.Contracts[0].MonthlyRent);
        Assert.Equal(1000m, vm.Contracts[0].DepositAmount);
        Assert.Equal(5, vm.Contracts[0].PaymentDay);

        // 2. Change filter to Expired
        vm.SelectedStatusFilter = ContractStatusFilter.Expired;
        Assert.Single(vm.Contracts);
        Assert.Equal("Expired Tenant", vm.Contracts[0].TenantName);
        Assert.Equal(450m, vm.Contracts[0].MonthlyRent);

        // 3. Change filter to All
        vm.SelectedStatusFilter = ContractStatusFilter.All;
        Assert.Equal(2, vm.Contracts.Count);

        // 4. Test SearchQuery combined with filter
        vm.SearchQuery = "Active";
        Assert.Single(vm.Contracts);
        Assert.Equal("Active Tenant", vm.Contracts[0].TenantName);

        vm.SearchQuery = "NonExistent";
        Assert.Empty(vm.Contracts);
    }

    [Fact]
    public void Sort_ByMonthlyRent_SortsCorrectly()
    {
        using var db = CreateContext();
        var prop = new Property { Name = "Prop", Address = "Addr" };
        db.Properties.Add(prop);
        db.SaveChanges();

        var t1 = new Tenant { PropertyId = prop.Id, FullName = "Tenant A" };
        var t2 = new Tenant { PropertyId = prop.Id, FullName = "Tenant B" };
        db.Tenants.AddRange(t1, t2);
        db.SaveChanges();

        var c1 = new RentalContract
        {
            PropertyId = prop.Id,
            TenantId = t1.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(2),
            MonthlyRent = 300m
        };
        var c2 = new RentalContract
        {
            PropertyId = prop.Id,
            TenantId = t2.Id,
            StartDate = DateTimeOffset.Now.AddMonths(-1),
            EndDate = DateTimeOffset.Now.AddMonths(2),
            MonthlyRent = 600m
        };
        db.RentalContracts.AddRange(c1, c2);
        db.SaveChanges();

        var vm = new ContractListViewModel(db);
        vm.LoadContracts(prop.Id);

        // Sort by MonthlyRent
        vm.Sort("MonthlyRent");
        Assert.Equal(300m, vm.Contracts[0].MonthlyRent);
        Assert.Equal(600m, vm.Contracts[1].MonthlyRent);

        // Reverse sort
        vm.Sort("MonthlyRent");
        Assert.Equal(600m, vm.Contracts[0].MonthlyRent);
        Assert.Equal(300m, vm.Contracts[1].MonthlyRent);
    }
}
