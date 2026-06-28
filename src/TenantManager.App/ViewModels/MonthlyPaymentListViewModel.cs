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

    public PaymentDisplayItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
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
        set => SetProperty(ref _editStatus, value);
    }

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

    private void LoadAvailableTenants()
    {
        AvailableTenants.Clear();
        foreach (var tenant in _db.Tenants.Where(t => t.IsActive && t.PropertyId == _currentPropertyId).OrderBy(t => t.FullName))
        {
            AvailableTenants.Add(tenant);
        }
    }

    private void StartNewPayment()
    {
        _editingPayment = null;
        EditSelectedTenant = AvailableTenants.FirstOrDefault();
        EditYear = CurrentYear;
        EditMonth = DateTime.Today.Month;
        EditExpectedRentAmount = 0;
        EditExpectedExpenseAmount = 0;
        EditPaidAmount = 0;
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

        var contracts = _db.RentalContracts.Where(c => c.TenantId == tenantId).ToList();
        var extensions = _db.RentalContractExtensions.Where(e => contracts.Select(c => c.Id).Contains(e.RentalContractId)).ToList();

        var currentDate = new DateTime((int)BatchStartYear, (int)BatchStartMonth, 1);
        var endDate = new DateTime((int)BatchEndYear, (int)BatchEndMonth, 1);

        while (currentDate <= endDate)
        {
            var year = currentDate.Year;
            var month = currentDate.Month;

            // Check if exists
            if (!existingPayments.Any(p => p.Year == year && p.Month == month))
            {
                var expectedRent = 0m;
                var expenseType = ExpensePaymentType.Variable;
                var fixedExpenseAmount = 0m;

                var targetDate = new DateTimeOffset(currentDate);
                
                // Fallback to the latest contract if no exact match is found for the month, but it's better to find the active one
                var activeContract = contracts
                    .Where(c => c.StartDate <= targetDate && (!c.EndDate.HasValue || c.EndDate.Value >= targetDate))
                    .OrderByDescending(c => c.StartDate)
                    .FirstOrDefault() ?? contracts.OrderByDescending(c => c.StartDate).FirstOrDefault();

                if (activeContract != null)
                {
                    var activeExtension = extensions
                        .Where(e => e.RentalContractId == activeContract.Id && e.StartDate <= targetDate && (!e.EndDate.HasValue || e.EndDate.Value >= targetDate))
                        .OrderByDescending(e => e.StartDate)
                        .FirstOrDefault();

                    if (activeExtension != null)
                    {
                        expectedRent = activeExtension.MonthlyRent;
                        expenseType = activeExtension.ExpensePaymentType;
                        fixedExpenseAmount = activeExtension.FixedExpenseAmount;
                    }
                    else
                    {
                        expectedRent = activeContract.MonthlyRent;
                        expenseType = activeContract.ExpensePaymentType;
                        fixedExpenseAmount = activeContract.FixedExpenseAmount;
                    }
                }

                var expectedExpense = 0m;
                if (expenseType == ExpensePaymentType.Fixed)
                {
                    expectedExpense = fixedExpenseAmount;
                }
                else
                {
                    var invoicesTotal = _db.ExpenseInvoices
                        .Where(i => i.Year == year && i.Month == month && i.PropertyId == _currentPropertyId)
                        .Sum(i => i.Amount);

                    var occupiedRooms = _db.RentalContracts
                        .Where(c => c.PropertyId == _currentPropertyId)
                        .AsEnumerable()
                        .Where(c => c.StartDate <= targetDate && (c.EndDate == null || c.EndDate >= targetDate))
                        .Select(c => c.RoomId)
                        .Distinct()
                        .Count();

                    if (occupiedRooms > 0)
                    {
                        expectedExpense = invoicesTotal / occupiedRooms;
                    }
                }

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
}
