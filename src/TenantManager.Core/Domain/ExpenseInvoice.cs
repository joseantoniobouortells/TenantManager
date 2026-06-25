using System;

namespace TenantManager.App.Domain;

public class ExpenseInvoice
{
    public int Id { get; set; }
    public string ExpenseType { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public int PropertyId { get; set; }
    public string? FilePath { get; set; }
    public byte[]? FileContent { get; set; }
}
