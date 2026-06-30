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
        AppDbContext.DefaultConnectionString = "Data Source=:memory:";
        SettingsPersistence.SettingsFilePath = "settings_test.json";
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
        var room = new Room { Name = "101" };
        db.Rooms.Add(room);
        db.SaveChanges();

        Assert.NotEqual(0, room.Id);
    }

    [Fact]
    public void CreateRoom_NullName_ShouldThrow()
    {
        using var db = CreateContext();
        var room = new Room { Name = null! };
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
    public void AssignContractToRoom_ShouldSetRoomId()
    {
        using var db = CreateContext();
        var room = new Room { Name = "101" };
        db.Rooms.Add(room);
        var tenant = new Tenant { FullName = "Jane Doe" };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        var contract = new RentalContract { RoomId = room.Id, TenantId = tenant.Id };
        db.RentalContracts.Add(contract);
        db.SaveChanges();

        var loaded = db.RentalContracts.Find(contract.Id);
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
            ExpectedRentAmount = 500,
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
            ExpectedRentAmount = 500,
            Status = PaymentStatus.Pending
        };
        db.MonthlyPayments.Add(payment1);
        db.SaveChanges();

        var payment2 = new MonthlyPayment
        {
            TenantId = 1,
            Year = 2026,
            Month = 6,
            ExpectedRentAmount = 500,
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
            new MonthlyPayment { TenantId = 1, Year = 2026, Month = 6, ExpectedRentAmount = 500, Status = PaymentStatus.Pending },
            new MonthlyPayment { TenantId = 2, Year = 2026, Month = 6, ExpectedRentAmount = 600, Status = PaymentStatus.Paid },
            new MonthlyPayment { TenantId = 3, Year = 2026, Month = 6, ExpectedRentAmount = 700, Status = PaymentStatus.Pending }
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
            ExpectedRentAmount = 500,
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
    public void DeactivateRoom_ShouldNotDelete()
    {
        using var db = CreateContext();
        var room = new Room { Name = "101", IsActive = true };
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
        var room = new Room { Name = "101", IsActive = false };
        db.Rooms.Add(room);
        db.SaveChanges();

        room.IsActive = true;
        db.SaveChanges();

        var loaded = db.Rooms.Find(room.Id);
        Assert.NotNull(loaded);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public void CreateProperty_ShouldPersist()
    {
        using var db = CreateContext();
        var property = new Property
        {
            Name = "Main Street Apt",
            Address = "123 Main St",
            City = "Metropolis",
            PostalCode = "12345"
        };
        db.Properties.Add(property);
        db.SaveChanges();

        Assert.NotEqual(0, property.Id);
        var loaded = db.Properties.Find(property.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Main Street Apt", loaded.Name);
    }

    [Fact]
    public void CreateRentalContractExtension_ShouldPersist()
    {
        using var db = CreateContext();
        var extension = new RentalContractExtension
        {
            RentalContractId = 1,
            StartDate = DateTimeOffset.Now,
            MonthlyRent = 750,
            ExpensePaymentType = ExpensePaymentType.Fixed,
            FixedExpenseAmount = 50,
            Notes = "First extension"
        };
        db.RentalContractExtensions.Add(extension);
        db.SaveChanges();

        Assert.NotEqual(0, extension.Id);
        var loaded = db.RentalContractExtensions.Find(extension.Id);
        Assert.NotNull(loaded);
        Assert.Equal(750, loaded.MonthlyRent);
        Assert.Equal(ExpensePaymentType.Fixed, loaded.ExpensePaymentType);
        Assert.Equal(50, loaded.FixedExpenseAmount);
    }

    [Fact]
    public void GenerateBatchPayments_ShouldCreatePayments()
    {
        using var db = CreateContext();
        
        var property = new Property { Name = "Prop 1" };
        db.Properties.Add(property);
        db.SaveChanges();

        var room = new Room { Name = "Room 101", PropertyId = property.Id, IsActive = true };
        db.Rooms.Add(room);
        db.SaveChanges();

        var tenant = new Tenant { FullName = "Alice Smith", PropertyId = property.Id };
        db.Tenants.Add(tenant);
        db.SaveChanges();

        var contract = new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            RoomId = room.Id,
            StartDate = new DateTimeOffset(new DateTime(2026, 1, 1)),
            EndDate = new DateTimeOffset(new DateTime(2026, 12, 31)),
            MonthlyRent = 500,
            ExpensePaymentType = ExpensePaymentType.Fixed,
            FixedExpenseAmount = 40
        };
        db.RentalContracts.Add(contract);
        db.SaveChanges();

        var vm = new TenantManager.App.ViewModels.MonthlyPaymentListViewModel(db);
        vm.LoadPayments(property.Id);

        vm.StartBatchCommand.Execute(null);

        var alice = vm.AvailableTenants.FirstOrDefault(t => t.Id == tenant.Id);
        Assert.NotNull(alice);
        vm.BatchSelectedTenant = alice;
        vm.BatchStartYear = 2026;
        vm.BatchStartMonth = 1;
        vm.BatchEndYear = 2026;
        vm.BatchEndMonth = 3;
        vm.BatchDefaultStatus = PaymentStatus.Pending;

        vm.GenerateBatchCommand.Execute(null);

        var payments = db.MonthlyPayments.Where(p => p.TenantId == tenant.Id && p.PropertyId == property.Id).ToList();
        Assert.Equal(3, payments.Count);
        
        var p1 = payments.FirstOrDefault(p => p.Month == 1);
        Assert.NotNull(p1);
        Assert.Equal(500, p1.ExpectedRentAmount);
        Assert.Equal(40, p1.ExpectedExpenseAmount);
        Assert.Equal(PaymentStatus.Pending, p1.Status);
    }

    [Fact]
    public void CreateExpenseInvoice_WithFile_ShouldPersist()
    {
        using var db = CreateContext();
        var invoice = new ExpenseInvoice
        {
            PropertyId = 1,
            ExpenseType = "Electricity",
            Year = 2026,
            Month = 6,
            Amount = 120.50m,
            FilePath = "/tmp/invoice.pdf",
            FileContent = new byte[] { 1, 2, 3, 4 },
            Notes = "June invoice"
        };
        db.ExpenseInvoices.Add(invoice);
        db.SaveChanges();

        Assert.NotEqual(0, invoice.Id);
        var loaded = db.ExpenseInvoices.Find(invoice.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Electricity", loaded.ExpenseType);
        Assert.Equal("/tmp/invoice.pdf", loaded.FilePath);
        Assert.NotNull(loaded.FileContent);
        Assert.Equal(4, loaded.FileContent.Length);
    }
}
