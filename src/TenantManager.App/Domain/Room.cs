namespace TenantManager.App.Domain;

public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
