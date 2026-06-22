using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.Tests;

public class DomainTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DomainTests()
    {
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
    public void CreateRoom_ShouldPersist()
    {
        using var db = CreateContext();
        var room = new Room { Name = "101", MonthlyRent = 500 };
        db.Rooms.Add(room);
        db.SaveChanges();

        Assert.NotEqual(0, room.Id);
    }

    [Fact]
    public void CreateRoom_NullName_ShouldThrow()
    {
        using var db = CreateContext();
        var room = new Room { Name = null!, MonthlyRent = 500 };
        db.Rooms.Add(room);
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void CreateTenant_ShouldPersist()
    {
        using var db = CreateContext();
        var tenant = new Tenant { FullName = "John Doe", Email = "john@example.com", Phone = "555-1234" };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        Assert.NotEqual(0, tenant.Id);
    }

    [Fact]
    public void AssignTenantToRoom_ShouldSetRoomId()
    {
        using var db = CreateContext();
        var room = new Room { Name = "101", MonthlyRent = 500 };
        db.Rooms.Add(room);
        db.SaveChanges();

        var tenant = new Tenant { FullName = "Jane Doe", RoomId = room.Id };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        var loaded = db.Tenants.Find(tenant.Id);
        Assert.NotNull(loaded);
        Assert.Equal(room.Id, loaded.RoomId);
    }

    [Fact]
    public void AddRentalContract_ShouldPersist()
    {
        using var db = CreateContext();
        var contract = new RentalContract
        {
            TenantId = 1,
            FilePath = "/tmp/test_contract.pdf",
            Notes = "Test contract"
        };
        db.RentalContracts.Add(contract);
        db.SaveChanges();

        Assert.NotEqual(0, contract.Id);
    }

    [Fact]
    public void CreateMonthlyPayment_ShouldPersist()
    {
        using var db = CreateContext();
        var payment = new MonthlyPayment
        {
            TenantId = 1,
            Year = 2026,
            Month = 6,
            ExpectedAmount = 500,
            Status = PaymentStatus.Pending
        };
        db.MonthlyPayments.Add(payment);
        db.SaveChanges();

        Assert.NotEqual(0, payment.Id);
    }

    [Fact]
    public void CreateDuplicateMonthlyPayment_ShouldThrow()
    {
        using var db = CreateContext();
        var payment1 = new MonthlyPayment
        {
            TenantId = 1,
            Year = 2026,
            Month = 6,
            ExpectedAmount = 500,
            Status = PaymentStatus.Pending
        };
        db.MonthlyPayments.Add(payment1);
        db.SaveChanges();

        var payment2 = new MonthlyPayment
        {
            TenantId = 1,
            Year = 2026,
            Month = 6,
            ExpectedAmount = 500,
            Status = PaymentStatus.Pending
        };
        db.MonthlyPayments.Add(payment2);
        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void QueryPendingPayments_ShouldReturnExpected()
    {
        using var db = CreateContext();
        db.MonthlyPayments.AddRange(
            new MonthlyPayment { TenantId = 1, Year = 2026, Month = 6, ExpectedAmount = 500, Status = PaymentStatus.Pending },
            new MonthlyPayment { TenantId = 2, Year = 2026, Month = 6, ExpectedAmount = 600, Status = PaymentStatus.Paid },
            new MonthlyPayment { TenantId = 3, Year = 2026, Month = 6, ExpectedAmount = 700, Status = PaymentStatus.Pending }
        );
        db.SaveChanges();

        var pending = db.MonthlyPayments
            .Where(p => p.Year == 2026 && p.Month == 6 && p.Status == PaymentStatus.Pending)
            .ToList();

        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public void UpdatePaymentStatus_ShouldReflect()
    {
        using var db = CreateContext();
        var payment = new MonthlyPayment
        {
            TenantId = 1,
            Year = 2026,
            Month = 6,
            ExpectedAmount = 500,
            Status = PaymentStatus.Pending
        };
        db.MonthlyPayments.Add(payment);
        db.SaveChanges();

        payment.Status = PaymentStatus.Paid;
        payment.PaidAmount = 500;
        payment.PaidDate = DateTime.Today;
        db.SaveChanges();

        var loaded = db.MonthlyPayments.Find(payment.Id);
        Assert.NotNull(loaded);
        Assert.Equal(PaymentStatus.Paid, loaded.Status);
        Assert.Equal(500, loaded.PaidAmount);
    }

    [Fact]
    public void DeactivateTenant_ShouldNotDelete()
    {
        using var db = CreateContext();
        var tenant = new Tenant { FullName = "John Doe", IsActive = true };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        tenant.IsActive = false;
        db.SaveChanges();

        var loaded = db.Tenants.Find(tenant.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsActive);
    }

    [Fact]
    public void DeactivateRoom_ShouldNotDelete()
    {
        using var db = CreateContext();
        var room = new Room { Name = "101", MonthlyRent = 500, IsActive = true };
        db.Rooms.Add(room);
        db.SaveChanges();

        room.IsActive = false;
        db.SaveChanges();

        var loaded = db.Rooms.Find(room.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsActive);
    }

    [Fact]
    public void ReactivateRoom_ShouldSetActiveTrue()
    {
        using var db = CreateContext();
        var room = new Room { Name = "101", MonthlyRent = 500, IsActive = false };
        db.Rooms.Add(room);
        db.SaveChanges();

        room.IsActive = true;
        db.SaveChanges();

        var loaded = db.Rooms.Find(room.Id);
        Assert.NotNull(loaded);
        Assert.True(loaded.IsActive);
    }
}
