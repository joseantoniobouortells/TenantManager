using System;

namespace TenantManager.App.Domain;

public class MonthlyPayment
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ExpectedRentAmount { get; set; }
    public decimal ExpectedExpenseAmount { get; set; }
    public decimal ExpectedAmount => ExpectedRentAmount + ExpectedExpenseAmount;
    public decimal PaidAmount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }
    public int PropertyId { get; set; }

    public int TenantId { get; set; }
}
