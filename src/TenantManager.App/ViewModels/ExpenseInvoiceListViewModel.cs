using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class ExpenseInvoiceListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private System.Collections.Generic.List<ExpenseInvoice> _allInvoices = new();
    private string _searchQuery = string.Empty;
    private string _sortColumn = "Year";
    private bool _sortAscending = false;
    private ExpenseInvoice? _editingInvoice;
    private ExpenseInvoice? _selectedInvoice;
    private bool _isEditing;

    private string _editExpenseType = string.Empty;
    private bool _editIsChargeableToTenant = true;
    private decimal _editYear;
    private decimal _editMonth;
    private decimal _editAmount;
    private string? _editNotes;
    private string _editFilePath = string.Empty;
    private byte[]? _editFileContent;
    private int _currentPropertyId;

    public ExpenseInvoiceListViewModel()
    {
        _db = new AppDbContext();
        Invoices = new ObservableCollection<ExpenseInvoice>();

        LoadInvoicesCommand = new RelayCommand(_ => LoadInvoices(_currentPropertyId));
        SortCommand = new RelayCommand(param => Sort(param as string));
        NewInvoiceCommand = new RelayCommand(_ => StartNewInvoice());
        EditInvoiceCommand = new RelayCommand(_ => EditInvoice());
        SaveInvoiceCommand = new RelayCommand(_ => SaveInvoice());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        DeleteInvoiceCommand = new RelayCommand(param => DeleteInvoice(param));
        OpenFileCommand = new RelayCommand(param => OpenFile(param));
        ClearFileCommand = new RelayCommand(_ => { EditFilePath = string.Empty; EditFileContent = null; });
        ConfirmDeleteInvoiceCommand = new RelayCommand(_ => ConfirmDeleteInvoice());
        CancelDeleteInvoiceCommand = new RelayCommand(_ => CancelDeleteInvoice());
    }

    public ObservableCollection<ExpenseInvoice> Invoices { get; }

    public RelayCommand LoadInvoicesCommand { get; }
    public RelayCommand SortCommand { get; }
    public RelayCommand NewInvoiceCommand { get; }
    public RelayCommand EditInvoiceCommand { get; }
    public RelayCommand SaveInvoiceCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand DeleteInvoiceCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand ClearFileCommand { get; }
    public RelayCommand ConfirmDeleteInvoiceCommand { get; }
    public RelayCommand CancelDeleteInvoiceCommand { get; }

    public ExpenseInvoice? SelectedInvoice
    {
        get => _selectedInvoice;
        set
        {
            if (SetProperty(ref _selectedInvoice, value))
            {
                IsConfirmingDeleteInvoice = false;
                if (_selectedInvoice != null) EditInvoice();
                OnPropertyChanged(nameof(HasSelectedInvoice));
                OnPropertyChanged(nameof(SelectedInvoiceHasFile));
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

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

    public string TypeSortIndicator => _sortColumn == "ExpenseType" ? (_sortAscending ? "▲" : "▼") : "";
    public string YearSortIndicator => _sortColumn == "Year" ? (_sortAscending ? "▲" : "▼") : "";
    public string MonthSortIndicator => _sortColumn == "Month" ? (_sortAscending ? "▲" : "▼") : "";
    public string AmountSortIndicator => _sortColumn == "Amount" ? (_sortAscending ? "▲" : "▼") : "";
    public string ChargeableSortIndicator => _sortColumn == "IsChargeableToTenant" ? (_sortAscending ? "▲" : "▼") : "";

    public bool EditIsChargeableToTenant
    {
        get => _editIsChargeableToTenant;
        set => SetProperty(ref _editIsChargeableToTenant, value);
    }

    public string EditExpenseType
    {
        get => _editExpenseType;
        set => SetProperty(ref _editExpenseType, value);
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

    public decimal EditAmount
    {
        get => _editAmount;
        set => SetProperty(ref _editAmount, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public string EditFilePath
    {
        get => _editFilePath;
        set => SetProperty(ref _editFilePath, value);
    }

    public byte[]? EditFileContent
    {
        get => _editFileContent;
        set => SetProperty(ref _editFileContent, value);
    }

    public bool HasSelectedInvoice => SelectedInvoice != null;

    public bool SelectedInvoiceHasFile => SelectedInvoice != null && (SelectedInvoice.FileContent != null || (!string.IsNullOrWhiteSpace(SelectedInvoice.FilePath) && File.Exists(SelectedInvoice.FilePath)));

    public void LoadInvoices(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();
        _allInvoices = _db.ExpenseInvoices.Where(i => i.PropertyId == propertyId).ToList();
        ApplyFiltersAndSort();
    }

    private void Sort(string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return;

        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        OnPropertyChanged(nameof(TypeSortIndicator));
        OnPropertyChanged(nameof(YearSortIndicator));
        OnPropertyChanged(nameof(MonthSortIndicator));
        OnPropertyChanged(nameof(AmountSortIndicator));
        OnPropertyChanged(nameof(ChargeableSortIndicator));

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = _allInvoices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(i => 
                (i.ExpenseType?.ToLowerInvariant().Contains(q) ?? false) ||
                i.Year.ToString().Contains(q) ||
                i.Month.ToString().Contains(q));
        }

        filtered = _sortColumn switch
        {
            "ExpenseType" => _sortAscending ? filtered.OrderBy(i => i.ExpenseType) : filtered.OrderByDescending(i => i.ExpenseType),
            "Year" => _sortAscending ? filtered.OrderBy(i => i.Year).ThenBy(i => i.Month) : filtered.OrderByDescending(i => i.Year).ThenByDescending(i => i.Month),
            "Month" => _sortAscending ? filtered.OrderBy(i => i.Month) : filtered.OrderByDescending(i => i.Month),
            "Amount" => _sortAscending ? filtered.OrderBy(i => i.Amount) : filtered.OrderByDescending(i => i.Amount),
            "IsChargeableToTenant" => _sortAscending ? filtered.OrderBy(i => i.IsChargeableToTenant) : filtered.OrderByDescending(i => i.IsChargeableToTenant),
            _ => filtered.OrderByDescending(i => i.Year).ThenByDescending(i => i.Month)
        };

        Invoices.Clear();
        foreach (var inv in filtered)
        {
            Invoices.Add(inv);
        }
    }

    private void StartNewInvoice()
    {
        _editingInvoice = null;
        EditExpenseType = string.Empty;
        EditIsChargeableToTenant = true;
        EditYear = DateTime.Today.Year;
        EditMonth = DateTime.Today.Month;
        EditAmount = 0;
        EditNotes = null;
        EditFilePath = string.Empty;
        EditFileContent = null;
        IsEditing = true;
    }

    private void EditInvoice()
    {
        if (SelectedInvoice == null) return;

        _editingInvoice = SelectedInvoice;
        EditExpenseType = _editingInvoice.ExpenseType;
        EditIsChargeableToTenant = _editingInvoice.IsChargeableToTenant;
        EditYear = _editingInvoice.Year;
        EditMonth = _editingInvoice.Month;
        EditAmount = _editingInvoice.Amount;
        EditNotes = _editingInvoice.Notes;
        EditFilePath = _editingInvoice.FilePath ?? string.Empty;
        EditFileContent = _editingInvoice.FileContent;
        IsEditing = true;
    }

    private void SaveInvoice()
    {
        if (string.IsNullOrWhiteSpace(EditExpenseType) || EditAmount < 0)
            return;

        if (_editingInvoice == null)
        {
            var invoice = new ExpenseInvoice
            {
                PropertyId = _currentPropertyId,
                ExpenseType = EditExpenseType.Trim(),
                Year = (int)EditYear,
                Month = (int)EditMonth,
                Amount = EditAmount,
                Notes = EditNotes?.Trim(),
                FilePath = EditFilePath?.Trim(),
                FileContent = EditFileContent,
                IsChargeableToTenant = EditIsChargeableToTenant
            };
            _db.ExpenseInvoices.Add(invoice);
        }
        else
        {
            _editingInvoice.ExpenseType = EditExpenseType.Trim();
            _editingInvoice.Year = (int)EditYear;
            _editingInvoice.Month = (int)EditMonth;
            _editingInvoice.Amount = EditAmount;
            _editingInvoice.Notes = EditNotes?.Trim();
            _editingInvoice.FilePath = EditFilePath?.Trim();
            _editingInvoice.FileContent = EditFileContent;
            _editingInvoice.IsChargeableToTenant = EditIsChargeableToTenant;
        }

        _db.SaveChanges();
        LoadInvoices(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingInvoice = null;
        EditExpenseType = string.Empty;
        EditIsChargeableToTenant = true;
        EditYear = DateTime.Today.Year;
        EditMonth = DateTime.Today.Month;
        EditAmount = 0;
        EditNotes = null;
        EditFilePath = string.Empty;
        EditFileContent = null;
        IsEditing = false;
    }

    private bool _isConfirmingDeleteInvoice;
    public bool IsConfirmingDeleteInvoice
    {
        get => _isConfirmingDeleteInvoice;
        set => SetProperty(ref _isConfirmingDeleteInvoice, value);
    }

    private ExpenseInvoice? _invoiceToDelete;

    private void DeleteInvoice(object? param)
    {
        if (param is ExpenseInvoice invoice)
        {
            _invoiceToDelete = invoice;
            IsConfirmingDeleteInvoice = true;
        }
    }

    private void ConfirmDeleteInvoice()
    {
        if (_invoiceToDelete != null)
        {
            _db.ExpenseInvoices.Remove(_invoiceToDelete);
            _db.SaveChanges();
            
            if (SelectedInvoice?.Id == _invoiceToDelete.Id)
            {
                SelectedInvoice = null;
            }
            
            _invoiceToDelete = null;
            IsConfirmingDeleteInvoice = false;
            LoadInvoices(_currentPropertyId);
            CancelEdit();
        }
    }

    private void CancelDeleteInvoice()
    {
        _invoiceToDelete = null;
        IsConfirmingDeleteInvoice = false;
    }

    private void OpenFile(object? param)
    {
        var invoice = param as ExpenseInvoice ?? SelectedInvoice;
        if (invoice == null || (invoice.FileContent == null && (string.IsNullOrWhiteSpace(invoice.FilePath) || !File.Exists(invoice.FilePath))))
            return;

        try
        {
            string targetPath = invoice.FilePath ?? string.Empty;

            if (invoice.FileContent != null && invoice.FileContent.Length > 0)
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "TenantManagerInvoices");
                Directory.CreateDirectory(tempDir);
                
                var fileName = string.IsNullOrWhiteSpace(targetPath) ? $"invoice_{invoice.Id}.pdf" : Path.GetFileName(targetPath);
                targetPath = Path.Combine(tempDir, fileName);
                
                File.WriteAllBytes(targetPath, invoice.FileContent);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fail silently
        }
    }
}
