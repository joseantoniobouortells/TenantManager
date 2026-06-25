namespace TenantManager.App.Domain;

public class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
