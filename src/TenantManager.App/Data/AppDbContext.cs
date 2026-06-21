using Microsoft.EntityFrameworkCore;
using TenantManager.App.Domain;

namespace TenantManager.App.Data;

public class AppDbContext : DbContext
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<MonthlyPayment> MonthlyPayments => Set<MonthlyPayment>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(DatabasePath.ConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonthlyPayment>()
            .HasIndex(m => new { m.TenantId, m.Year, m.Month })
            .IsUnique();
    }
}
