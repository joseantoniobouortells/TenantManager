using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class PaymentDisplayItem
{
    public MonthlyPayment Payment { get; init; } = null!;
    public string TenantName { get; set; } = string.Empty;
}

public class MonthlyPaymentListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private List<PaymentDisplayItem> _allPayments = new();
    private string _searchQuery = string.Empty;
    private string _sortColumn = "Year";
    private bool _sortAscending = false;
    private PaymentDisplayItem? _selectedItem;
    private MonthlyPayment? _editingPayment;
    private bool _isEditing;
    private bool _isRegisteringPending;
    private bool _isNewManualPayment;
    private Tenant? _editSelectedTenant;
    private decimal _editYear;
    private decimal _editMonth;
    private decimal _editExpectedRentAmount;
    private decimal _editExpectedExpenseAmount;
    private decimal _editPaidAmount;
    private PaymentStatus _editStatus;
    private DateTimeOffset? _editPaidDate;
    private string? _editNotes;
    private ComputedPendingPayment? _pendingBeingRegistered;

    private int _currentPropertyId;

    private static readonly int CurrentYear = DateTime.Today.Year;

    public MonthlyPaymentListViewModel() : this(new AppDbContext())
    {
    }

    public MonthlyPaymentListViewModel(AppDbContext db)
    {
        _db = db;
        Payments = new ObservableCollection<PaymentDisplayItem>();
        PendingPayments = new ObservableCollection<ComputedPendingPayment>();
        AvailableTenants = new ObservableCollection<Tenant>();
        AvailableStatuses = new ObservableCollection<PaymentStatus>(
            Enum.GetValues<PaymentStatus>());

        LoadPaymentsCommand = new RelayCommand(_ => LoadPayments(_currentPropertyId));
        SortCommand = new RelayCommand(param => Sort(param as string));
        SavePaymentCommand = new RelayCommand(_ => SavePayment());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        ClearPaidDateCommand = new RelayCommand(_ => EditPaidDate = null);
        RegisterPendingCommand = new RelayCommand(param => StartRegisterPending(param as ComputedPendingPayment));
        NewPaymentCommand = new RelayCommand(_ => StartNewPayment());

        DeletePaymentCommand = new RelayCommand(param => DeletePayment(param));
        ConfirmDeletePaymentCommand = new RelayCommand(_ => ConfirmDeletePayment());
        CancelDeletePaymentCommand = new RelayCommand(_ => CancelDeletePayment());
    }

    public ObservableCollection<PaymentDisplayItem> Payments { get; }
    public ObservableCollection<ComputedPendingPayment> PendingPayments { get; }
    public ObservableCollection<Tenant> AvailableTenants { get; }
    public ObservableCollection<PaymentStatus> AvailableStatuses { get; }

    public RelayCommand LoadPaymentsCommand { get; }
    public RelayCommand SortCommand { get; }
    public RelayCommand SavePaymentCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand ClearPaidDateCommand { get; }
    public RelayCommand RegisterPendingCommand { get; }
    public RelayCommand NewPaymentCommand { get; }
    public RelayCommand DeletePaymentCommand { get; }
    public RelayCommand ConfirmDeletePaymentCommand { get; }
    public RelayCommand CancelDeletePaymentCommand { get; }

    public PaymentDisplayItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                IsConfirmingDeletePayment = false;
                if (_selectedItem != null) EditPayment();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public bool IsRegisteringPending
    {
        get => _isRegisteringPending;
        set => SetProperty(ref _isRegisteringPending, value);
    }

    public bool IsNewManualPayment
    {
        get => _isNewManualPayment;
        set => SetProperty(ref _isNewManualPayment, value);
    }

    public bool HasPendingPayments => PendingPayments.Count > 0;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFiltersAndSort();
            }
        }
    }

    public string TenantSortIndicator => _sortColumn == "Tenant" ? (_sortAscending ? "▲" : "▼") : "";
    public string YearSortIndicator => _sortColumn == "Year" ? (_sortAscending ? "▲" : "▼") : "";
    public string MonthSortIndicator => _sortColumn == "Month" ? (_sortAscending ? "▲" : "▼") : "";
    public string TotalSortIndicator => _sortColumn == "Total" ? (_sortAscending ? "▲" : "▼") : "";
    public string StatusSortIndicator => _sortColumn == "Status" ? (_sortAscending ? "▲" : "▼") : "";

    public Tenant? EditSelectedTenant
    {
        get => _editSelectedTenant;
        set => SetProperty(ref _editSelectedTenant, value);
    }

    public decimal EditYear
    {
        get => _editYear;
        set => SetProperty(ref _editYear, value);
    }

    public decimal EditMonth
    {
        get => _editMonth;
        set => SetProperty(ref _editMonth, value);
    }

    public decimal EditExpectedRentAmount
    {
        get => _editExpectedRentAmount;
        set => SetProperty(ref _editExpectedRentAmount, value);
    }

    public decimal EditExpectedExpenseAmount
    {
        get => _editExpectedExpenseAmount;
        set => SetProperty(ref _editExpectedExpenseAmount, value);
    }

    public decimal EditPaidAmount
    {
        get => _editPaidAmount;
        set => SetProperty(ref _editPaidAmount, value);
    }

    public PaymentStatus EditStatus
    {
        get => _editStatus;
        set
        {
            if (SetProperty(ref _editStatus, value))
            {
                decimal balance = EditSelectedTenant != null ? GetTenantBalance(EditSelectedTenant.Id) : 0m;
                decimal expectedTotal = EditExpectedRentAmount + EditExpectedExpenseAmount;

                if (value == PaymentStatus.Paid)
                {
                    EditPaidAmount = expectedTotal - balance;
                    if (EditPaidDate == null)
                        EditPaidDate = DateTimeOffset.Now;
                }
                else if (value == PaymentStatus.Partial)
                {
                    if (EditPaidAmount == 0 || EditPaidAmount == expectedTotal)
                        EditPaidAmount = expectedTotal - balance;
                    if (EditPaidDate == null)
                        EditPaidDate = DateTimeOffset.Now;
                }
                OnPropertyChanged(nameof(IsPaidAmountEnabled));
            }
        }
    }

    public bool IsPaidAmountEnabled => EditStatus == PaymentStatus.Partial || EditStatus == PaymentStatus.Paid;

    public DateTimeOffset? EditPaidDate
    {
        get => _editPaidDate;
        set => SetProperty(ref _editPaidDate, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    // ─── Load ────────────────────────────────────────────────────────────────

    public void LoadPayments(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();

        // 1. Load only Paid/Partial records from DB into the history list
        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        _allPayments.Clear();
        foreach (var payment in _db.MonthlyPayments
            .Where(p => p.PropertyId == propertyId)
            .ToList())
        {
            _allPayments.Add(new PaymentDisplayItem
            {
                Payment = payment,
                TenantName = tenantLookup.TryGetValue(payment.TenantId, out var name) ? name : $"(id={payment.TenantId})"
            });
        }

        // 2. Compute pending payments automatically from active contracts
        ComputePendingPayments(tenantLookup);

        ApplyFiltersAndSort();
    }

    /// <summary>
    /// Computes pending payments dynamically: any contract month up to today
    /// that has no Paid or Partial record in the DB.
    /// </summary>
    private void ComputePendingPayments(Dictionary<int, string> tenantLookup)
    {
        PendingPayments.Clear();

        var today = DateTime.Today;
        var paidMonths = _allPayments
            .Select(p => (p.Payment.TenantId, p.Payment.Year, p.Payment.Month))
            .ToHashSet();

        var contracts = _db.RentalContracts
            .Where(c => c.PropertyId == _currentPropertyId)
            .ToList();

        var contractIds = contracts.Select(c => c.Id).ToList();
        var extensions = _db.RentalContractExtensions
            .Where(e => contractIds.Contains(e.RentalContractId))
            .ToList();

        var pending = new List<ComputedPendingPayment>();

        foreach (var contract in contracts)
        {
            var contractExtensions = extensions.Where(e => e.RentalContractId == contract.Id).ToList();

            // Determine the earliest start (contract start)
            var startDate = new DateTime(contract.StartDate.Year, contract.StartDate.Month, 1);

            // Determine the latest effective end date
            DateTime? effectiveEnd = null;
            if (contract.EndDate.HasValue)
                effectiveEnd = new DateTime(contract.EndDate.Value.Year, contract.EndDate.Value.Month, 1);

            foreach (var ext in contractExtensions)
            {
                if (!ext.EndDate.HasValue)
                {
                    effectiveEnd = null; // open-ended extension
                    break;
                }
                var extEnd = new DateTime(ext.EndDate.Value.Year, ext.EndDate.Value.Month, 1);
                if (effectiveEnd == null || extEnd > effectiveEnd)
                    effectiveEnd = extEnd;
            }

            // Iterate each month in the contract window up to today
            var cursor = startDate;
            var cutoff = effectiveEnd.HasValue
                ? new DateTime(Math.Min(effectiveEnd.Value.Ticks, new DateTime(today.Year, today.Month, 1).Ticks))
                : new DateTime(today.Year, today.Month, 1);

            while (cursor <= cutoff)
            {
                var year = cursor.Year;
                var month = cursor.Month;

                // Skip months that already have a Paid/Partial record
                if (!paidMonths.Contains((contract.TenantId, year, month)))
                {
                    var contractInfo = GetContractForTenantMonth(contract.TenantId, year, month, contracts, extensions);
                    if (contractInfo.HasValue)
                    {
                        var (rent, expense, _) = contractInfo.Value;
                        pending.Add(new ComputedPendingPayment
                        {
                            TenantId = contract.TenantId,
                            TenantName = tenantLookup.TryGetValue(contract.TenantId, out var name) ? name : $"(id={contract.TenantId})",
                            Year = year,
                            Month = month,
                            ExpectedRentAmount = rent,
                            ExpectedExpenseAmount = expense,
                            ContractId = contract.Id
                        });
                    }
                }

                cursor = cursor.AddMonths(1);
            }
        }

        // Sort: oldest unpaid months first
        foreach (var item in pending
            .GroupBy(p => (p.TenantId, p.Year, p.Month)) // dedup in case of multiple contracts same tenant
            .Select(g => g.First())
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ThenBy(p => p.TenantName))
        {
            PendingPayments.Add(item);
        }

        OnPropertyChanged(nameof(HasPendingPayments));
    }

    // ─── Sort & Filter ───────────────────────────────────────────────────────

    private void Sort(string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return;

        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        OnPropertyChanged(nameof(TenantSortIndicator));
        OnPropertyChanged(nameof(YearSortIndicator));
        OnPropertyChanged(nameof(MonthSortIndicator));
        OnPropertyChanged(nameof(TotalSortIndicator));
        OnPropertyChanged(nameof(StatusSortIndicator));

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = _allPayments.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(i =>
                (i.TenantName?.ToLowerInvariant().Contains(q) ?? false) ||
                i.Payment.Year.ToString().Contains(q) ||
                i.Payment.Month.ToString().Contains(q) ||
                i.Payment.Status.ToString().ToLowerInvariant().Contains(q));
        }

        filtered = _sortColumn switch
        {
            "Tenant" => _sortAscending ? filtered.OrderBy(i => i.TenantName) : filtered.OrderByDescending(i => i.TenantName),
            "Year" => _sortAscending ? filtered.OrderBy(i => i.Payment.Year).ThenBy(i => i.Payment.Month) : filtered.OrderByDescending(i => i.Payment.Year).ThenByDescending(i => i.Payment.Month),
            "Month" => _sortAscending ? filtered.OrderBy(i => i.Payment.Month) : filtered.OrderByDescending(i => i.Payment.Month),
            "Total" => _sortAscending ? filtered.OrderBy(i => i.Payment.ExpectedRentAmount + i.Payment.ExpectedExpenseAmount) : filtered.OrderByDescending(i => i.Payment.ExpectedRentAmount + i.Payment.ExpectedExpenseAmount),
            "Status" => _sortAscending ? filtered.OrderBy(i => i.Payment.Status.ToString()) : filtered.OrderByDescending(i => i.Payment.Status.ToString()),
            _ => filtered.OrderByDescending(i => i.Payment.Year).ThenByDescending(i => i.Payment.Month)
        };

        Payments.Clear();
        foreach (var inv in filtered)
            Payments.Add(inv);
    }

    // ─── Register pending payment ────────────────────────────────────────────

    private decimal GetTenantBalance(int tenantId)
    {
        var payments = _db.MonthlyPayments.Where(p => p.TenantId == tenantId && p.PropertyId == _currentPropertyId).ToList();
        var totalExpected = payments.Sum(p => p.ExpectedRentAmount + p.ExpectedExpenseAmount);
        var totalPaid = payments.Sum(p => p.PaidAmount);
        return totalPaid - totalExpected;
    }

    private void StartRegisterPending(ComputedPendingPayment? pending)
    {
        if (pending == null) return;

        _pendingBeingRegistered = pending;
        _editingPayment = null;

        LoadAvailableTenants(pending.TenantId);
        EditSelectedTenant = AvailableTenants.FirstOrDefault(t => t.Id == pending.TenantId);
        EditYear = pending.Year;
        EditMonth = pending.Month;
        EditExpectedRentAmount = pending.ExpectedRentAmount;
        EditExpectedExpenseAmount = pending.ExpectedExpenseAmount;
        
        decimal balance = GetTenantBalance(pending.TenantId);
        EditPaidAmount = (pending.ExpectedRentAmount + pending.ExpectedExpenseAmount) - balance;
        
        _editStatus = PaymentStatus.Paid;
        OnPropertyChanged(nameof(EditStatus));
        OnPropertyChanged(nameof(IsPaidAmountEnabled));
        
        EditPaidDate = DateTimeOffset.Now;
        EditNotes = null;
        IsRegisteringPending = true;
        IsNewManualPayment = false;
        IsEditing = true;
    }

    // ─── New manual payment ──────────────────────────────────────────────────

    private void StartNewPayment()
    {
        _pendingBeingRegistered = null;
        _editingPayment = null;

        LoadAvailableTenants(null);
        EditSelectedTenant = null;
        EditYear = DateTime.Today.Year;
        EditMonth = DateTime.Today.Month;
        EditExpectedRentAmount = 0;
        EditExpectedExpenseAmount = 0;
        
        _editStatus = PaymentStatus.Paid;
        OnPropertyChanged(nameof(EditStatus));
        OnPropertyChanged(nameof(IsPaidAmountEnabled));
        
        EditPaidAmount = 0;
        EditPaidDate = DateTimeOffset.Now;
        EditNotes = null;
        
        IsRegisteringPending = false;
        IsNewManualPayment = true;
        IsEditing = true;
    }

    // ─── Edit existing payment ───────────────────────────────────────────────

    private void EditPayment()
    {
        if (SelectedItem == null) return;

        _editingPayment = SelectedItem.Payment;
        _pendingBeingRegistered = null;
        LoadAvailableTenants(_editingPayment.TenantId);
        EditSelectedTenant = AvailableTenants.FirstOrDefault(t => t.Id == _editingPayment.TenantId);
        EditYear = _editingPayment.Year;
        EditMonth = _editingPayment.Month;
        EditExpectedRentAmount = _editingPayment.ExpectedRentAmount;
        EditExpectedExpenseAmount = _editingPayment.ExpectedExpenseAmount;
        EditPaidAmount = _editingPayment.PaidAmount;
        EditStatus = _editingPayment.Status;
        EditPaidDate = _editingPayment.PaidDate is DateTime pd ? new DateTimeOffset(pd) : null;
        EditNotes = _editingPayment.Notes;
        IsRegisteringPending = false;
        IsNewManualPayment = false;
        IsEditing = true;
    }

    // ─── Save ────────────────────────────────────────────────────────────────

    private void SavePayment()
    {
        if (EditSelectedTenant == null) return;

        if (_editingPayment == null)
        {
            // New record (registering a pending payment)
            var payment = new MonthlyPayment
            {
                PropertyId = _currentPropertyId,
                TenantId = EditSelectedTenant.Id,
                Year = (int)EditYear,
                Month = (int)EditMonth,
                ExpectedRentAmount = EditExpectedRentAmount,
                ExpectedExpenseAmount = EditExpectedExpenseAmount,
                PaidAmount = EditPaidAmount,
                Status = EditStatus,
                PaidDate = EditPaidDate?.DateTime,
                Notes = EditNotes?.Trim()
            };
            _db.MonthlyPayments.Add(payment);
        }
        else
        {
            // Update existing record
            _editingPayment.TenantId = EditSelectedTenant.Id;
            _editingPayment.Year = (int)EditYear;
            _editingPayment.Month = (int)EditMonth;
            _editingPayment.ExpectedRentAmount = EditExpectedRentAmount;
            _editingPayment.ExpectedExpenseAmount = EditExpectedExpenseAmount;
            _editingPayment.PaidAmount = EditPaidAmount;
            _editingPayment.Status = EditStatus;
            _editingPayment.PaidDate = EditPaidDate?.DateTime;
            _editingPayment.Notes = EditNotes?.Trim();
        }

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            LoadPayments(_currentPropertyId);
            CancelEdit();
            return;
        }

        LoadPayments(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingPayment = null;
        _pendingBeingRegistered = null;
        SelectedItem = null;
        EditSelectedTenant = null;
        EditYear = CurrentYear;
        EditMonth = DateTime.Today.Month;
        EditExpectedRentAmount = 0;
        EditExpectedExpenseAmount = 0;
        EditPaidAmount = 0;
        EditStatus = PaymentStatus.Paid;
        EditPaidDate = null;
        EditNotes = null;
        IsEditing = false;
        IsRegisteringPending = false;
        IsNewManualPayment = false;
    }

    // ─── Delete ──────────────────────────────────────────────────────────────

    public void DeletePayments(IEnumerable<PaymentDisplayItem> itemsToDelete)
    {
        if (itemsToDelete == null || !itemsToDelete.Any()) return;

        foreach (var item in itemsToDelete)
            _db.MonthlyPayments.Remove(item.Payment);

        _db.SaveChanges();
        LoadPayments(_currentPropertyId);
    }

    private bool _isConfirmingDeletePayment;
    public bool IsConfirmingDeletePayment
    {
        get => _isConfirmingDeletePayment;
        set => SetProperty(ref _isConfirmingDeletePayment, value);
    }

    private PaymentDisplayItem? _paymentToDelete;

    private void DeletePayment(object? param)
    {
        if (param is PaymentDisplayItem item)
        {
            _paymentToDelete = item;
            IsConfirmingDeletePayment = true;
        }
    }

    private void ConfirmDeletePayment()
    {
        if (_paymentToDelete != null)
        {
            _db.MonthlyPayments.Remove(_paymentToDelete.Payment);
            _db.SaveChanges();

            if (SelectedItem?.Payment?.Id == _paymentToDelete.Payment.Id)
                SelectedItem = null;

            _paymentToDelete = null;
            IsConfirmingDeletePayment = false;
            LoadPayments(_currentPropertyId);
        }
    }

    private void CancelDeletePayment()
    {
        _paymentToDelete = null;
        IsConfirmingDeletePayment = false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void LoadAvailableTenants(int? includeTenantId = null)
    {
        AvailableTenants.Clear();

        var targetYear = EditYear > 2000 ? (int)EditYear : DateTime.Today.Year;
        var targetMonth = EditMonth > 0 ? (int)EditMonth : DateTime.Today.Month;
        var targetDateStart = new DateTimeOffset(new DateTime(targetYear, targetMonth, 1));
        var targetDateEnd = targetDateStart.AddMonths(1).AddDays(-1);

        var propertyContracts = _db.RentalContracts.Where(c => c.PropertyId == _currentPropertyId).ToList();
        var contractIds = propertyContracts.Select(c => c.Id).ToList();
        var propertyExtensions = _db.RentalContractExtensions.Where(e => contractIds.Contains(e.RentalContractId)).ToList();

        var activeTenantIds = propertyContracts
            .Where(c => {
                var exts = propertyExtensions.Where(e => e.RentalContractId == c.Id).ToList();
                bool isContractActive = c.StartDate <= targetDateEnd && (c.EndDate == null || c.EndDate >= targetDateStart);
                bool isExtensionActive = exts.Any(e => e.StartDate <= targetDateEnd && (e.EndDate == null || e.EndDate >= targetDateStart));
                return isContractActive || isExtensionActive;
            })
            .Select(c => c.TenantId)
            .Distinct()
            .ToHashSet();

        foreach (var tenant in _db.Tenants
            .Where(t => t.PropertyId == _currentPropertyId)
            .ToList()
            .Where(t => activeTenantIds.Contains(t.Id) || t.Id == includeTenantId)
            .OrderBy(t => t.FullName))
        {
            AvailableTenants.Add(tenant);
        }
    }

    /// <summary>
    /// Returns the expected rent and expense for a tenant in a given month/year.
    /// Uses pre-loaded contracts and extensions to avoid multiple DB readers.
    /// </summary>
    private (decimal rent, decimal expense, ExpensePaymentType expenseType)? GetContractForTenantMonth(
        int tenantId, int year, int month,
        List<RentalContract> allContracts,
        List<RentalContractExtension> allExtensions)
    {
        var targetDateStart = new DateTimeOffset(new DateTime(year, month, 1));
        var targetDateEnd = targetDateStart.AddMonths(1).AddDays(-1);

        var contracts = allContracts.Where(c => c.TenantId == tenantId).ToList();
        var contractIds = contracts.Select(c => c.Id).ToList();
        var extensions = allExtensions.Where(e => contractIds.Contains(e.RentalContractId)).ToList();

        var activeContract = contracts
            .Where(c => c.StartDate <= targetDateEnd && (!c.EndDate.HasValue || c.EndDate.Value >= targetDateStart))
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault();

        if (activeContract == null)
        {
            var standaloneExtension = extensions
                .Where(e => e.StartDate <= targetDateEnd && (!e.EndDate.HasValue || e.EndDate.Value >= targetDateStart))
                .OrderByDescending(e => e.StartDate)
                .FirstOrDefault();
            if (standaloneExtension == null) return null;
            return ComputeExpense(standaloneExtension.MonthlyRent, standaloneExtension.ExpensePaymentType, standaloneExtension.FixedExpenseAmount, standaloneExtension.VariableExpensePercentage, year, month, standaloneExtension.StartDate);
        }

        var activeExtension = extensions
            .Where(e => e.RentalContractId == activeContract.Id && e.StartDate <= targetDateEnd && (!e.EndDate.HasValue || e.EndDate.Value >= targetDateStart))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefault();

        if (activeExtension != null)
            return ComputeExpense(activeExtension.MonthlyRent, activeExtension.ExpensePaymentType, activeExtension.FixedExpenseAmount, activeExtension.VariableExpensePercentage, year, month, activeExtension.StartDate);

        return ComputeExpense(activeContract.MonthlyRent, activeContract.ExpensePaymentType, activeContract.FixedExpenseAmount, activeContract.VariableExpensePercentage, year, month, activeContract.StartDate);
    }

    // Overload used from SavePayment for validation
    private (decimal rent, decimal expense, ExpensePaymentType expenseType)? GetContractForTenantMonth(int tenantId, int year, int month)
    {
        var contracts = _db.RentalContracts.Where(c => c.TenantId == tenantId && c.PropertyId == _currentPropertyId).ToList();
        var contractIds = contracts.Select(c => c.Id).ToList();
        var extensions = _db.RentalContractExtensions.Where(e => contractIds.Contains(e.RentalContractId)).ToList();
        return GetContractForTenantMonth(tenantId, year, month, contracts, extensions);
    }

    private (decimal rent, decimal expense, ExpensePaymentType expenseType) ComputeExpense(
        decimal rent, ExpensePaymentType expenseType, decimal fixedExpenseAmount, decimal variableExpensePercentage, int year, int month, DateTimeOffset startDate)
    {
        if (expenseType == ExpensePaymentType.Fixed)
            return (rent, fixedExpenseAmount, expenseType);

        var targetDate = new DateTime(year, month, 1).AddMonths(-1);
        var targetYear = targetDate.Year;
        var targetMonth = targetDate.Month;

        if (targetYear < startDate.Year || (targetYear == startDate.Year && targetMonth < startDate.Month))
        {
            return (rent, fixedExpenseAmount, expenseType); // Return without variable expenses
        }

        var chargeableCategories = _db.ExpenseCategories.Where(c => c.IsChargeable).Select(c => c.Id).ToList();
        var totalExpense = _db.ExpenseInvoices
            .Where(i => i.Year == targetYear && i.Month == targetMonth && i.PropertyId == _currentPropertyId && chargeableCategories.Contains(i.CategoryId))
            .Sum(i => i.Amount);

        var variableExpense = totalExpense * (variableExpensePercentage / 100m);
        return (rent, fixedExpenseAmount + variableExpense, expenseType);
    }
}
