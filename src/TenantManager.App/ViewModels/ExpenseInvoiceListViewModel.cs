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
    private ExpenseCategory? _selectedFilterCategory;
    private string _selectedFilterYear = "Todos los años";
    private string _sortColumn = "Year";
    private bool _sortAscending = false;
    private ExpenseInvoice? _editingInvoice;
    private ExpenseInvoice? _selectedInvoice;
    private bool _isEditing;

    private ExpenseCategory? _editCategory;
    private string _editConcept = string.Empty;
    private bool _isManagingCategory;
    private bool _isEditingExistingCategory;
    private string _newCategoryName = string.Empty;
    private bool _newCategoryIsChargeable;

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
        AvailableCategories = new ObservableCollection<ExpenseCategory>();
        FilterCategories = new ObservableCollection<ExpenseCategory>();
        FilterYears = new ObservableCollection<string>();

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
        StartNewCategoryCommand = new RelayCommand(_ => StartNewCategory());
        StartEditCategoryCommand = new RelayCommand(_ => StartEditCategory());
        SaveNewCategoryCommand = new RelayCommand(_ => SaveCategory());
        CancelNewCategoryCommand = new RelayCommand(_ => { IsManagingCategory = false; });
    }

    public ObservableCollection<ExpenseInvoice> Invoices { get; }
    public ObservableCollection<ExpenseCategory> AvailableCategories { get; }
    public ObservableCollection<ExpenseCategory> FilterCategories { get; }
    public ObservableCollection<string> FilterYears { get; }

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
    public RelayCommand StartNewCategoryCommand { get; }
    public RelayCommand StartEditCategoryCommand { get; }
    public RelayCommand SaveNewCategoryCommand { get; }
    public RelayCommand CancelNewCategoryCommand { get; }

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

    public ExpenseCategory? SelectedFilterCategory
    {
        get => _selectedFilterCategory;
        set
        {
            if (SetProperty(ref _selectedFilterCategory, value))
            {
                ApplyFiltersAndSort();
            }
        }
    }

    public string SelectedFilterYear
    {
        get => _selectedFilterYear;
        set
        {
            if (SetProperty(ref _selectedFilterYear, value))
            {
                ApplyFiltersAndSort();
            }
        }
    }

    public string ConceptSortIndicator => _sortColumn == "Concept" ? (_sortAscending ? "▲" : "▼") : "";
    public string CategorySortIndicator => _sortColumn == "CategoryName" ? (_sortAscending ? "▲" : "▼") : "";
    public string YearSortIndicator => _sortColumn == "Year" ? (_sortAscending ? "▲" : "▼") : "";
    public string MonthSortIndicator => _sortColumn == "Month" ? (_sortAscending ? "▲" : "▼") : "";
    public string AmountSortIndicator => _sortColumn == "Amount" ? (_sortAscending ? "▲" : "▼") : "";

    public ExpenseCategory? EditCategory
    {
        get => _editCategory;
        set => SetProperty(ref _editCategory, value);
    }

    public string EditConcept
    {
        get => _editConcept;
        set => SetProperty(ref _editConcept, value);
    }

    public bool IsManagingCategory
    {
        get => _isManagingCategory;
        set => SetProperty(ref _isManagingCategory, value);
    }

    public string CategoryOverlayTitle => _isEditingExistingCategory ? "Editar Categoría" : "Añadir Categoría de Gasto";

    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    public bool NewCategoryIsChargeable
    {
        get => _newCategoryIsChargeable;
        set => SetProperty(ref _newCategoryIsChargeable, value);
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
        
        var categories = _db.ExpenseCategories.OrderBy(c => c.Name).ToList();
        AvailableCategories.Clear();
        foreach (var c in categories) AvailableCategories.Add(c);

        // Update filters
        FilterCategories.Clear();
        FilterCategories.Add(new ExpenseCategory { Id = 0, Name = "Todas las categorías" });
        foreach (var c in categories) FilterCategories.Add(c);
        SelectedFilterCategory = FilterCategories.FirstOrDefault();

        _allInvoices = _db.ExpenseInvoices.Where(i => i.PropertyId == propertyId).ToList();
        
        FilterYears.Clear();
        FilterYears.Add("Todos los años");
        var distinctYears = _allInvoices.Select(i => i.Year).Distinct().OrderByDescending(y => y).ToList();
        foreach (var y in distinctYears) FilterYears.Add(y.ToString());
        SelectedFilterYear = "Todos los años";

        // Map category UI properties
        foreach (var inv in _allInvoices)
        {
            var cat = categories.FirstOrDefault(c => c.Id == inv.CategoryId);
            if (cat != null)
            {
                inv.CategoryName = cat.Name;
                inv.IsChargeableToTenant = cat.IsChargeable;
            }
        }
        
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

        OnPropertyChanged(nameof(ConceptSortIndicator));
        OnPropertyChanged(nameof(CategorySortIndicator));
        OnPropertyChanged(nameof(YearSortIndicator));
        OnPropertyChanged(nameof(MonthSortIndicator));
        OnPropertyChanged(nameof(AmountSortIndicator));

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = _allInvoices.AsEnumerable();

        if (SelectedFilterCategory != null && SelectedFilterCategory.Id > 0)
        {
            filtered = filtered.Where(i => i.CategoryId == SelectedFilterCategory.Id);
        }

        if (SelectedFilterYear != "Todos los años")
        {
            filtered = filtered.Where(i => i.Year.ToString() == SelectedFilterYear);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(i => 
                (i.Concept?.ToLowerInvariant().Contains(q) ?? false) ||
                (i.CategoryName?.ToLowerInvariant().Contains(q) ?? false) ||
                i.Year.ToString().Contains(q) ||
                i.Month.ToString().Contains(q));
        }

        filtered = _sortColumn switch
        {
            "Concept" => _sortAscending ? filtered.OrderBy(i => i.Concept) : filtered.OrderByDescending(i => i.Concept),
            "CategoryName" => _sortAscending ? filtered.OrderBy(i => i.CategoryName) : filtered.OrderByDescending(i => i.CategoryName),
            "Year" => _sortAscending ? filtered.OrderBy(i => i.Year).ThenBy(i => i.Month) : filtered.OrderByDescending(i => i.Year).ThenByDescending(i => i.Month),
            "Month" => _sortAscending ? filtered.OrderBy(i => i.Month) : filtered.OrderByDescending(i => i.Month),
            "Amount" => _sortAscending ? filtered.OrderBy(i => i.Amount) : filtered.OrderByDescending(i => i.Amount),
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
        EditConcept = string.Empty;
        EditCategory = AvailableCategories.FirstOrDefault();
        IsManagingCategory = false;
        NewCategoryName = string.Empty;
        NewCategoryIsChargeable = false;
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
        EditConcept = _editingInvoice.Concept;
        EditCategory = AvailableCategories.FirstOrDefault(c => c.Id == _editingInvoice.CategoryId);
        IsManagingCategory = false;
        NewCategoryName = string.Empty;
        NewCategoryIsChargeable = false;
        EditYear = _editingInvoice.Year;
        EditMonth = _editingInvoice.Month;
        EditAmount = _editingInvoice.Amount;
        EditNotes = _editingInvoice.Notes;
        EditFilePath = _editingInvoice.FilePath ?? string.Empty;
        EditFileContent = _editingInvoice.FileContent;
        IsEditing = true;
    }

    private void StartNewCategory()
    {
        _isEditingExistingCategory = false;
        OnPropertyChanged(nameof(CategoryOverlayTitle));
        NewCategoryName = string.Empty;
        NewCategoryIsChargeable = false;
        IsManagingCategory = true;
    }

    private void StartEditCategory()
    {
        if (EditCategory == null) return;
        _isEditingExistingCategory = true;
        OnPropertyChanged(nameof(CategoryOverlayTitle));
        NewCategoryName = EditCategory.Name;
        NewCategoryIsChargeable = EditCategory.IsChargeable;
        IsManagingCategory = true;
    }

    private void SaveCategory()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

        int selectedId = 0;

        if (_isEditingExistingCategory && EditCategory != null)
        {
            var cat = _db.ExpenseCategories.Find(EditCategory.Id);
            if (cat != null)
            {
                cat.Name = NewCategoryName.Trim();
                cat.IsChargeable = NewCategoryIsChargeable;
                _db.SaveChanges();
                selectedId = cat.Id;

                // Visual refresh: update historical invoices in memory so UI reflects the new name without a full reload
                foreach (var inv in _allInvoices.Where(i => i.CategoryId == cat.Id))
                {
                    inv.CategoryName = cat.Name;
                    inv.IsChargeableToTenant = cat.IsChargeable;
                }
                
                // Force UI re-filter/sort to update the table immediately
                ApplyFiltersAndSort();
            }
        }
        else
        {
            var newCat = new ExpenseCategory { Name = NewCategoryName.Trim(), IsChargeable = NewCategoryIsChargeable };
            _db.ExpenseCategories.Add(newCat);
            _db.SaveChanges();
            selectedId = newCat.Id;
        }

        // Resync categories in alphabetical order
        var sorted = _db.ExpenseCategories.OrderBy(c => c.Name).ToList();
        AvailableCategories.Clear();
        foreach (var c in sorted) AvailableCategories.Add(c);

        // Also update FilterCategories
        var prevFilterId = SelectedFilterCategory?.Id ?? 0;
        FilterCategories.Clear();
        FilterCategories.Add(new ExpenseCategory { Id = 0, Name = "Todas las categorías" });
        foreach (var c in sorted) FilterCategories.Add(c);
        SelectedFilterCategory = FilterCategories.FirstOrDefault(c => c.Id == prevFilterId) ?? FilterCategories.FirstOrDefault();

        if (selectedId > 0)
        {
            EditCategory = AvailableCategories.FirstOrDefault(c => c.Id == selectedId);
        }

        IsManagingCategory = false;
    }

    private void SaveInvoice()
    {
        if (EditAmount < 0 || EditCategory == null || string.IsNullOrWhiteSpace(EditConcept))
            return;
            
        int finalCategoryId = EditCategory.Id;

        if (_editingInvoice == null)
        {
            var invoice = new ExpenseInvoice
            {
                PropertyId = _currentPropertyId,
                Concept = EditConcept.Trim(),
                CategoryId = finalCategoryId,
                Year = (int)EditYear,
                Month = (int)EditMonth,
                Amount = EditAmount,
                Notes = EditNotes?.Trim(),
                FilePath = EditFilePath?.Trim(),
                FileContent = EditFileContent
            };
            _db.ExpenseInvoices.Add(invoice);
        }
        else
        {
            _editingInvoice.Concept = EditConcept.Trim();
            _editingInvoice.CategoryId = finalCategoryId;
            _editingInvoice.Year = (int)EditYear;
            _editingInvoice.Month = (int)EditMonth;
            _editingInvoice.Amount = EditAmount;
            _editingInvoice.Notes = EditNotes?.Trim() ?? string.Empty;
            _editingInvoice.FilePath = EditFilePath?.Trim() ?? string.Empty;
            _editingInvoice.FileContent = EditFileContent;
        }

        _db.SaveChanges();

        // Update FilterYears if a new year was added
        var invoiceYearStr = ((int)EditYear).ToString();
        if (!FilterYears.Contains(invoiceYearStr))
        {
            var years = FilterYears.Where(y => y != "Todos los años").Select(int.Parse).ToList();
            years.Add((int)EditYear);
            years = years.OrderByDescending(y => y).ToList();
            FilterYears.Clear();
            FilterYears.Add("Todos los años");
            foreach (var y in years) FilterYears.Add(y.ToString());
        }

        LoadInvoices(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingInvoice = null;
        EditConcept = string.Empty;
        EditCategory = null;
        IsManagingCategory = false;
        NewCategoryName = string.Empty;
        NewCategoryIsChargeable = false;
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
