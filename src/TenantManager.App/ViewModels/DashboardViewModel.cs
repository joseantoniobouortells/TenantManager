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

    private double _paidStartAngle;
    private double _paidSweepAngle;
    private double _pendingStartAngle;
    private double _pendingSweepAngle;
    private double _partialStartAngle;
    private double _partialSweepAngle;
    private double _lateStartAngle;
    private double _lateSweepAngle;
    private double _waivedStartAngle;
    private double _waivedSweepAngle;
    private double _occupancySweepAngle;
    private double _incomeSweepAngle;

    public DashboardViewModel()
    {
        _db = new AppDbContext();
        OccupiedRooms = new ObservableCollection<RoomOccupancyItem>();
        PendingPayments = new ObservableCollection<PendingPaymentItem>();
        AvailableRooms = new ObservableCollection<AvailableRoomItem>();
        MissingContracts = new ObservableCollection<MissingContractItem>();

        RefreshCommand = new RelayCommand(_ => Refresh(_currentPropertyId));
    }

    private int _currentPropertyId;

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

    public double PaidStartAngle
    {
        get => _paidStartAngle;
        set => SetProperty(ref _paidStartAngle, value);
    }

    public double PaidSweepAngle
    {
        get => _paidSweepAngle;
        set => SetProperty(ref _paidSweepAngle, value);
    }

    public double PendingStartAngle
    {
        get => _pendingStartAngle;
        set => SetProperty(ref _pendingStartAngle, value);
    }

    public double PendingSweepAngle
    {
        get => _pendingSweepAngle;
        set => SetProperty(ref _pendingSweepAngle, value);
    }

    public double PartialStartAngle
    {
        get => _partialStartAngle;
        set => SetProperty(ref _partialStartAngle, value);
    }

    public double PartialSweepAngle
    {
        get => _partialSweepAngle;
        set => SetProperty(ref _partialSweepAngle, value);
    }

    public double LateStartAngle
    {
        get => _lateStartAngle;
        set => SetProperty(ref _lateStartAngle, value);
    }

    public double LateSweepAngle
    {
        get => _lateSweepAngle;
        set => SetProperty(ref _lateSweepAngle, value);
    }

    public double WaivedStartAngle
    {
        get => _waivedStartAngle;
        set => SetProperty(ref _waivedStartAngle, value);
    }

    public double WaivedSweepAngle
    {
        get => _waivedSweepAngle;
        set => SetProperty(ref _waivedSweepAngle, value);
    }

    public double OccupancySweepAngle
    {
        get => _occupancySweepAngle;
        set => SetProperty(ref _occupancySweepAngle, value);
    }

    public double IncomeSweepAngle
    {
        get => _incomeSweepAngle;
        set => SetProperty(ref _incomeSweepAngle, value);
    }

    public string OccupancyPercentageString => TotalRooms > 0 ? $"{(int)Math.Round(((double)OccupiedRoomsCount / TotalRooms) * 100)}%" : "0%";

    public string IncomeCollectedPercentageString => ExpectedIncome > 0 ? $"{(int)Math.Round(((double)CollectedIncome / (double)ExpectedIncome) * 100)}%" : "0%";

    public void Refresh(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();

        var now = DateTime.Today;
        var nowOffset = new DateTimeOffset(now);
        var currentYear = now.Year;
        var currentMonth = now.Month;

        var rooms = _db.Rooms.Where(r => r.PropertyId == propertyId).ToList();
        var activeTenants = _db.Tenants.Where(t => t.IsActive && t.PropertyId == propertyId).ToList();

        TotalRooms = rooms.Count;
        ActiveTenants = activeTenants.Count;

        var activeContracts = _db.RentalContracts
            .Where(c => c.PropertyId == propertyId)
            .AsEnumerable()
            .Where(c => c.StartDate <= nowOffset && (c.EndDate == null || c.EndDate >= nowOffset))
            .ToList();

        var occupiedRoomIds = activeContracts
            .Select(c => c.RoomId)
            .Distinct()
            .ToHashSet();

        OccupiedRoomsCount = occupiedRoomIds.Count;

        AvailableRoomsCount = TotalRooms - OccupiedRoomsCount;

        var roomLookup = rooms.ToDictionary(r => r.Id, r => r);

        OccupiedRooms.Clear();
        foreach (var contract in activeContracts)
        {
            var tenant = activeTenants.FirstOrDefault(t => t.Id == contract.TenantId);
            if (tenant == null) continue;
            var roomName = roomLookup.TryGetValue(contract.RoomId, out var rn) ? rn.Name : $"(id={contract.RoomId})";
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
                MonthlyRent = room.BaseRent
            });
        }

        var allPayments = _db.MonthlyPayments.Where(p => p.PropertyId == propertyId).ToList();

        var currentMonthPayments = allPayments
            .Where(p => p.Year == currentYear && p.Month == currentMonth)
            .ToList();

        ExpectedIncome = currentMonthPayments.Sum(p => p.ExpectedAmount);
        CollectedIncome = currentMonthPayments.Sum(p => p.PaidAmount);

        TotalPaymentsCount = allPayments.Count;
        PendingPaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Pending);
        PaidPaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Paid);
        PartialPaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Partial);
        LatePaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Late);
        WaivedPaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Waived);

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        PendingPayments.Clear();
        foreach (var payment in allPayments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Late).OrderBy(p => p.Year).ThenBy(p => p.Month))
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

        var contracts = _db.RentalContracts.Where(c => c.PropertyId == propertyId).ToList();
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

        // Donut Chart Angles calculations
        double currentAngle = -90; // Start at 12 o'clock
        double total = TotalPaymentsCount;

        if (total > 0)
        {
            PaidSweepAngle = (PaidPaymentsCount / total) * 360.0;
            PaidStartAngle = currentAngle;
            currentAngle += PaidSweepAngle;

            PendingSweepAngle = (PendingPaymentsCount / total) * 360.0;
            PendingStartAngle = currentAngle;
            currentAngle += PendingSweepAngle;

            PartialSweepAngle = (PartialPaymentsCount / total) * 360.0;
            PartialStartAngle = currentAngle;
            currentAngle += PartialSweepAngle;

            LateSweepAngle = (LatePaymentsCount / total) * 360.0;
            LateStartAngle = currentAngle;
            currentAngle += LateSweepAngle;

            WaivedSweepAngle = (WaivedPaymentsCount / total) * 360.0;
            WaivedStartAngle = currentAngle;
            currentAngle += WaivedSweepAngle;
        }
        else
        {
            PaidSweepAngle = 0; PaidStartAngle = -90;
            PendingSweepAngle = 0; PendingStartAngle = -90;
            PartialSweepAngle = 0; PartialStartAngle = -90;
            LateSweepAngle = 0; LateStartAngle = -90;
            WaivedSweepAngle = 0; WaivedStartAngle = -90;
        }

        OccupancySweepAngle = (TotalRooms > 0) ? ((double)OccupiedRoomsCount / TotalRooms) * 360.0 : 0.0;
        IncomeSweepAngle = (ExpectedIncome > 0) ? (double)(CollectedIncome / ExpectedIncome) * 360.0 : 0.0;

        OnPropertyChanged(nameof(OccupancyPercentageString));
        OnPropertyChanged(nameof(IncomeCollectedPercentageString));

        OnPropertyChanged(nameof(HasNoPendingPayments));
        OnPropertyChanged(nameof(HasNoAvailableRooms));
        OnPropertyChanged(nameof(HasNoMissingContracts));
    }
}
