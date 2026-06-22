using System;

namespace TenantManager.App.Domain;

public class RoomRentPeriod
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public decimal MonthlyRent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
}
