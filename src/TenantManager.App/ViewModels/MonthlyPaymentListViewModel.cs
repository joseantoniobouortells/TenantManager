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
    private int _editYear;
    private int _editMonth;
    private decimal _editExpectedAmount;
    private decimal _editPaidAmount;
    private PaymentStatus _editStatus;
    private DateTimeOffset? _editPaidDate;
    private string? _editNotes;

    private static readonly int CurrentYear = DateTime.Today.Year;

    public MonthlyPaymentListViewModel()
    {
        _db = new AppDbContext();
        Payments = new ObservableCollection<PaymentDisplayItem>();
        AvailableTenants = new ObservableCollection<Tenant>();
        AvailableStatuses = new ObservableCollection<PaymentStatus>(
            Enum.GetValues<PaymentStatus>());

        LoadPaymentsCommand = new RelayCommand(_ => LoadPayments());
        NewPaymentCommand = new RelayCommand(_ => StartNewPayment());
        EditPaymentCommand = new RelayCommand(_ => EditPayment());
        SavePaymentCommand = new RelayCommand(_ => SavePayment());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());

        LoadPayments();
    }

    public ObservableCollection<PaymentDisplayItem> Payments { get; }
    public ObservableCollection<Tenant> AvailableTenants { get; }
    public ObservableCollection<PaymentStatus> AvailableStatuses { get; }

    public RelayCommand LoadPaymentsCommand { get; }
    public RelayCommand NewPaymentCommand { get; }
    public RelayCommand EditPaymentCommand { get; }
    public RelayCommand SavePaymentCommand { get; }
    public RelayCommand CancelEditCommand { get; }

    public PaymentDisplayItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public Tenant? EditSelectedTenant
    {
        get => _editSelectedTenant;
        set => SetProperty(ref _editSelectedTenant, value);
    }

    public int EditYear
    {
        get => _editYear;
        set => SetProperty(ref _editYear, value);
    }

    public int EditMonth
    {
        get => _editMonth;
        set => SetProperty(ref _editMonth, value);
    }

    public decimal EditExpectedAmount
    {
        get => _editExpectedAmount;
        set => SetProperty(ref _editExpectedAmount, value);
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

    public void LoadPayments()
    {
        LoadAvailableTenants();

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        Payments.Clear();
        foreach (var payment in _db.MonthlyPayments
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
        foreach (var tenant in _db.Tenants.Where(t => t.IsActive).OrderBy(t => t.FullName))
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
        EditExpectedAmount = 0;
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
        EditExpectedAmount = _editingPayment.ExpectedAmount;
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
                TenantId = EditSelectedTenant.Id,
                Year = EditYear,
                Month = EditMonth,
                ExpectedAmount = EditExpectedAmount,
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
            _editingPayment.Year = EditYear;
            _editingPayment.Month = EditMonth;
            _editingPayment.ExpectedAmount = EditExpectedAmount;
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
            LoadPayments();
            CancelEdit();
            return;
        }

        LoadPayments();
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingPayment = null;
        EditSelectedTenant = null;
        EditYear = CurrentYear;
        EditMonth = DateTime.Today.Month;
        EditExpectedAmount = 0;
        EditPaidAmount = 0;
        EditStatus = PaymentStatus.Pending;
        EditPaidDate = null;
        EditNotes = null;
        IsEditing = false;
    }
}
