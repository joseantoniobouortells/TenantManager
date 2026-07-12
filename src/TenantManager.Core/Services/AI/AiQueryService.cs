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

    public async Task<(string? FinalAnswer, bool IsSpanish)> ResolveIntentAndGetDataAsync(string userMessage)
    {
        bool isSpanish = false;
        var lowerMsg = NormalizeString(userMessage);
        
        // Fast heuristic for language
        isSpanish = lowerMsg.Contains("cuando") || lowerMsg.Contains("qué") || lowerMsg.Contains("habitacion") || lowerMsg.Contains("cuant") || lowerMsg.Contains("estado") || lowerMsg.Contains("pago");

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

        string intent = "unknown";
        string? tenantName = null;
        double confidence = 0;

        if (extraction != null)
        {
            isSpanish = extraction.Language.StartsWith("es", StringComparison.InvariantCultureIgnoreCase);
            confidence = extraction.Confidence;
            intent = extraction.Intent;

            if (extraction.Entities.ValueKind == JsonValueKind.Object && extraction.Entities.TryGetProperty("tenantName", out var nameProp))
            {
                tenantName = nameProp.GetString();
            }
        }
        else
        {
            // FALLBACK KEYWORD ROUTING (Isolated)
            bool isMoveOut = lowerMsg.Contains("move out") || lowerMsg.Contains("leave") || lowerMsg.Contains("deja") || lowerMsg.Contains("se va") || lowerMsg.Contains("sale");
            bool isRoom = lowerMsg.Contains("room") || lowerMsg.Contains("habitacion") || lowerMsg.Contains("cuarto");
            if (isMoveOut) intent = "tenant_move_out_date";
            else if (isRoom) intent = "tenant_current_room";
            
            // Very naive name extraction fallback
            tenantName = userMessage; // Just pass the whole message to the matcher as a desperate fallback
            confidence = 1.0; 
        }

        if (confidence < 0.5 && extraction != null)
        {
            return (null, isSpanish);
        }

        if (intent == "tenant_move_out_date" || intent == "tenant_current_room")
        {
            if (string.IsNullOrWhiteSpace(tenantName)) return (null, isSpanish);

            var tenants = await _dbContext.Tenants.ToListAsync();
            var targetNorm = NormalizeString(tenantName);
            var targetTokens = targetNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var exactMatch = tenants.FirstOrDefault(t => NormalizeString(t.FullName) == targetNorm);
            Tenant? bestMatch = exactMatch;

            if (bestMatch == null)
            {
                var partialMatches = tenants.Where(t => 
                {
                    var tNorm = NormalizeString(t.FullName);
                    return targetTokens.All(token => tNorm.Contains(token));
                }).ToList();

                if (partialMatches.Count == 1)
                {
                    bestMatch = partialMatches[0];
                }
                else if (partialMatches.Count > 1)
                {
                    var names = string.Join(", ", partialMatches.Select(p => p.FullName));
                    string clar = isSpanish 
                        ? $"He encontrado varios inquilinos parecidos: {names}. ¿A cuál te refieres?"
                        : $"I found multiple similar tenants: {names}. Which one do you mean?";
                    return (clar, isSpanish);
                }
                else
                {
                    string notFound = isSpanish 
                        ? $"No encuentro un inquilino llamado {tenantName}."
                        : $"I cannot find a tenant named {tenantName}.";
                    return (notFound, isSpanish);
                }
            }

            var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == bestMatch.Id);
            var contracts = await _dbContext.RentalContracts
                .Where(c => c.TenantId == bestMatch.Id)
                .ToListAsync();
            var latestContract = contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();

            if (intent == "tenant_move_out_date")
            {
                if (latestContract?.EndDate.HasValue == true)
                {
                    var dateStr = latestContract.EndDate.Value.ToString("yyyy-MM-dd");
                    var ans = isSpanish 
                        ? $"{bestMatch.FullName} deja la habitación el {dateStr}."
                        : $"{bestMatch.FullName} is scheduled to move out on {dateStr}.";
                    return (ans, isSpanish);
                }
                else
                {
                    var ans = isSpanish 
                        ? $"No hay fecha de salida registrada para {bestMatch.FullName}."
                        : $"There is no move-out date registered for {bestMatch.FullName}.";
                    return (ans, isSpanish);
                }
            }
            else if (intent == "tenant_current_room")
            {
                var rName = room?.Name ?? (isSpanish ? "ninguna habitación" : "no room");
                var ans = isSpanish 
                    ? $"{bestMatch.FullName} está en la habitación {rName}."
                    : $"{bestMatch.FullName} is assigned to {rName}.";
                return (ans, isSpanish);
            }
        }
        else if (intent == "dashboard_summary" || intent == "available_rooms" || intent == "pending_or_late_payments")
        {
             var rooms = await _dbContext.Rooms.ToListAsync();
             var tenantsCount = await _dbContext.Tenants.CountAsync();
             var ans = isSpanish 
                ? $"Resumen: La aplicación tiene {rooms.Count} habitaciones y {tenantsCount} inquilinos."
                : $"Summary: The app has {rooms.Count} rooms and {tenantsCount} tenants.";
             return (ans, isSpanish);
        }
        
        return (null, isSpanish);
    }

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = input.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        var noAccents = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        return string.Join(" ", noAccents.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
