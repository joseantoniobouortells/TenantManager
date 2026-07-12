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

        // Intent: Tenant Move-out date
        // Example: "When does Erik Artigas move out?" or "move out date for erik"
        if (lowerMsg.Contains("move out") || lowerMsg.Contains("leave"))
        {
            // Simple deterministic extraction for Phase 4: Try to find a tenant name in the message
            var tenants = await _dbContext.Tenants.ToListAsync();
            
            Tenant? matchedTenant = null;
            foreach (var t in tenants)
            {
                // Basic matching: if the tenant's first or full name is in the message
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
                // Fetch room and contract data
                var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == matchedTenant.Id); // Note: Simplified relation matching for MVP
                
                // Find the latest contract for this tenant
                var contracts = await _dbContext.RentalContracts
                    .Where(c => c.TenantId == matchedTenant.Id)
                    .ToListAsync();
                    
                var latestContract = contracts
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefault();

                var contextData = new TenantContextData
                {
                    FullName = matchedTenant.FullName,
                    Phone = matchedTenant.Phone,
                    Email = matchedTenant.Email,
                    Notes = matchedTenant.Notes,
                    RoomName = room?.Name,
                    MoveInDate = latestContract?.StartDate.DateTime,
                    MoveOutDate = latestContract?.EndDate?.DateTime
                };

                return SafeContextBuilder.BuildTenantContext(contextData);
            }
        }

        // Return null if we don't understand the intent
        return null;
    }
}
