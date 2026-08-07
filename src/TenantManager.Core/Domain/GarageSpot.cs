namespace TenantManager.App.Domain;

public class GarageSpot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal BaseRent { get; set; }
    public bool IsActive { get; set; } = true;
    public int PropertyId { get; set; }
    public string? Notes { get; set; }
}
