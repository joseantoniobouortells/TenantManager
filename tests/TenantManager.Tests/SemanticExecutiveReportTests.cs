using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;
using TenantManager.Core.Services.AI;
using TenantManager.Core.Services.Reports;
using Xunit;

namespace TenantManager.Tests;

public class SemanticExecutiveReportTests
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
    public async Task ProcessDashboard_CalculatesCompleteExecutiveReportMetrics()
    {
        // Arrange
        using var db = GetMemoryDbContext();
        var executor = new SemanticQueryExecutor(db);

        var property = new Property { Name = "Piso Centro" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room1 = new Room { Name = "Hab 1", PropertyId = property.Id, IsActive = true, BaseRent = 400m };
        var room2 = new Room { Name = "Hab 2", PropertyId = property.Id, IsActive = true, BaseRent = 450m };
        db.Rooms.AddRange(room1, room2);

        var tenant1 = new Tenant { FullName = "Laura Gomez", PropertyId = property.Id };
        db.Tenants.Add(tenant1);
        await db.SaveChangesAsync();

        var contract1 = new RentalContract
        {
            PropertyId = property.Id,
            TenantId = tenant1.Id,
            RoomId = room1.Id,
            MonthlyRent = 400m,
            StartDate = new DateTimeOffset(new DateTime(2026, 1, 1)),
            EndDate = new DateTimeOffset(new DateTime(2026, 12, 31))
        };
        db.RentalContracts.Add(contract1);

        // Paid payment: 400 €
        db.MonthlyPayments.Add(new MonthlyPayment
        {
            PropertyId = property.Id,
            TenantId = tenant1.Id,
            Year = 2026,
            Month = 1,
            ExpectedRentAmount = 400m,
            PaidAmount = 400m,
            Status = PaymentStatus.Paid
        });

        // Expense: 100 €
        db.ExpenseInvoices.Add(new ExpenseInvoice
        {
            PropertyId = property.Id,
            Year = 2026,
            Month = 1,
            Amount = 100m,
            Concept = "Internet"
        });

        await db.SaveChangesAsync();

        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Dashboard,
            Operation = SemanticQueryOperation.Summary,
            Projection = new List<string> { "totalIncome", "totalExpenses", "profit", "pendingAmount", "occupancyRate" },
            Filters = new List<SemanticQueryFilter>
            {
                new() { Field = "year", Operator = SemanticQueryOperator.Equals, Value = 2026 }
            },
            Language = "es",
            Confidence = 0.95
        };

        var valResult = SemanticQueryPlanValidator.Validate(plan, property.Id);
        Assert.True(valResult.IsValid);

        // Act
        var result = await executor.ExecuteAsync(plan);

        // Assert
        Assert.NotNull(result);
        var dashboard = Assert.IsType<SemanticDashboardResult>(result);
        Assert.True(dashboard.IsExecutiveReport);
        Assert.Equal(400m, dashboard.TotalIncome);
        Assert.Equal(100m, dashboard.TotalExpenses);
        Assert.Equal(300m, dashboard.Profit);
        Assert.Equal(2, dashboard.RoomCount);
        Assert.Equal(1, dashboard.OccupiedRooms);
        Assert.Equal(50.0, dashboard.OccupancyRate);
        Assert.Equal(1, dashboard.ActiveTenantsCount);
    }

    [Fact]
    public void ExecutiveReportGenerator_GeneratesMarkdownAndPdf()
    {
        // Arrange
        var dashboard = new SemanticDashboardResult
        {
            RoomCount = 2,
            OccupiedRooms = 2,
            ActiveTenantsCount = 2,
            OccupancyRate = 100.0,
            TotalIncome = 1200m,
            TotalExpenses = 300m,
            Profit = 900m,
            PendingAmount = 0m,
            PendingPaymentsCount = 0,
            LatePaymentsCount = 0,
            Year = 2026,
            IsExecutiveReport = true
        };

        // Act
        var markdown = ExecutiveReportGenerator.GenerateMarkdown(dashboard, "Piso Principal", isSpanish: true);
        var pdfBytes = ExecutiveReportGenerator.GeneratePdf(dashboard, "Piso Principal", isSpanish: true);

        // Assert
        Assert.Contains("Informe Ejecutivo Financiero", markdown);
        Assert.Contains("1.200,00 €", markdown);
        Assert.Contains("900,00 €", markdown);
        Assert.Contains("100,0%", markdown);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 100);
        var pdfHeader = Encoding.ASCII.GetString(pdfBytes, 0, 5);
        Assert.Equal("%PDF-", pdfHeader);
    }

    [Fact]
    public void SemanticAnswerFormatter_FormatsExecutiveReportSummary()
    {
        // Arrange
        var plan = new SemanticQueryPlan
        {
            Resource = SemanticQueryResource.Dashboard,
            Operation = SemanticQueryOperation.Summary,
            Projection = new List<string> { "totalIncome", "totalExpenses", "profit" },
            Filters = new List<SemanticQueryFilter>
            {
                new() { Field = "year", Operator = SemanticQueryOperator.Equals, Value = 2026 }
            },
            Language = "es"
        };

        var dashboard = new SemanticDashboardResult
        {
            RoomCount = 3,
            OccupiedRooms = 2,
            ActiveTenantsCount = 2,
            OccupancyRate = 66.7,
            TotalIncome = 2000m,
            TotalExpenses = 500m,
            Profit = 1500m,
            PendingAmount = 250m,
            IsExecutiveReport = true
        };

        // Act
        var formatted = SemanticAnswerFormatter.Format(plan, dashboard, "es");

        // Assert
        Assert.Contains("### 📊 Informe Ejecutivo Financiero", formatted);
        Assert.Contains("Ingresos Cobrados:", formatted);
        Assert.Contains("2.000,00 €", formatted);
        Assert.Contains("Beneficio Neto:", formatted);
        Assert.Contains("1.500,00 €", formatted);
    }

    [Fact]
    public void ExecutiveReportGenerator_GeneratesDynamicDashboardHistoryReport()
    {
        // Arrange
        var dashboard = new SemanticDashboardResult
        {
            ReportType = ExecutiveReportType.DashboardHistory,
            TotalIncome = 5000m,
            TotalExpenses = 1500m,
            Profit = 3500m,
            Year = 2026,
            IsExecutiveReport = true,
            MonthlyBreakdown = new List<MonthlyReportItem>
            {
                new() { Year = 2026, Month = 1, MonthName = "Enero", Income = 2500m, Expenses = 800m },
                new() { Year = 2026, Month = 2, MonthName = "Febrero", Income = 2500m, Expenses = 700m }
            }
        };

        // Act
        var markdown = ExecutiveReportGenerator.GenerateMarkdown(dashboard, "Piso Valencia", isSpanish: true);
        var pdfBytes = ExecutiveReportGenerator.GeneratePdf(dashboard, "Piso Valencia", isSpanish: true);

        // Assert
        Assert.Contains("Evolución Histórica del Panel", markdown);
        Assert.Contains("Gráfico de Evolución Mensual", markdown);
        Assert.Contains("🟩", markdown);
        Assert.Contains("🟥", markdown);
        Assert.Contains("Enero", markdown);
        Assert.Contains("Febrero", markdown);
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);

        var pdfContent = Encoding.ASCII.GetString(pdfBytes);
        Assert.Contains("INGRESOS TOTALES", pdfContent);
        Assert.Contains("GASTOS TOTALES", pdfContent);
        Assert.Contains("BENEFICIO ACUMULADO", pdfContent);
        Assert.Contains("GRAFICO COMPARATIVO", pdfContent);
        Assert.Contains("140 0 Td\n(2.500,00 EUR) Tj", pdfContent);
        Assert.Contains("130 0 Td\n(800,00 EUR) Tj", pdfContent);
        Assert.Contains("130 0 Td\n(1.700,00 EUR) Tj", pdfContent);
    }

    [Fact]
    public void ExecutiveReportGenerator_GeneratesDynamicExpensesDetailReport()
    {
        // Arrange
        var dashboard = new SemanticDashboardResult
        {
            ReportType = ExecutiveReportType.ExpensesDetail,
            TotalExpenses = 1800m,
            Year = 2026,
            IsExecutiveReport = true,
            ExpenseCategories = new List<CategoryExpenseReportItem>
            {
                new() { CategoryName = "Suministros", TotalAmount = 1200m, Percentage = 66.7, ChargeableAmount = 1200m },
                new() { CategoryName = "Reparaciones", TotalAmount = 600m, Percentage = 33.3, ChargeableAmount = 0m }
            }
        };

        // Act
        var markdown = ExecutiveReportGenerator.GenerateMarkdown(dashboard, "Piso Valencia", isSpanish: true);
        var pdfBytes = ExecutiveReportGenerator.GeneratePdf(dashboard, "Piso Valencia", isSpanish: true);

        // Assert
        Assert.Contains("Informe Detallado de Gastos", markdown);
        Assert.Contains("Suministros", markdown);
        Assert.Contains("1.200,00", markdown);
        Assert.Contains("66,7%", markdown);
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);
    }

    [Fact]
    public void ExecutiveReportGenerator_GeneratesDynamicOccupancyLeasesReport()
    {
        // Arrange
        var dashboard = new SemanticDashboardResult
        {
            ReportType = ExecutiveReportType.OccupancyLeases,
            RoomCount = 3,
            OccupiedRooms = 2,
            ActiveTenantsCount = 2,
            OccupancyRate = 66.7,
            Year = 2026,
            IsExecutiveReport = true,
            Leases = new List<TenantLeaseReportItem>
            {
                new() { RoomName = "Habitación 1", TenantName = "Carlos", MonthlyRent = 450m, IsActive = true },
                new() { RoomName = "Habitación 2", TenantName = "Marta", MonthlyRent = 400m, IsActive = true }
            }
        };

        // Act
        var markdown = ExecutiveReportGenerator.GenerateMarkdown(dashboard, "Piso Valencia", isSpanish: true);
        var pdfBytes = ExecutiveReportGenerator.GeneratePdf(dashboard, "Piso Valencia", isSpanish: true);

        // Assert
        Assert.Contains("Informe de Ocupación y Contratos", markdown);
        Assert.Contains("Habitación 1", markdown);
        Assert.Contains("Carlos", markdown);
        Assert.Contains("450,00", markdown);
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);
    }
}
