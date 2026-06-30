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
    private PaymentDisplayItem? _selectedItem;
    private MonthlyPayment? _editingPayment;
    private bool _isEditing;
    private Tenant? _editSelectedTenant;
    private decimal _editYear;
    private decimal _editMonth;
    private decimal _editExpectedRentAmount;
    private decimal _editExpectedExpenseAmount;
    private decimal _editPaidAmount;
    private PaymentStatus _editStatus;
    private DateTimeOffset? _editPaidDate;
    private string? _editNotes;

    private bool _isBatchGenerating;
    private Tenant? _batchSelectedTenant;
    private decimal _batchStartYear;
    private decimal _batchStartMonth;
    private decimal _batchEndYear;
    private decimal _batchEndMonth;
    private PaymentStatus _batchDefaultStatus;
    private DateTimeOffset? _batchPaidDate;

    private int _currentPropertyId;

    private static readonly int CurrentYear = DateTime.Today.Year;

    public MonthlyPaymentListViewModel() : this(new AppDbContext())
    {
    }

    public MonthlyPaymentListViewModel(AppDbContext db)
    {
        _db = db;
        Payments = new ObservableCollection<PaymentDisplayItem>();
        AvailableTenants = new ObservableCollection<Tenant>();
        AvailableStatuses = new ObservableCollection<PaymentStatus>(
            Enum.GetValues<PaymentStatus>());

        LoadPaymentsCommand = new RelayCommand(_ => LoadPayments(_currentPropertyId));
        NewPaymentCommand = new RelayCommand(_ => StartNewPayment());
        EditPaymentCommand = new RelayCommand(_ => EditPayment());
        SavePaymentCommand = new RelayCommand(_ => SavePayment());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        ClearPaidDateCommand = new RelayCommand(_ => EditPaidDate = null);

        StartBatchCommand = new RelayCommand(_ => StartBatch());
        GenerateBatchCommand = new RelayCommand(_ => GenerateBatch());
        CancelBatchCommand = new RelayCommand(_ => CancelBatch());
        ClearBatchPaidDateCommand = new RelayCommand(_ => BatchPaidDate = null);

        DeletePaymentCommand = new RelayCommand(param => DeletePayment(param));
        ConfirmDeletePaymentCommand = new RelayCommand(_ => ConfirmDeletePayment());
        CancelDeletePaymentCommand = new RelayCommand(_ => CancelDeletePayment());
    }

    public ObservableCollection<PaymentDisplayItem> Payments { get; }
    public ObservableCollection<Tenant> AvailableTenants { get; }
    public ObservableCollection<PaymentStatus> AvailableStatuses { get; }

    public RelayCommand LoadPaymentsCommand { get; }
    public RelayCommand NewPaymentCommand { get; }
    public RelayCommand EditPaymentCommand { get; }
    public RelayCommand SavePaymentCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand ClearPaidDateCommand { get; }

    public RelayCommand StartBatchCommand { get; }
    public RelayCommand GenerateBatchCommand { get; }
    public RelayCommand CancelBatchCommand { get; }
    public RelayCommand ClearBatchPaidDateCommand { get; }
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

    public bool IsBatchGenerating
    {
        get => _isBatchGenerating;
        set => SetProperty(ref _isBatchGenerating, value);
    }

    public Tenant? EditSelectedTenant
    {
        get => _editSelectedTenant;
        set
        {
            if (SetProperty(ref _editSelectedTenant, value))
            {
                if (IsEditing && _editingPayment == null)
                    AutoFillAmounts();
            }
        }
    }

    public decimal EditYear
    {
        get => _editYear;
        set 
        {
            if (SetProperty(ref _editYear, value))
            {
                if (IsEditing)
                {
                    // For new payments: refresh tenant list without forcing current tenant
                    // For editing existing: keep original tenant visible (option A)
                    LoadAvailableTenants(_editingPayment?.TenantId);
                    if (_editingPayment == null)
                        AutoFillAmounts();
                }
            }
        }
    }

    public decimal EditMonth
    {
        get => _editMonth;
        set 
        {
            if (SetProperty(ref _editMonth, value))
            {
                if (IsEditing)
                {
                    LoadAvailableTenants(_editingPayment?.TenantId);
                    if (_editingPayment == null)
                        AutoFillAmounts();
                }
            }
        }
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
                if (value == PaymentStatus.Paid)
                {
                    EditPaidAmount = EditExpectedRentAmount + EditExpectedExpenseAmount;
                    if (EditPaidDate == null)
                    {
                        EditPaidDate = DateTimeOffset.Now;
                    }
                }
                else if (value == PaymentStatus.Partial)
                {
                    // Keep current or default to expected if it was zero
                    if (EditPaidAmount == 0)
                    {
                        EditPaidAmount = EditExpectedRentAmount + EditExpectedExpenseAmount;
                    }
                    if (EditPaidDate == null)
                    {
                        EditPaidDate = DateTimeOffset.Now;
                    }
                }
                else
                {
                    EditPaidAmount = 0;
                    if (value == PaymentStatus.Pending)
                    {
                        EditPaidDate = null;
                    }
                    else if (EditPaidDate == null)
                    {
                        EditPaidDate = DateTimeOffset.Now;
                    }
                }
                OnPropertyChanged(nameof(IsPaidAmountEnabled));
            }
        }
    }

    public bool IsPaidAmountEnabled => EditStatus == PaymentStatus.Partial;

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

    public Tenant? BatchSelectedTenant
    {
        get => _batchSelectedTenant;
        set => SetProperty(ref _batchSelectedTenant, value);
    }

    public decimal BatchStartYear
    {
        get => _batchStartYear;
        set => SetProperty(ref _batchStartYear, value);
    }

    public decimal BatchStartMonth
    {
        get => _batchStartMonth;
        set => SetProperty(ref _batchStartMonth, value);
    }

    public decimal BatchEndYear
    {
        get => _batchEndYear;
        set => SetProperty(ref _batchEndYear, value);
    }

    public decimal BatchEndMonth
    {
        get => _batchEndMonth;
        set => SetProperty(ref _batchEndMonth, value);
    }

    public PaymentStatus BatchDefaultStatus
    {
        get => _batchDefaultStatus;
        set => SetProperty(ref _batchDefaultStatus, value);
    }

    public DateTimeOffset? BatchPaidDate
    {
        get => _batchPaidDate;
        set => SetProperty(ref _batchPaidDate, value);
    }

    public void LoadPayments(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();
        LoadAvailableTenants();

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        Payments.Clear();
        foreach (var payment in _db.MonthlyPayments
            .Where(p => p.PropertyId == propertyId)
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ThenBy(p => p.TenantId))
        {
            Payments.Add(new PaymentDisplayItem
            {
                Payment = payment,
                TenantName = tenantLookup.TryGetValue(payment.TenantId, out var name) ? name : $"(id={payment.TenantId})"
            });
        }
    }

    private void LoadAvailableTenants(int? includeTenantId = null)
    {
        AvailableTenants.Clear();
        
        // Define the target date based on what the user is currently editing (or default to current month/year)
        var targetYear = EditYear > 2000 ? (int)EditYear : DateTime.Today.Year;
        var targetMonth = EditMonth > 0 ? (int)EditMonth : DateTime.Today.Month;
        var targetDateStart = new DateTimeOffset(new DateTime(targetYear, targetMonth, 1));
        var targetDateEnd = targetDateStart.AddMonths(1).AddDays(-1); // Last day of the target month
        
        // Load contracts and extensions into memory first to avoid SQLite multiple active data readers exception
        var propertyContracts = _db.RentalContracts.Where(c => c.PropertyId == _currentPropertyId).ToList();
        var contractIds = propertyContracts.Select(c => c.Id).ToList();
        var propertyExtensions = _db.RentalContractExtensions.Where(e => contractIds.Contains(e.RentalContractId)).ToList();

        var activeTenantIds = propertyContracts
            .Where(c => {
                var extensions = propertyExtensions.Where(e => e.RentalContractId == c.Id).ToList();
                
                // A contract/extension is valid for the target month if it starts before the end of the month
                // AND (it has no end date OR it ends on or after the first day of the target month).
                bool isContractActive = c.StartDate <= targetDateEnd && (c.EndDate == null || c.EndDate >= targetDateStart);
                bool isExtensionActive = extensions.Any(e => e.StartDate <= targetDateEnd && (e.EndDate == null || e.EndDate >= targetDateStart));
                
                return isContractActive || isExtensionActive;
            })
            .Select(c => c.TenantId)
            .Distinct()
            .ToHashSet();

        foreach (var tenant in _db.Tenants
            .Where(t => t.IsActive && t.PropertyId == _currentPropertyId)
            .ToList()
            .Where(t => activeTenantIds.Contains(t.Id) || t.Id == includeTenantId)
            .OrderBy(t => t.FullName))
        {
            AvailableTenants.Add(tenant);
        }
    }

    private void StartNewPayment()
    {
        _editingPayment = null;
        // Set year/month BEFORE loading tenants so the filter uses correct values
        _editYear = CurrentYear;
        _editMonth = DateTime.Today.Month;
        OnPropertyChanged(nameof(EditYear));
        OnPropertyChanged(nameof(EditMonth));
        LoadAvailableTenants();
        EditSelectedTenant = AvailableTenants.FirstOrDefault();
        // Amounts auto-filled by EditSelectedTenant setter via AutoFillAmounts()
        EditStatus = PaymentStatus.Pending;
        EditPaidDate = null;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditPayment()
    {
        if (SelectedItem == null)
            return;

        _editingPayment = SelectedItem.Payment;
        LoadAvailableTenants(_editingPayment.TenantId);
        EditSelectedTenant = AvailableTenants.FirstOrDefault(t => t.Id == _editingPayment.TenantId);
        EditYear = _editingPayment.Year;
        EditMonth = _editingPayment.Month;
        EditExpectedRentAmount = _editingPayment.ExpectedRentAmount;
        EditExpectedExpenseAmount = _editingPayment.ExpectedExpenseAmount;
        EditPaidAmount = _editingPayment.PaidAmount;
        EditStatus = _editingPayment.Status;
        EditPaidDate = _editingPayment.PaidDate is DateTime pd
            ? new DateTimeOffset(pd)
            : null;
        EditNotes = _editingPayment.Notes;
        IsEditing = true;
    }

    private void SavePayment()
    {
        if (EditSelectedTenant == null)
            return;

        // Validate: tenant must have an active contract for the selected month/year
        var contractInfo = GetContractForTenantMonth(EditSelectedTenant.Id, (int)EditYear, (int)EditMonth);
        if (contractInfo == null)
        {
            Console.WriteLine($"[SavePayment] Blocked: {EditSelectedTenant.FullName} has no active contract for {EditYear}/{EditMonth}");
            return;
        }

        if (_editingPayment == null)
        {
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
        EditSelectedTenant = null;
        EditYear = CurrentYear;
        EditMonth = DateTime.Today.Month;
        EditExpectedRentAmount = 0;
        EditExpectedExpenseAmount = 0;
        EditPaidAmount = 0;
        EditStatus = PaymentStatus.Pending;
        EditPaidDate = null;
        EditNotes = null;
        IsEditing = false;
    }

    private void StartBatch()
    {
        BatchSelectedTenant = AvailableTenants.FirstOrDefault();
        BatchStartYear = CurrentYear;
        BatchStartMonth = 1;
        BatchEndYear = CurrentYear;
        BatchEndMonth = 12;
        BatchDefaultStatus = PaymentStatus.Paid;
        BatchPaidDate = DateTimeOffset.Now;
        IsBatchGenerating = true;
    }

    private void CancelBatch()
    {
        IsBatchGenerating = false;
        BatchSelectedTenant = null;
    }

    private void GenerateBatch()
    {
        Console.WriteLine($"[DEBUG Batch] Tenant: {BatchSelectedTenant?.FullName ?? "null"}, Start: {BatchStartYear}/{BatchStartMonth}, End: {BatchEndYear}/{BatchEndMonth}");
        if (BatchSelectedTenant == null)
        {
            Console.WriteLine("[DEBUG Batch] Selected tenant is null");
            return;
        }
        if (BatchStartYear > BatchEndYear || (BatchStartYear == BatchEndYear && BatchStartMonth > BatchEndMonth))
        {
            Console.WriteLine("[DEBUG Batch] Date range invalid");
            return;
        }

        var tenantId = BatchSelectedTenant.Id;

        var existingPayments = _db.MonthlyPayments
            .Where(p => p.TenantId == tenantId && p.Year >= (int)BatchStartYear && p.Year <= (int)BatchEndYear)
            .ToList();

        var currentDate = new DateTime((int)BatchStartYear, (int)BatchStartMonth, 1);
        var endDate = new DateTime((int)BatchEndYear, (int)BatchEndMonth, 1);

        while (currentDate <= endDate)
        {
            var year = currentDate.Year;
            var month = currentDate.Month;

            // Check if exists
            if (!existingPayments.Any(p => p.Year == year && p.Month == month))
            {
                // Only create payment if tenant has an active contract for this month
                var contractInfo = GetContractForTenantMonth(tenantId, year, month);
                if (contractInfo != null)
                {
                    var (expectedRent, expectedExpense, _) = contractInfo.Value;

                    var payment = new MonthlyPayment
                    {
                        PropertyId = _currentPropertyId,
                        TenantId = tenantId,
                        Year = year,
                        Month = month,
                        ExpectedRentAmount = expectedRent,
                        ExpectedExpenseAmount = expectedExpense,
                        PaidAmount = BatchDefaultStatus == PaymentStatus.Paid ? expectedRent + expectedExpense : 0,
                        Status = BatchDefaultStatus,
                        PaidDate = BatchDefaultStatus == PaymentStatus.Paid ? BatchPaidDate?.DateTime : null
                    };

                    _db.MonthlyPayments.Add(payment);
                }
            }

            currentDate = currentDate.AddMonths(1);
        }

        _db.SaveChanges();
        LoadPayments(_currentPropertyId);
        CancelBatch();
    }

    public void DeletePayments(IEnumerable<PaymentDisplayItem> itemsToDelete)
    {
        if (itemsToDelete == null || !itemsToDelete.Any())
            return;

        foreach (var item in itemsToDelete)
        {
            _db.MonthlyPayments.Remove(item.Payment);
        }
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
            {
                SelectedItem = null;
            }
            
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

    /// <summary>
    /// Returns the expected rent and expense for a tenant in a given month/year,
    /// based on the active contract or extension. Returns null if no contract covers that month.
    /// </summary>
    private (decimal rent, decimal expense, ExpensePaymentType expenseType)? GetContractForTenantMonth(int tenantId, int year, int month)
    {
        var targetDateStart = new DateTimeOffset(new DateTime(year, month, 1));
        var targetDateEnd = targetDateStart.AddMonths(1).AddDays(-1);

        // Load into memory to avoid SQLite multiple active readers
        var contracts = _db.RentalContracts
            .Where(c => c.TenantId == tenantId && c.PropertyId == _currentPropertyId)
            .ToList();
        var contractIds = contracts.Select(c => c.Id).ToList();
        var extensions = _db.RentalContractExtensions
            .Where(e => contractIds.Contains(e.RentalContractId))
            .ToList();

        // Find a contract that overlaps with the target month
        var activeContract = contracts
            .Where(c => c.StartDate <= targetDateEnd && (!c.EndDate.HasValue || c.EndDate.Value >= targetDateStart))
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault();

        if (activeContract == null)
        {
            // Check if any extension covers this month even if the base contract doesn't
            var standaloneExtension = extensions
                .Where(e => e.StartDate <= targetDateEnd && (!e.EndDate.HasValue || e.EndDate.Value >= targetDateStart))
                .OrderByDescending(e => e.StartDate)
                .FirstOrDefault();
            if (standaloneExtension == null)
                return null;

            // Extension found — use its values
            return ComputeExpense(standaloneExtension.MonthlyRent, standaloneExtension.ExpensePaymentType, standaloneExtension.FixedExpenseAmount, year, month);
        }

        // Check for an extension on this contract that covers the target month
        var activeExtension = extensions
            .Where(e => e.RentalContractId == activeContract.Id && e.StartDate <= targetDateEnd && (!e.EndDate.HasValue || e.EndDate.Value >= targetDateStart))
            .OrderByDescending(e => e.StartDate)
            .FirstOrDefault();

        if (activeExtension != null)
            return ComputeExpense(activeExtension.MonthlyRent, activeExtension.ExpensePaymentType, activeExtension.FixedExpenseAmount, year, month);

        return ComputeExpense(activeContract.MonthlyRent, activeContract.ExpensePaymentType, activeContract.FixedExpenseAmount, year, month);
    }

    private (decimal rent, decimal expense, ExpensePaymentType expenseType) ComputeExpense(
        decimal rent, ExpensePaymentType expenseType, decimal fixedExpenseAmount, int year, int month)
    {
        if (expenseType == ExpensePaymentType.Fixed)
            return (rent, fixedExpenseAmount, expenseType);

        // Variable: split invoices among occupied rooms
        var targetDate = new DateTimeOffset(new DateTime(year, month, 1));
        var invoicesTotal = _db.ExpenseInvoices
            .Where(i => i.Year == year && i.Month == month && i.PropertyId == _currentPropertyId && i.IsChargeableToTenant)
            .ToList()
            .Sum(i => i.Amount);

        var occupiedRooms = _db.RentalContracts
            .Where(c => c.PropertyId == _currentPropertyId)
            .ToList()
            .Where(c => c.StartDate <= targetDate && (c.EndDate == null || c.EndDate >= targetDate))
            .Select(c => c.RoomId)
            .Distinct()
            .Count();

        var expense = occupiedRooms > 0 ? invoicesTotal / occupiedRooms : 0m;
        return (rent, expense, expenseType);
    }

    /// <summary>
    /// Auto-fills ExpectedRentAmount and ExpectedExpenseAmount from the active contract
    /// when creating a new payment. Also updates PaidAmount if status is Paid.
    /// </summary>
    private void AutoFillAmounts()
    {
        if (EditSelectedTenant == null || EditYear < 2000 || EditMonth < 1 || EditMonth > 12)
        {
            EditExpectedRentAmount = 0;
            EditExpectedExpenseAmount = 0;
            EditPaidAmount = 0;
            return;
        }

        var contractInfo = GetContractForTenantMonth(EditSelectedTenant.Id, (int)EditYear, (int)EditMonth);
        if (contractInfo == null)
        {
            EditExpectedRentAmount = 0;
            EditExpectedExpenseAmount = 0;
            EditPaidAmount = 0;
            return;
        }

        var (rent, expense, _) = contractInfo.Value;
        EditExpectedRentAmount = rent;
        EditExpectedExpenseAmount = expense;

        if (EditStatus == PaymentStatus.Paid)
            EditPaidAmount = rent + expense;
        else if (EditStatus == PaymentStatus.Pending)
            EditPaidAmount = 0;
    }
}
