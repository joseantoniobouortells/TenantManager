using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.Core.Services.AI;

public class AiQueryService
{
    private readonly AppDbContext _dbContext;

    public AiQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Attempts to deterministically resolve the user's intent and fetch the required data.
    /// If an intent is recognized, returns the populated TenantContextData or relevant context string.
    /// If no intent is recognized, returns null.
    /// </summary>
    public async Task<string?> ResolveIntentAndGetDataAsync(string userMessage)
    {
        var lowerMsg = userMessage.ToLowerInvariant();
        var isSpanish = lowerMsg.Contains("cuando") || lowerMsg.Contains("qué") || lowerMsg.Contains("habitación") || lowerMsg.Contains("cuánt") || lowerMsg.Contains("estado") || lowerMsg.Contains("pago");

        // Intents
        bool isMoveOut = lowerMsg.Contains("move out") || lowerMsg.Contains("leave") || lowerMsg.Contains("deja") || lowerMsg.Contains("se va") || lowerMsg.Contains("sale");
        bool isRoom = lowerMsg.Contains("room") || lowerMsg.Contains("habitación") || lowerMsg.Contains("cuarto");
        bool isAvailable = lowerMsg.Contains("available") || lowerMsg.Contains("disponible") || lowerMsg.Contains("libre");
        bool isPayments = lowerMsg.Contains("payment") || lowerMsg.Contains("pago") || lowerMsg.Contains("deuda");
        bool isSummary = lowerMsg.Contains("summary") || lowerMsg.Contains("dashboard") || lowerMsg.Contains("resumen");

        if (isMoveOut || isRoom)
        {
            var tenants = await _dbContext.Tenants.ToListAsync();
            Tenant? matchedTenant = null;
            foreach (var t in tenants)
            {
                var nameParts = t.FullName.ToLowerInvariant().Split(' ');
                if (lowerMsg.Contains(t.FullName.ToLowerInvariant()) || 
                    (nameParts.Length > 0 && lowerMsg.Contains(nameParts[0])))
                {
                    matchedTenant = t;
                    break;
                }
            }

            if (matchedTenant != null)
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
        else if (isAvailable || isSummary || isPayments)
        {
             // For now, return a generic mock summary based on DB
             var rooms = await _dbContext.Rooms.ToListAsync();
             var tenantsCount = await _dbContext.Tenants.CountAsync();
             return $"Data Context: App has {rooms.Count} rooms and {tenantsCount} tenants. More detailed summary logic can be added here.";
        }

        return null;
    }
}
