using System.Collections.Generic;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Lightweight in-session conversation context for the AI assistant.
/// Not persisted to the database. Reset when the app restarts.
/// </summary>
public class AssistantContext
{
    public string? LastResolvedIntent { get; set; }
    public string? LastLanguage { get; set; }
    public string? LastEntityType { get; set; }

    // Extended Semantic Context properties
    public string? LastResource { get; set; }
    public string? LastOperation { get; set; }
    public List<string> LastProjection { get; set; } = new();
    public int? LastTenantId { get; set; }
    public string? LastTenantDisplayName { get; set; }
    public int? LastYear { get; set; }
    public int? LastMonth { get; set; }
    public int? LastPropertyId { get; set; }

    public bool HasContext => LastResolvedIntent != null;

    public void Reset()
    {
        LastResolvedIntent = null;
        LastLanguage = null;
        LastEntityType = null;
        LastResource = null;
        LastOperation = null;
        LastProjection.Clear();
        LastTenantId = null;
        LastTenantDisplayName = null;
        LastYear = null;
        LastMonth = null;
    }
}
