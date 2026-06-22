using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Domain;

namespace TenantManager.App.Data;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<MonthlyPayment> MonthlyPayments => Set<MonthlyPayment>();
    public DbSet<RoomRentPeriod> RoomRentPeriods => Set<RoomRentPeriod>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite(DatabasePath.ConnectionString);
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
        var entries = ChangeTracker.Entries<RoomRentPeriod>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var entity = entry.Entity;
            if (entity.MonthlyRent < 0)
                throw new InvalidOperationException("MonthlyRent must be greater than or equal to 0.");
            if (entity.EndDate.HasValue && entity.EndDate.Value < entity.StartDate)
                throw new InvalidOperationException("EndDate must be greater than or equal to StartDate.");
        }
    }
}
