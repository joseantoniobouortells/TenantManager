using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class RoomOccupancyItem
{
    public string RoomName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
}

public class PendingPaymentItem
{
    public string TenantName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ExpectedAmount { get; set; }
}

public class DashboardViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private int _totalRooms;
    private int _occupiedRooms;
    private int _activeTenants;
    private int _pendingPaymentsCount;

    public DashboardViewModel()
    {
        _db = new AppDbContext();
        OccupiedRooms = new ObservableCollection<RoomOccupancyItem>();
        PendingPayments = new ObservableCollection<PendingPaymentItem>();

        RefreshCommand = new RelayCommand(_ => Refresh());

        Refresh();
    }

    public ObservableCollection<RoomOccupancyItem> OccupiedRooms { get; }
    public ObservableCollection<PendingPaymentItem> PendingPayments { get; }

    public RelayCommand RefreshCommand { get; }

    public int TotalRooms
    {
        get => _totalRooms;
        set => SetProperty(ref _totalRooms, value);
    }

    public int OccupiedRoomsCount
    {
        get => _occupiedRooms;
        set => SetProperty(ref _occupiedRooms, value);
    }

    public int ActiveTenants
    {
        get => _activeTenants;
        set => SetProperty(ref _activeTenants, value);
    }

    public int PendingPaymentsCount
    {
        get => _pendingPaymentsCount;
        set => SetProperty(ref _pendingPaymentsCount, value);
    }

    public void Refresh()
    {
        var now = DateTime.Today;
        var currentYear = now.Year;
        var currentMonth = now.Month;

        var rooms = _db.Rooms.ToList();
        var activeTenants = _db.Tenants.Where(t => t.IsActive).ToList();

        TotalRooms = rooms.Count;
        ActiveTenants = activeTenants.Count;

        var occupiedRoomIds = activeTenants
            .Where(t => t.RoomId.HasValue)
            .Select(t => t.RoomId!.Value)
            .Distinct()
            .ToHashSet();

        OccupiedRoomsCount = occupiedRoomIds.Count;

        var roomLookup = rooms.ToDictionary(r => r.Id, r => r.Name);

        OccupiedRooms.Clear();
        foreach (var tenant in activeTenants.Where(t => t.RoomId.HasValue))
        {
            var roomName = roomLookup.TryGetValue(tenant.RoomId!.Value, out var rn) ? rn : $"(id={tenant.RoomId})";
            OccupiedRooms.Add(new RoomOccupancyItem
            {
                RoomName = roomName,
                TenantName = tenant.FullName
            });
        }

        var pendingQuery = _db.MonthlyPayments
            .Where(p => p.Year == currentYear && p.Month == currentMonth && p.Status == PaymentStatus.Pending)
            .ToList();

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);
        PendingPaymentsCount = pendingQuery.Count;

        PendingPayments.Clear();
        foreach (var payment in pendingQuery)
        {
            PendingPayments.Add(new PendingPaymentItem
            {
                TenantName = tenantLookup.TryGetValue(payment.TenantId, out var name) ? name : $"(id={payment.TenantId})",
                Year = payment.Year,
                Month = payment.Month,
                ExpectedAmount = payment.ExpectedAmount
            });
        }
    }
}
