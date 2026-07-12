using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.Core.Services.AI;

public class IntentExtractionResult
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "unknown";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.0;

    [JsonPropertyName("entities")]
    public JsonElement Entities { get; set; }
}

public class AiQueryService
{
    private readonly AppDbContext _dbContext;
    private readonly LocalAiClient _aiClient;

    public AiQueryService(AppDbContext dbContext, LocalAiClient aiClient)
    {
        _dbContext = dbContext;
        _aiClient = aiClient;
    }

    public async Task<(string? ContextData, bool IsSpanish, string? ClarificationMessage)> ResolveIntentAndGetDataAsync(string userMessage)
    {
        bool isSpanish = false;
        string? clarificationMessage = null;
        var lowerMsg = userMessage.ToLowerInvariant();
        isSpanish = lowerMsg.Contains("cuando") || lowerMsg.Contains("qué") || lowerMsg.Contains("habitación") || lowerMsg.Contains("cuánt") || lowerMsg.Contains("estado") || lowerMsg.Contains("pago");

        var json = await _aiClient.ExtractIntentAsync(userMessage);
        IntentExtractionResult? extraction = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (json.StartsWith("```"))
                {
                    var firstNewline = json.IndexOf('\n');
                    var lastBackticks = json.LastIndexOf("```", StringComparison.Ordinal);
                    if (firstNewline != -1 && lastBackticks != -1 && lastBackticks > firstNewline)
                    {
                        json = json.Substring(firstNewline + 1, lastBackticks - firstNewline - 1);
                    }
                }
                extraction = JsonSerializer.Deserialize<IntentExtractionResult>(json);
            }
            catch { }
        }

        if (extraction != null)
        {
            isSpanish = extraction.Language.StartsWith("es", StringComparison.InvariantCultureIgnoreCase);
            
            if (extraction.Confidence < 0.6)
            {
                return (null, isSpanish, null);
            }

            if (extraction.Intent == "tenant_move_out_date" || extraction.Intent == "tenant_current_room")
            {
                string? tenantName = null;
                if (extraction.Entities.ValueKind == JsonValueKind.Object && extraction.Entities.TryGetProperty("tenantName", out var nameProp))
                {
                    tenantName = nameProp.GetString();
                }

                if (!string.IsNullOrWhiteSpace(tenantName))
                {
                    var tenants = await _dbContext.Tenants.ToListAsync();
                    
                    var exactMatch = tenants.FirstOrDefault(t => t.FullName.Equals(tenantName, StringComparison.InvariantCultureIgnoreCase));
                    if (exactMatch != null)
                    {
                        return (await BuildTenantContextData(exactMatch), isSpanish, null);
                    }

                    var partialMatches = tenants.Where(t => 
                        t.FullName.ToLowerInvariant().Contains(tenantName.ToLowerInvariant()) || 
                        tenantName.ToLowerInvariant().Contains(t.FullName.ToLowerInvariant())).ToList();

                    if (partialMatches.Count == 1 && extraction.Confidence >= 0.8)
                    {
                        return (await BuildTenantContextData(partialMatches[0]), isSpanish, null);
                    }
                    else if (partialMatches.Count > 0)
                    {
                        clarificationMessage = isSpanish 
                            ? $"No encuentro un inquilino con el nombre exacto de {tenantName}. ¿Te refieres a {partialMatches[0].FullName}?"
                            : $"I cannot find a tenant with the exact name {tenantName}. Do you mean {partialMatches[0].FullName}?";
                        return (null, isSpanish, clarificationMessage);
                    }
                    else
                    {
                        clarificationMessage = isSpanish 
                            ? $"No encuentro ningún inquilino llamado {tenantName}."
                            : $"I cannot find any tenant named {tenantName}.";
                        return (null, isSpanish, clarificationMessage);
                    }
                }
            }
            else if (extraction.Intent == "dashboard_summary" || extraction.Intent == "available_rooms" || extraction.Intent == "pending_or_late_payments")
            {
                 var rooms = await _dbContext.Rooms.ToListAsync();
                 var tenantsCount = await _dbContext.Tenants.CountAsync();
                 return ($"Data Context: App has {rooms.Count} rooms and {tenantsCount} tenants.", isSpanish, null);
            }
        }
        
        return (null, isSpanish, null);
    }

    private async Task<string> BuildTenantContextData(Tenant matchedTenant)
    {
        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == matchedTenant.Id);
        var contracts = await _dbContext.RentalContracts
            .Where(c => c.TenantId == matchedTenant.Id)
            .ToListAsync();
        var latestContract = contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();

        var contextData = new TenantContextData
        {
            FullName = matchedTenant.FullName,
            RoomName = room?.Name,
            MoveInDate = latestContract?.StartDate.DateTime,
            MoveOutDate = latestContract?.EndDate?.DateTime
        };

        return SafeContextBuilder.BuildTenantContext(contextData);
    }
}
