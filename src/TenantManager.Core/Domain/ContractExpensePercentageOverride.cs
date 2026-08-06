using System;

namespace TenantManager.App.Domain;

public class ContractExpensePercentageOverride
{
    public int Id { get; set; }
    public int RentalContractId { get; set; }
    public int CategoryId { get; set; }
    public decimal Percentage { get; set; }

    public RentalContract? RentalContract { get; set; }
    public ExpenseCategory? Category { get; set; }
}
