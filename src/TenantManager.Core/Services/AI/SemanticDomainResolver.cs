using System;
using System.Collections.Generic;
using System.Linq;
using TenantManager.App.Domain;

namespace TenantManager.Core.Services.AI;

/// <summary>
/// Domain semantic resolvers for computed concepts.
/// Centralizes C# logic for effective end dates, extensions, room occupancy, rent computation, etc.
/// </summary>
public static class SemanticDomainResolver
{
    /// <summary>
    /// Gets the effective end date of a contract, considering all its extensions.
    /// </summary>
    public static DateTimeOffset? GetEffectiveEndDate(RentalContract contract, List<RentalContractExtension> extensions)
    {
        if (contract == null) return null;
        var contractExtensions = extensions.Where(e => e.RentalContractId == contract.Id).ToList();
        var validExtensions = contractExtensions.Where(e => e.EndDate.HasValue).ToList();
        if (validExtensions.Any())
        {
            return validExtensions.OrderByDescending(e => e.EndDate).First().EndDate;
        }
        return contract.EndDate;
    }

    /// <summary>
    /// Gets the effective move-out date of a tenant, based on their latest contract and extensions.
    /// </summary>
    public static DateTimeOffset? GetEffectiveTenantMoveOutDate(Tenant tenant, List<RentalContract> contracts, List<RentalContractExtension> extensions)
    {
        if (tenant == null) return null;
        var latestContract = contracts
            .Where(c => c.TenantId == tenant.Id)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault();

        return latestContract != null ? GetEffectiveEndDate(latestContract, extensions) : null;
    }

    /// <summary>
    /// Checks if a room is occupied at a given point in time.
    /// </summary>
    public static bool IsRoomOccupied(Room room, List<RentalContract> contracts, List<RentalContractExtension> extensions, DateTimeOffset now)
    {
        if (room == null) return false;
        var occupiedRoomIds = GetOccupiedRoomIds(contracts, extensions, now);
        return occupiedRoomIds.Contains(room.Id);
    }

    /// <summary>
    /// Checks if a room is active and available at a given point in time.
    /// </summary>
    public static bool IsRoomAvailable(Room room, List<RentalContract> contracts, List<RentalContractExtension> extensions, DateTimeOffset now)
    {
        if (room == null) return false;
        return room.IsActive && !IsRoomOccupied(room, contracts, extensions, now);
    }

    /// <summary>
    /// Gets the list of Room IDs occupied at a given point in time.
    /// </summary>
    public static HashSet<int> GetOccupiedRoomIds(List<RentalContract> contracts, List<RentalContractExtension> extensions, DateTimeOffset now)
    {
        return contracts
            .Where(c => c.StartDate <= now && (GetEffectiveEndDate(c, extensions) == null || GetEffectiveEndDate(c, extensions) >= now))
            .Select(c => c.RoomId)
            .ToHashSet();
    }

    /// <summary>
    /// Resolves the current monthly rent for a room.
    /// </summary>
    public static decimal GetCurrentRentForRoom(Room room, List<RentalContract> contracts, List<RentalContractExtension> extensions, DateTimeOffset now)
    {
        if (room == null) return 0m;
        var activeContract = contracts.FirstOrDefault(c => c.RoomId == room.Id && c.StartDate <= now && (GetEffectiveEndDate(c, extensions) == null || GetEffectiveEndDate(c, extensions) >= now));
        if (activeContract == null) return room.BaseRent;

        var activeExtension = extensions
            .Where(e => e.RentalContractId == activeContract.Id && e.StartDate <= now && (e.EndDate == null || e.EndDate >= now))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefault();

        return activeExtension != null ? activeExtension.MonthlyRent : activeContract.MonthlyRent;
    }

    /// <summary>
    /// Resolves both base rent and calculated expenses for a tenant in a specific billing month.
    /// Variable expenses are computed by dividing active property bills by the count of occupied rooms.
    /// </summary>
    public static (decimal rent, decimal expense) GetRentAndExpenseForMonth(
        RentalContract contract, 
        List<RentalContractExtension> extensions, 
        int year, 
        int month, 
        List<Room> rooms, 
        List<RentalContract> allContracts, 
        List<ExpenseInvoice> allExpenses, 
        List<ExpenseCategory> categories)
    {
        var targetDateStart = new DateTimeOffset(new DateTime(year, month, 1));
        var targetDateEnd = targetDateStart.AddMonths(1).AddDays(-1);

        var activeExtension = extensions
            .Where(e => e.RentalContractId == contract.Id && e.StartDate <= targetDateEnd && (!e.EndDate.HasValue || e.EndDate.Value >= targetDateStart))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefault();

        decimal rent = activeExtension != null ? activeExtension.MonthlyRent : contract.MonthlyRent;
        ExpensePaymentType expenseType = activeExtension != null ? activeExtension.ExpensePaymentType : contract.ExpensePaymentType;
        decimal fixedExpenseAmount = activeExtension != null ? activeExtension.FixedExpenseAmount : contract.FixedExpenseAmount;

        if (expenseType == ExpensePaymentType.Fixed)
        {
            return (rent, fixedExpenseAmount);
        }

        // Variable expense calculation
        var chargeableCategoryIds = categories.Where(c => c.IsChargeable).Select(c => c.Id).ToList();
        var totalExpense = allExpenses
            .Where(i => i.Year == year && i.Month == month && i.PropertyId == contract.PropertyId && chargeableCategoryIds.Contains(i.CategoryId))
            .Sum(i => i.Amount);

        var targetDate = new DateTimeOffset(new DateTime(year, month, 1));
        var occupiedRoomsCount = allContracts
            .Where(c => c.PropertyId == contract.PropertyId)
            .ToList()
            .Where(c => c.StartDate <= targetDate && (c.EndDate == null || c.EndDate >= targetDate))
            .Select(c => c.RoomId)
            .Distinct()
            .Count();

        var variableExpense = occupiedRoomsCount > 0 ? totalExpense / occupiedRoomsCount : 0m;
        return (rent, fixedExpenseAmount + variableExpense);
    }
}
