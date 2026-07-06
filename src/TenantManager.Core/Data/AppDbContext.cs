using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Domain;

namespace TenantManager.App.Data;

public class AppDbContext : DbContext
{
    public static string? DefaultConnectionString { get; set; }
    
    private readonly string? _connectionString;

    public AppDbContext()
    {
    }

    public AppDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<MonthlyPayment> MonthlyPayments => Set<MonthlyPayment>();
    public DbSet<RentalContractExtension> RentalContractExtensions => Set<RentalContractExtension>();
    public DbSet<ExpenseInvoice> ExpenseInvoices => Set<ExpenseInvoice>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connStr = _connectionString ?? DefaultConnectionString ?? "Data Source=tenantmanager.db";
            optionsBuilder.UseSqlite(connStr, b => b.MigrationsAssembly("TenantManager.Core"));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonthlyPayment>()
            .HasIndex(m => new { m.TenantId, m.Year, m.Month })
            .IsUnique();
    }

    public override int SaveChanges()
    {
        ValidateEntities();
        return base.SaveChanges();
    }

    private void ValidateEntities()
    {
        // Validation logic can go here
    }
}
