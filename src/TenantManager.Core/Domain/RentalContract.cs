using System;

namespace TenantManager.App.Domain;

public class RentalContract
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public byte[]? FileContent { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }

    public decimal MonthlyRent { get; set; }
    public ExpensePaymentType ExpensePaymentType { get; set; } = ExpensePaymentType.Variable;
    public decimal FixedExpenseAmount { get; set; }
    public int PaymentDay { get; set; } = 1;

    public string? Notes { get; set; }

    public int TenantId { get; set; }
    public int RoomId { get; set; }
    public decimal DepositAmount { get; set; }
}
