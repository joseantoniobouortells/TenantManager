using System;

namespace TenantManager.App.Domain;

public class RentalContract
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }

    public int TenantId { get; set; }
}
