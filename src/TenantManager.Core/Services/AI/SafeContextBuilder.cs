using System;
using System.Text;

namespace TenantManager.Core.Services.AI;

// DTOs for data passed to the context builder to avoid exposing Entity Framework entities directly
public class TenantContextData
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string? RoomName { get; set; }
    public DateTime? MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
}

public static class SafeContextBuilder
{
    public static string BuildSystemPrompt(string dataContext, bool isSpanish = false)
    {
        string languageInstruction = isSpanish 
            ? "Answer in Spanish (the same language as the user question)." 
            : "Answer in English (the same language as the user question).";

        return $@"You are a helpful, read-only AI assistant for a property management application.
Your goal is to answer the user's question based strictly on the provided context.

RULES:
1. {languageInstruction}
2. Use ONLY the information provided in the Context section below. Do not invent missing dates, names, payments, or contracts.
3. If the answer cannot be determined from the Context, state exactly: 'The information is not available in the provided context.' (or its translation in the requested language).
4. Be concise and clear. Do not hallucinate or guess.

--- CONTEXT ---
{dataContext}
---------------";
    }

    public static string BuildTenantContext(TenantContextData tenantData)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tenant Name: {tenantData.FullName}");
        
        if (!string.IsNullOrWhiteSpace(tenantData.RoomName))
        {
            sb.AppendLine($"Assigned Room: {tenantData.RoomName}");
        }

        if (tenantData.MoveInDate.HasValue)
        {
            sb.AppendLine($"Move-in Date: {tenantData.MoveInDate.Value:yyyy-MM-dd}");
        }

        if (tenantData.MoveOutDate.HasValue)
        {
            sb.AppendLine($"Move-out Date: {tenantData.MoveOutDate.Value:yyyy-MM-dd}");
        }
        else
        {
            sb.AppendLine("Move-out Date: Not specified (ongoing contract)");
        }

        // REDACT PII: We explicitly DO NOT include Phone, Email, or private Notes in the generated context text.
        // This ensures the LLM never sees them.

        return sb.ToString();
    }
}
