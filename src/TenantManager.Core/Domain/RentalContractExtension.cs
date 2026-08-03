using System;

namespace TenantManager.App.Domain;

public class RentalContractExtension
{
    public int Id { get; set; }
    
    public int RentalContractId { get; set; }
    
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    
    public decimal MonthlyRent { get; set; }
    public ExpensePaymentType ExpensePaymentType { get; set; } = ExpensePaymentType.Variable;
    public decimal FixedExpenseAmount { get; set; }
    public decimal VariableExpensePercentage { get; set; }

    public string? FilePath { get; set; }
    public byte[]? FileContent { get; set; }
    
    public string? Notes { get; set; }
    public int PropertyId { get; set; }
}
