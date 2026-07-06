using System;

namespace TenantManager.App.Domain;

public class ExpenseInvoice
{
    public int Id { get; set; }
    public string Concept { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public int PropertyId { get; set; }
    public string? FilePath { get; set; }
    public byte[]? FileContent { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HasFile => FileContent != null || !string.IsNullOrWhiteSpace(FilePath);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string CategoryName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsChargeableToTenant { get; set; }
}
