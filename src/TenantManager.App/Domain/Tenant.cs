using System;

namespace TenantManager.App.Domain;

public class Tenant
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public decimal DepositAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public int? RoomId { get; set; }
}
