namespace TenantManager.App.Domain;

public class RentalContract
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public int TenantId { get; set; }
}
