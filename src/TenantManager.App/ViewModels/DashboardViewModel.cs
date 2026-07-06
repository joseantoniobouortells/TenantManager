using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class RoomOccupancyItem
{
    public string RoomName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
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

public class MonthlyBarChartItem
{
    public string MonthName { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public double IncomeHeight { get; set; }
    public double ExpenseHeight { get; set; }
    public string IncomeString => Income.ToString("C0");
    public string ExpenseString => Expenses.ToString("C0");
}

public class IntervalOption
{
    public string DisplayText { get; set; } = string.Empty;
    public int Value { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is IntervalOption other && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}

public class DashboardViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private int _totalRooms;
    private int _occupiedRooms;
    private int _activeTenants;

    private int _availableRoomsCount;
    private decimal _expectedIncome;
    private decimal _collectedIncome;

    private int _paidPaymentsCount;
    private int _partialPaymentsCount;
    private int _totalPaymentsCount;

    private double _paidStartAngle;
    private double _paidSweepAngle;
    private double _pendingStartAngle;
    private double _pendingSweepAngle;
    private double _partialStartAngle;
    private double _partialSweepAngle;
    private double _occupancySweepAngle;
    private double _incomeSweepAngle;

    private IntervalOption _selectedInterval = null!;
    private decimal _totalPeriodIncome;
    private decimal _totalPeriodExpenses;
    private decimal _periodProfit;

    public DashboardViewModel()
    {
        _db = new AppDbContext();
        OccupiedRooms = new ObservableCollection<RoomOccupancyItem>();
        PendingPayments = new ObservableCollection<ComputedPendingPayment>();
        AvailableRooms = new ObservableCollection<AvailableRoomItem>();
        MissingContracts = new ObservableCollection<MissingContractItem>();
        MonthlyBarChartItems = new ObservableCollection<MonthlyBarChartItem>();

        AvailableIntervals = new ObservableCollection<IntervalOption>
        {
            new() { DisplayText = "3 meses", Value = 3 },
            new() { DisplayText = "6 meses", Value = 6 },
            new() { DisplayText = "12 meses", Value = 12 },
            new() { DisplayText = "Este año", Value = -2 },
            new() { DisplayText = "Desde el inicio", Value = -1 }
        };
        _selectedInterval = AvailableIntervals[1]; // default 6 months
        _lastNotificationTime = DateTime.MinValue;

        RefreshCommand = new RelayCommand(_ => Refresh(_currentPropertyId));
    }

    private int _currentPropertyId;

    public ObservableCollection<RoomOccupancyItem> OccupiedRooms { get; }
    public ObservableCollection<ComputedPendingPayment> PendingPayments { get; }
    public ObservableCollection<AvailableRoomItem> AvailableRooms { get; }
    public ObservableCollection<MissingContractItem> MissingContracts { get; }
    public ObservableCollection<MonthlyBarChartItem> MonthlyBarChartItems { get; } = new();
    public ObservableCollection<CategoryDonutItem> ExpenseCategoryChartItems { get; } = new();
    public ObservableCollection<IntervalOption> AvailableIntervals { get; }

    public bool HasNoPendingPayments => PendingPayments.Count == 0;
    public bool HasExpenseCategoryChartItems => ExpenseCategoryChartItems.Count > 0;
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

    private decimal _estimatedNextMonthIncome;
    public decimal EstimatedNextMonthIncome
    {
        get => _estimatedNextMonthIncome;
        set => SetProperty(ref _estimatedNextMonthIncome, value);
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

    public IntervalOption SelectedInterval
    {
        get => _selectedInterval;
        set
        {
            if (SetProperty(ref _selectedInterval, value))
            {
                Refresh(_currentPropertyId);
            }
        }
    }

    public decimal TotalPeriodIncome
    {
        get => _totalPeriodIncome;
        set => SetProperty(ref _totalPeriodIncome, value);
    }

    public decimal TotalPeriodExpenses
    {
        get => _totalPeriodExpenses;
        set => SetProperty(ref _totalPeriodExpenses, value);
    }

    public decimal PeriodProfit
    {
        get => _periodProfit;
        set => SetProperty(ref _periodProfit, value);
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
        var allTenants = _db.Tenants.Where(t => t.PropertyId == propertyId).ToList();

        TotalRooms = rooms.Count;

        var propertyContracts = _db.RentalContracts.Where(c => c.PropertyId == propertyId).ToList();
        var propertyContractIds = propertyContracts.Select(c => c.Id).ToList();
        var propertyExtensions = _db.RentalContractExtensions.Where(e => propertyContractIds.Contains(e.RentalContractId)).ToList();

        // Active contracts TODAY (for Occupancy and Tenant counts)
        var activeContracts = propertyContracts
            .Where(c => 
                (c.StartDate <= nowOffset && (c.EndDate == null || c.EndDate >= nowOffset)) ||
                propertyExtensions.Any(e => e.RentalContractId == c.Id && e.StartDate <= nowOffset && (e.EndDate == null || e.EndDate >= nowOffset)))
            .ToList();

        var occupiedRoomIds = activeContracts
            .Select(c => c.RoomId)
            .Distinct()
            .ToHashSet();

        OccupiedRoomsCount = occupiedRoomIds.Count;
        ActiveTenants = activeContracts.Select(c => c.TenantId).Distinct().Count();

        AvailableRoomsCount = TotalRooms - OccupiedRoomsCount;

        var nextMonthDate = now.AddMonths(1);
        int daysInNextMonth = DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month);

        // Calculate Estimated Income iterating per day to perfectly prorate base and extensions
        decimal estimatedIncome = 0;
        foreach (var contract in propertyContracts)
        {
            var extensions = propertyExtensions.Where(e => e.RentalContractId == contract.Id).ToList();

            for (int day = 1; day <= daysInNextMonth; day++)
            {
                var date = new DateTimeOffset(nextMonthDate.Year, nextMonthDate.Month, day, 12, 0, 0, TimeSpan.Zero);

                var activeExt = extensions.FirstOrDefault(e => e.StartDate <= date && (e.EndDate == null || e.EndDate >= date));
                if (activeExt != null)
                {
                    decimal monthlyTotal = activeExt.MonthlyRent + (activeExt.ExpensePaymentType == ExpensePaymentType.Fixed ? activeExt.FixedExpenseAmount : 0);
                    estimatedIncome += monthlyTotal / daysInNextMonth;
                }
                else if (contract.StartDate <= date && (contract.EndDate == null || contract.EndDate >= date))
                {
                    decimal monthlyTotal = contract.MonthlyRent + (contract.ExpensePaymentType == ExpensePaymentType.Fixed ? contract.FixedExpenseAmount : 0);
                    estimatedIncome += monthlyTotal / daysInNextMonth;
                }
            }
        }
        EstimatedNextMonthIncome = estimatedIncome;

        var roomLookup = rooms.ToDictionary(r => r.Id, r => r);

        OccupiedRooms.Clear();
        foreach (var contract in activeContracts)
        {
            var tenant = allTenants.FirstOrDefault(t => t.Id == contract.TenantId);
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

        // Auto-repair: If a payment is marked as Paid but PaidAmount is 0, update it to match expected amount
        bool dbRepaired = false;
        foreach (var payment in allPayments.Where(p => p.Status == PaymentStatus.Paid && p.PaidAmount == 0 && p.ExpectedAmount > 0))
        {
            payment.PaidAmount = payment.ExpectedAmount;
            if (payment.PaidDate == null)
            {
                payment.PaidDate = now;
            }
            _db.MonthlyPayments.Update(payment);
            dbRepaired = true;
        }
        if (dbRepaired)
        {
            _db.SaveChanges();
            allPayments = _db.MonthlyPayments.Where(p => p.PropertyId == propertyId).ToList();
        }

        var currentMonthPayments = allPayments
            .Where(p => p.Year == currentYear && p.Month == currentMonth)
            .ToList();

        ExpectedIncome = currentMonthPayments.Sum(p => p.ExpectedAmount);
        CollectedIncome = currentMonthPayments.Sum(p => p.PaidAmount);

        TotalPaymentsCount = allPayments.Count;
        PaidPaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Paid);
        PartialPaymentsCount = allPayments.Count(p => p.Status == PaymentStatus.Partial);

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        // Compute pending payments dynamically from contracts
        var contracts = _db.RentalContracts.Where(c => c.PropertyId == propertyId).ToList();
        var contractIds = contracts.Select(c => c.Id).ToList();
        var allExtensions = _db.RentalContractExtensions.Where(e => contractIds.Contains(e.RentalContractId)).ToList();
        var paidMonths = allPayments.Select(p => (p.TenantId, p.Year, p.Month)).ToHashSet();

        PendingPayments.Clear();
        var pendingList = new List<ComputedPendingPayment>();
        foreach (var contract in contracts)
        {
            var contractExtensions = allExtensions.Where(e => e.RentalContractId == contract.Id).ToList();
            var startDate = new DateTime(contract.StartDate.Year, contract.StartDate.Month, 1);

            DateTime? effectiveEnd = null;
            if (contract.EndDate.HasValue)
                effectiveEnd = new DateTime(contract.EndDate.Value.Year, contract.EndDate.Value.Month, 1);
            foreach (var ext in contractExtensions)
            {
                if (!ext.EndDate.HasValue) { effectiveEnd = null; break; }
                var extEnd = new DateTime(ext.EndDate.Value.Year, ext.EndDate.Value.Month, 1);
                if (effectiveEnd == null || extEnd > effectiveEnd) effectiveEnd = extEnd;
            }

            var cutoff = effectiveEnd.HasValue
                ? new DateTime(Math.Min(effectiveEnd.Value.Ticks, new DateTime(now.Year, now.Month, 1).Ticks))
                : new DateTime(now.Year, now.Month, 1);

            var cursor = startDate;
            while (cursor <= cutoff)
            {
                if (!paidMonths.Contains((contract.TenantId, cursor.Year, cursor.Month)))
                {
                    // Determine which contract/extension is active this month
                    var targetDateStart = new DateTimeOffset(new DateTime(cursor.Year, cursor.Month, 1));
                    var targetDateEnd = targetDateStart.AddMonths(1).AddDays(-1);

                    var activeExtension = contractExtensions
                        .Where(e => e.RentalContractId == contract.Id
                            && e.StartDate <= targetDateEnd
                            && (!e.EndDate.HasValue || e.EndDate.Value >= targetDateStart))
                        .OrderByDescending(e => e.StartDate)
                        .FirstOrDefault();

                    decimal rent, expense;
                    ExpensePaymentType expenseType;

                    if (activeExtension != null)
                    {
                        rent = activeExtension.MonthlyRent;
                        expenseType = activeExtension.ExpensePaymentType;
                        expense = expenseType == ExpensePaymentType.Fixed
                            ? activeExtension.FixedExpenseAmount
                            : ComputeVariableExpense(propertyId, cursor.Year, cursor.Month);
                    }
                    else
                    {
                        rent = contract.MonthlyRent;
                        expenseType = contract.ExpensePaymentType;
                        expense = expenseType == ExpensePaymentType.Fixed
                            ? contract.FixedExpenseAmount
                            : ComputeVariableExpense(propertyId, cursor.Year, cursor.Month);
                    }

                    pendingList.Add(new ComputedPendingPayment
                    {
                        TenantId = contract.TenantId,
                        TenantName = tenantLookup.TryGetValue(contract.TenantId, out var tn) ? tn : $"(id={contract.TenantId})",
                        Year = cursor.Year,
                        Month = cursor.Month,
                        ExpectedRentAmount = rent,
                        ExpectedExpenseAmount = expense,
                        ContractId = contract.Id
                    });
                }
                cursor = cursor.AddMonths(1);
            }
        }
        foreach (var item in pendingList
            .GroupBy(p => (p.TenantId, p.Year, p.Month)).Select(g => g.First())
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ThenBy(p => p.TenantName))
        {
            PendingPayments.Add(item);
        }

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

        // Donut Chart: only Paid vs Partial (no Pending in DB anymore)
        double currentAngle = -90;
        double total = TotalPaymentsCount;
        if (total > 0)
        {
            PaidSweepAngle = (PaidPaymentsCount / total) * 360.0;
            PaidStartAngle = currentAngle;
            currentAngle += PaidSweepAngle;

            PartialSweepAngle = (PartialPaymentsCount / total) * 360.0;
            PartialStartAngle = currentAngle;
        }
        else
        {
            PaidSweepAngle = 0; PaidStartAngle = -90;
            PartialSweepAngle = 0; PartialStartAngle = -90;
        }
        // Keep Pending angles at 0 for compatibility with existing XAML bindings
        PendingSweepAngle = 0; PendingStartAngle = -90;

        OccupancySweepAngle = (TotalRooms > 0) ? ((double)OccupiedRoomsCount / TotalRooms) * 360.0 : 0.0;
        IncomeSweepAngle = (ExpectedIncome > 0) ? (double)(CollectedIncome / ExpectedIncome) * 360.0 : 0.0;

        // Calculate Monthly Bar Chart items (Income vs Expenses) for SelectedInterval
        var chartItems = new List<MonthlyBarChartItem>();
        decimal totalIncome = 0;
        decimal totalExpenses = 0;
        var expensesInPeriod = new List<ExpenseInvoice>();

        int monthsToFetch = SelectedInterval.Value;
        if (monthsToFetch == -1)
        {
            var earliestPayment = allPayments.Any() ? allPayments.Min(p => new DateTime(p.Year, p.Month, 1)) : now;
            var invoiceDates = _db.ExpenseInvoices
                .Where(exp => exp.PropertyId == propertyId)
                .Select(exp => new { exp.Year, exp.Month })
                .ToList();
            
            var earliestInvoice = invoiceDates.Any()
                ? invoiceDates.Min(exp => new DateTime(exp.Year, exp.Month, 1))
                : now;

            var earliestDate = earliestPayment < earliestInvoice ? earliestPayment : earliestInvoice;
            int diffMonths = ((DateTime.Today.Year - earliestDate.Year) * 12) + DateTime.Today.Month - earliestDate.Month + 1;
            monthsToFetch = Math.Max(1, diffMonths);
        }
        else if (monthsToFetch == -2)
        {
            monthsToFetch = DateTime.Today.Month; // Enero hasta el mes actual
        }

        for (int i = monthsToFetch - 1; i >= 0; i--)
        {
            var targetMonthDate = now.AddMonths(-i);
            var yearVal = targetMonthDate.Year;
            var monthVal = targetMonthDate.Month;

            var monthPayments = allPayments
                .Where(p => p.Year == yearVal && p.Month == monthVal)
                .ToList();
            
            var monthInvoices = _db.ExpenseInvoices
                .Where(exp => exp.PropertyId == propertyId && exp.Year == yearVal && exp.Month == monthVal)
                .ToList();

            var income = monthPayments.Sum(p => p.PaidAmount);
            var expenses = monthInvoices.Sum(exp => exp.Amount);

            totalIncome += income;
            totalExpenses += expenses;
            expensesInPeriod.AddRange(monthInvoices);

            chartItems.Add(new MonthlyBarChartItem
            {
                MonthName = targetMonthDate.ToString("MMM yy"),
                Income = income,
                Expenses = expenses
            });
        }

        TotalPeriodIncome = totalIncome;
        TotalPeriodExpenses = totalExpenses;
        PeriodProfit = totalIncome - totalExpenses;

        // --- 4. Gastos por Categoría (Donut) ---
        ExpenseCategoryChartItems.Clear();
        var categoryGroups = expensesInPeriod
            .GroupBy(e => e.CategoryId)
            .Select(g => new { 
                CategoryId = g.Key, 
                Total = g.Sum(e => e.Amount) 
            })
            .Where(g => g.Total > 0)
            .OrderByDescending(g => g.Total)
            .ToList();

        if (categoryGroups.Any())
        {
            var categoriesDb = _db.ExpenseCategories.ToDictionary(c => c.Id, c => c.Name);
            double donutAngle = -90; // Start at top
            var totalExpensesAmount = (double)categoryGroups.Sum(g => g.Total);
            int colorIdx = 0;
            string[] palette = { "#3F51B5", "#E91E63", "#009688", "#FF9800", "#9C27B0", "#4CAF50", "#FF5722", "#00BCD4" };

            foreach (var group in categoryGroups)
            {
                var name = categoriesDb.ContainsKey(group.CategoryId) ? categoriesDb[group.CategoryId] : "Desconocido";
                double sweep = ((double)group.Total / totalExpensesAmount) * 360.0;
                double pct = ((double)group.Total / totalExpensesAmount) * 100.0;
                
                ExpenseCategoryChartItems.Add(new CategoryDonutItem
                {
                    CategoryName = name,
                    Amount = group.Total,
                    Percentage = pct,
                    StartAngle = donutAngle,
                    SweepAngle = sweep,
                    Color = palette[colorIdx % palette.Length]
                });
                
                donutAngle += sweep;
                colorIdx++;
            }
        }
        
        OnPropertyChanged(nameof(HasExpenseCategoryChartItems));

        var maxVal = chartItems.Any() 
            ? chartItems.Max(x => Math.Max(x.Income, x.Expenses)) 
            : 0;

        if (maxVal == 0) maxVal = 100; // prevent division by zero

        foreach (var item in chartItems)
        {
            item.IncomeHeight = (double)(item.Income / maxVal) * 140.0; // max height 140px
            item.ExpenseHeight = (double)(item.Expenses / maxVal) * 140.0;
        }

        MonthlyBarChartItems.Clear();
        foreach (var item in chartItems)
        {
            MonthlyBarChartItems.Add(item);
        }

        OnPropertyChanged(nameof(OccupancyPercentageString));
        OnPropertyChanged(nameof(IncomeCollectedPercentageString));

        OnPropertyChanged(nameof(HasNoPendingPayments));
        OnPropertyChanged(nameof(HasNoAvailableRooms));
        OnPropertyChanged(nameof(HasNoMissingContracts));

        Console.WriteLine($"[DASH] Refresh complete. PendingPayments={PendingPayments.Count}, TimeSinceLastNotif={(DateTime.Now - _lastNotificationTime).TotalHours:F1}h");

        if (PendingPayments.Count > 0 && (DateTime.Now - _lastNotificationTime).TotalHours > 12)
        {
            _lastNotificationTime = DateTime.Now;
            var paymentsToNotify = PendingPayments.ToList();
            Console.WriteLine($"[DASH] Scheduling {paymentsToNotify.Count} notification(s) in 2s...");
            // Delay so macOS NSRunLoop is fully started before delivering notifications
            _ = Task.Delay(2000).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var payment in paymentsToNotify)
                    {
                        Console.WriteLine($"[DASH] Firing notification for {payment.TenantName}");
                        TenantManager.App.Services.NativeNotificationService.ShowNotification(
                            "Alquiler Pendiente",
                            $"{payment.TenantName} tiene pendiente el mes de {payment.MonthLabel}.");
                    }
                });
            });
        }
        else
        {
            Console.WriteLine("[DASH] Skipping notifications (no pending or cooldown active).");
        }
    }

    private static DateTime _lastNotificationTime = DateTime.MinValue;

    /// <summary>
    /// Computes the variable expense share for a given property month by splitting
    /// chargeable invoices across occupied rooms, matching the logic in MonthlyPaymentListViewModel.
    /// </summary>
    private decimal ComputeVariableExpense(int propertyId, int year, int month)
    {
        var targetDate = new DateTimeOffset(new DateTime(year, month, 1));

        var chargeableCategories = _db.ExpenseCategories.Where(c => c.IsChargeable).Select(c => c.Id).ToList();
        var invoicesTotal = _db.ExpenseInvoices
            .Where(i => i.Year == year && i.Month == month && i.PropertyId == propertyId && chargeableCategories.Contains(i.CategoryId))
            .ToList()
            .Sum(i => i.Amount);

        var occupiedRooms = _db.RentalContracts
            .Where(c => c.PropertyId == propertyId)
            .ToList()
            .Where(c => c.StartDate <= targetDate && (c.EndDate == null || c.EndDate >= targetDate))
            .Select(c => c.RoomId)
            .Distinct()
            .Count();

        return occupiedRooms > 0 ? invoicesTotal / occupiedRooms : 0m;
    }
}

public class CategoryDonutItem
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountString => $"{Amount:C0}";
    public double Percentage { get; set; }
    public string PercentageString => $"{Percentage:0.1}%";
    public double StartAngle { get; set; }
    public double SweepAngle { get; set; }
    public string Color { get; set; } = string.Empty;
}
