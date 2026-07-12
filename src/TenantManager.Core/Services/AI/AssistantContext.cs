namespace TenantManager.Core.Services.AI;

/// <summary>
/// Lightweight in-session conversation context for the AI assistant.
/// Not persisted to the database. Reset when the app restarts.
/// </summary>
public class AssistantContext
{
    /// <summary>Last successfully resolved intent (e.g. "tenant_move_out_date").</summary>
    public string? LastResolvedIntent { get; set; }

    /// <summary>Last detected language ("es" or "en").</summary>
    public string? LastLanguage { get; set; }

    /// <summary>Last entity type topic (e.g. "tenantName").</summary>
    public string? LastEntityType { get; set; }

    public bool HasContext => LastResolvedIntent != null;
}
