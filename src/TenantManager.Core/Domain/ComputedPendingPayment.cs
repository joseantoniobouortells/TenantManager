using System;

namespace TenantManager.App.Domain;

/// <summary>
/// Represents a pending payment that is computed dynamically from active contracts.
/// This is NOT persisted in the database.
/// </summary>
public class ComputedPendingPayment
{
    public int TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ExpectedRentAmount { get; set; }
    public decimal ExpectedExpenseAmount { get; set; }
    public decimal TotalExpected => ExpectedRentAmount + ExpectedExpenseAmount;
    public int ContractId { get; set; }
    public string MonthLabel => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    public bool IsOverdue => new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month)) < DateTime.Today;
}
