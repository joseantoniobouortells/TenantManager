using System.ComponentModel.DataAnnotations;

namespace TenantManager.App.Domain;

public class ExpenseCategory
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public bool IsChargeable { get; set; }
}
