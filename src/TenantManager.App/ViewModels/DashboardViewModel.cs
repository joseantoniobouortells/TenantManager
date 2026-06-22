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
    public string Status { get; set; } = string.Empty;
}

public class AvailableRoomItem
{
    public string RoomName { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
}

public class MissingContractItem
{
    public string TenantName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class DashboardViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private int _totalRooms;
    private int _occupiedRooms;
    private int _activeTenants;
    private int _pendingPaymentsCount;

    private int _availableRoomsCount;
    private decimal _expectedIncome;
    private decimal _collectedIncome;

    private int _paidPaymentsCount;
    private int _partialPaymentsCount;
    private int _latePaymentsCount;
    private int _waivedPaymentsCount;
    private int _totalPaymentsCount;

    public DashboardViewModel()
    {
        _db = new AppDbContext();
        OccupiedRooms = new ObservableCollection<RoomOccupancyItem>();
        PendingPayments = new ObservableCollection<PendingPaymentItem>();
        AvailableRooms = new ObservableCollection<AvailableRoomItem>();
        MissingContracts = new ObservableCollection<MissingContractItem>();

        RefreshCommand = new RelayCommand(_ => Refresh());

        Refresh();
    }

    public ObservableCollection<RoomOccupancyItem> OccupiedRooms { get; }
    public ObservableCollection<PendingPaymentItem> PendingPayments { get; }
    public ObservableCollection<AvailableRoomItem> AvailableRooms { get; }
    public ObservableCollection<MissingContractItem> MissingContracts { get; }

    public bool HasNoPendingPayments => PendingPayments.Count == 0;
    public bool HasNoAvailableRooms => AvailableRooms.Count == 0;
    public bool HasNoMissingContracts => MissingContracts.Count == 0;

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

    public int AvailableRoomsCount
    {
        get => _availableRoomsCount;
        set => SetProperty(ref _availableRoomsCount, value);
    }

    public decimal ExpectedIncome
    {
        get => _expectedIncome;
        set => SetProperty(ref _expectedIncome, value);
    }

    public decimal CollectedIncome
    {
        get => _collectedIncome;
        set => SetProperty(ref _collectedIncome, value);
    }

    public int PaidPaymentsCount
    {
        get => _paidPaymentsCount;
        set => SetProperty(ref _paidPaymentsCount, value);
    }

    public int PartialPaymentsCount
    {
        get => _partialPaymentsCount;
        set => SetProperty(ref _partialPaymentsCount, value);
    }

    public int LatePaymentsCount
    {
        get => _latePaymentsCount;
        set => SetProperty(ref _latePaymentsCount, value);
    }

    public int WaivedPaymentsCount
    {
        get => _waivedPaymentsCount;
        set => SetProperty(ref _waivedPaymentsCount, value);
    }

    public int TotalPaymentsCount
    {
        get => _totalPaymentsCount;
        set => SetProperty(ref _totalPaymentsCount, value);
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

        AvailableRoomsCount = TotalRooms - OccupiedRoomsCount;

        var roomLookup = rooms.ToDictionary(r => r.Id, r => r);

        OccupiedRooms.Clear();
        foreach (var tenant in activeTenants.Where(t => t.RoomId.HasValue))
        {
            var roomName = roomLookup.TryGetValue(tenant.RoomId!.Value, out var rn) ? rn.Name : $"(id={tenant.RoomId})";
            OccupiedRooms.Add(new RoomOccupancyItem
            {
                RoomName = roomName,
                TenantName = tenant.FullName
            });
        }

        AvailableRooms.Clear();
        foreach (var room in rooms.Where(r => r.IsActive && !occupiedRoomIds.Contains(r.Id)))
        {
            AvailableRooms.Add(new AvailableRoomItem
            {
                RoomName = room.Name,
                MonthlyRent = room.MonthlyRent
            });
        }

        var currentMonthPayments = _db.MonthlyPayments
            .Where(p => p.Year == currentYear && p.Month == currentMonth)
            .ToList();

        ExpectedIncome = currentMonthPayments.Sum(p => p.ExpectedAmount);
        CollectedIncome = currentMonthPayments.Sum(p => p.PaidAmount);

        TotalPaymentsCount = currentMonthPayments.Count;
        PendingPaymentsCount = currentMonthPayments.Count(p => p.Status == PaymentStatus.Pending);
        PaidPaymentsCount = currentMonthPayments.Count(p => p.Status == PaymentStatus.Paid);
        PartialPaymentsCount = currentMonthPayments.Count(p => p.Status == PaymentStatus.Partial);
        LatePaymentsCount = currentMonthPayments.Count(p => p.Status == PaymentStatus.Late);
        WaivedPaymentsCount = currentMonthPayments.Count(p => p.Status == PaymentStatus.Waived);

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        PendingPayments.Clear();
        foreach (var payment in currentMonthPayments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Late))
        {
            PendingPayments.Add(new PendingPaymentItem
            {
                TenantName = tenantLookup.TryGetValue(payment.TenantId, out var name) ? name : $"(id={payment.TenantId})",
                Year = payment.Year,
                Month = payment.Month,
                ExpectedAmount = payment.ExpectedAmount,
                Status = payment.Status.ToString()
            });
        }

        var contracts = _db.RentalContracts.ToList();
        MissingContracts.Clear();
        foreach (var contract in contracts)
        {
            if (!string.IsNullOrWhiteSpace(contract.FilePath) && !System.IO.File.Exists(contract.FilePath))
            {
                MissingContracts.Add(new MissingContractItem
                {
                    TenantName = tenantLookup.TryGetValue(contract.TenantId, out var name) ? name : $"(id={contract.TenantId})",
                    FilePath = contract.FilePath
                });
            }
        }

        OnPropertyChanged(nameof(HasNoPendingPayments));
        OnPropertyChanged(nameof(HasNoAvailableRooms));
        OnPropertyChanged(nameof(HasNoMissingContracts));
    }
}
