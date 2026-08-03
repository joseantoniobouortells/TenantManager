using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class ContractDisplayItem
{
    public RentalContract Contract { get; init; } = null!;
    public string TenantName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string FileStatus { get; set; } = string.Empty;
    public bool FileExists { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    
    public bool IsActive => StartDate.Date <= DateTime.Today && (!EndDate.HasValue || EndDate.Value.Date >= DateTime.Today);
}

public class ContractListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private List<ContractDisplayItem> _allContracts = new();
    private string _searchQuery = string.Empty;
    private ContractDisplayItem? _selectedItem;
    private RentalContract? _editingContract;
    private bool _isEditing;
    private Tenant? _editSelectedTenant;
    private Room? _editSelectedRoom;
    private string _editFilePath = string.Empty;
    private byte[]? _editFileContent;
    private DateTimeOffset? _editStartDate;
    private DateTimeOffset? _editEndDate;
    private decimal _editMonthlyRent;
    private decimal _editDepositAmount;
    private ExpensePaymentType _editExpensePaymentType;
    private decimal _editFixedExpenseAmount;
    private decimal _editVariableExpensePercentage;
    private string? _editNotes;
    private int _currentPropertyId;

    public ContractListViewModel()
    {
        _db = new AppDbContext();
        Contracts = new ObservableCollection<ContractDisplayItem>();
        AvailableTenants = new ObservableCollection<Tenant>();
        AvailableRooms = new ObservableCollection<Room>();
        AvailableExpenseTypes = new ObservableCollection<ExpensePaymentType>(Enum.GetValues<ExpensePaymentType>());
        Extensions = new ObservableCollection<RentalContractExtension>();

        LoadContractsCommand = new RelayCommand(_ => LoadContracts(_currentPropertyId));
        NewContractCommand = new RelayCommand(_ => StartNewContract());
        EditContractCommand = new RelayCommand(_ => EditContract());
        SaveContractCommand = new RelayCommand(_ => SaveContract());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        OpenFileCommand = new RelayCommand(_ => OpenFile());
        DeleteContractCommand = new RelayCommand(param => DeleteContract(param));
        SortCommand = new RelayCommand(field => Sort(field as string));
        ConfirmDeleteContractCommand = new RelayCommand(_ => ConfirmDeleteContract());
        CancelDeleteContractCommand = new RelayCommand(_ => CancelDeleteContract());

        NewExtensionCommand = new RelayCommand(_ => StartNewExtension());
        EditExtensionCommand = new RelayCommand(_ => EditExtension());
        SaveExtensionCommand = new RelayCommand(_ => SaveExtension());
        CancelExtensionCommand = new RelayCommand(_ => CancelExtension());
        DeleteExtensionCommand = new RelayCommand(_ => DeleteExtension());
        OpenFileExtensionCommand = new RelayCommand(_ => OpenFileExtension());
        ConfirmDeleteExtensionCommand = new RelayCommand(_ => ConfirmDeleteExtension());
        CancelDeleteExtensionCommand = new RelayCommand(_ => CancelDeleteExtension());
    }

    public ObservableCollection<ContractDisplayItem> Contracts { get; }
    public ObservableCollection<Tenant> AvailableTenants { get; }
    public ObservableCollection<Room> AvailableRooms { get; }
    public ObservableCollection<ExpensePaymentType> AvailableExpenseTypes { get; }
    public ObservableCollection<RentalContractExtension> Extensions { get; }

    public RelayCommand LoadContractsCommand { get; }
    public RelayCommand NewContractCommand { get; }
    public RelayCommand EditContractCommand { get; }
    public RelayCommand SaveContractCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand DeleteContractCommand { get; }
    public RelayCommand SortCommand { get; }
    public RelayCommand ConfirmDeleteContractCommand { get; }
    public RelayCommand CancelDeleteContractCommand { get; }

    public RelayCommand NewExtensionCommand { get; }
    public RelayCommand EditExtensionCommand { get; }
    public RelayCommand SaveExtensionCommand { get; }
    public RelayCommand CancelExtensionCommand { get; }
    public RelayCommand DeleteExtensionCommand { get; }
    public RelayCommand OpenFileExtensionCommand { get; }
    public RelayCommand ConfirmDeleteExtensionCommand { get; }
    public RelayCommand CancelDeleteExtensionCommand { get; }

    public ContractDisplayItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(HasSelectedContract));
                LoadExtensions();
                CancelExtension();
                IsConfirmingDeleteExtension = false;
                if (_selectedItem != null) EditContract();
            }
        }
    }

    public bool HasSelectedContract => SelectedItem != null;

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

    public Room? EditSelectedRoom
    {
        get => _editSelectedRoom;
        set => SetProperty(ref _editSelectedRoom, value);
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

    public DateTimeOffset? EditStartDate
    {
        get => _editStartDate;
        set => SetProperty(ref _editStartDate, value);
    }

    public DateTimeOffset? EditEndDate
    {
        get => _editEndDate;
        set => SetProperty(ref _editEndDate, value);
    }

    public decimal EditMonthlyRent
    {
        get => _editMonthlyRent;
        set => SetProperty(ref _editMonthlyRent, value);
    }

    public decimal EditDepositAmount
    {
        get => _editDepositAmount;
        set => SetProperty(ref _editDepositAmount, value);
    }

    private int _editPaymentDay = 1;
    public int EditPaymentDay
    {
        get => _editPaymentDay;
        set => SetProperty(ref _editPaymentDay, value);
    }

    public ExpensePaymentType EditExpensePaymentType
    {
        get => _editExpensePaymentType;
        set => SetProperty(ref _editExpensePaymentType, value);
    }

    public decimal EditFixedExpenseAmount
    {
        get => _editFixedExpenseAmount;
        set => SetProperty(ref _editFixedExpenseAmount, value);
    }

    public decimal EditVariableExpensePercentage
    {
        get => _editVariableExpensePercentage;
        set => SetProperty(ref _editVariableExpensePercentage, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    private RentalContractExtension? _selectedExtension;
    public RentalContractExtension? SelectedExtension
    {
        get => _selectedExtension;
        set
        {
            if (SetProperty(ref _selectedExtension, value))
            {
                IsConfirmingDeleteExtension = false;
            }
        }
    }

    private RentalContractExtension? _editingExtension;
    private bool _isEditingExtension;
    public bool IsEditingExtension
    {
        get => _isEditingExtension;
        set => SetProperty(ref _isEditingExtension, value);
    }

    private DateTimeOffset _extensionEditStartDate = DateTimeOffset.Now;
    public DateTimeOffset ExtensionEditStartDate
    {
        get => _extensionEditStartDate;
        set => SetProperty(ref _extensionEditStartDate, value);
    }

    private DateTimeOffset? _extensionEditEndDate;
    public DateTimeOffset? ExtensionEditEndDate
    {
        get => _extensionEditEndDate;
        set => SetProperty(ref _extensionEditEndDate, value);
    }

    private decimal _extensionEditMonthlyRent;
    public decimal ExtensionEditMonthlyRent
    {
        get => _extensionEditMonthlyRent;
        set => SetProperty(ref _extensionEditMonthlyRent, value);
    }

    private ExpensePaymentType _extensionEditExpensePaymentType;
    public ExpensePaymentType ExtensionEditExpensePaymentType
    {
        get => _extensionEditExpensePaymentType;
        set => SetProperty(ref _extensionEditExpensePaymentType, value);
    }

    private decimal _extensionEditFixedExpenseAmount;
    public decimal ExtensionEditFixedExpenseAmount
    {
        get => _extensionEditFixedExpenseAmount;
        set => SetProperty(ref _extensionEditFixedExpenseAmount, value);
    }

    private decimal _extensionEditVariableExpensePercentage;
    public decimal ExtensionEditVariableExpensePercentage
    {
        get => _extensionEditVariableExpensePercentage;
        set => SetProperty(ref _extensionEditVariableExpensePercentage, value);
    }

    private string _extensionEditFilePath = string.Empty;
    public string ExtensionEditFilePath
    {
        get => _extensionEditFilePath;
        set => SetProperty(ref _extensionEditFilePath, value);
    }

    private byte[]? _extensionEditFileContent;
    public byte[]? ExtensionEditFileContent
    {
        get => _extensionEditFileContent;
        set => SetProperty(ref _extensionEditFileContent, value);
    }

    private string? _extensionEditNotes;
    public string? ExtensionEditNotes
    {
        get => _extensionEditNotes;
        set => SetProperty(ref _extensionEditNotes, value);
    }

    public void LoadContracts(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        var selectedContractId = SelectedItem?.Contract?.Id;

        _db.ChangeTracker.Clear();
        LoadAvailableTenants();
        LoadAvailableRooms();

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);
        var roomLookup = _db.Rooms.ToDictionary(r => r.Id, r => r.Name);

        var contractIds = _db.RentalContracts.Where(c => c.PropertyId == propertyId).Select(c => c.Id).ToList();
        var extensions = _db.RentalContractExtensions.Where(e => contractIds.Contains(e.RentalContractId)).ToList();

        var items = new List<ContractDisplayItem>();
        foreach (var contract in _db.RentalContracts.Where(c => c.PropertyId == propertyId).AsEnumerable())
        {
            var exists = contract.FileContent != null || (!string.IsNullOrWhiteSpace(contract.FilePath) && File.Exists(contract.FilePath));
            
            var contractExtensions = extensions.Where(e => e.RentalContractId == contract.Id).ToList();
            var displayEndDate = contract.EndDate;
            if (contractExtensions.Any())
            {
                var latestExtension = contractExtensions.OrderByDescending(e => e.StartDate).First();
                displayEndDate = latestExtension.EndDate;
            }

            items.Add(new ContractDisplayItem
            {
                Contract = contract,
                TenantName = tenantLookup.TryGetValue(contract.TenantId, out var name) ? name : $"(id={contract.TenantId})",
                RoomName = roomLookup.TryGetValue(contract.RoomId, out var rName) ? rName : $"(id={contract.RoomId})",
                FileExists = exists,
                FileStatus = exists ? "Yes" : "No",
                StartDate = contract.StartDate,
                EndDate = displayEndDate
            });
        }

        _allContracts = items;
        ApplyFiltersAndSort();

        if (selectedContractId.HasValue)
        {
            var itemToSelect = Contracts.FirstOrDefault(c => c.Contract.Id == selectedContractId.Value);
            if (itemToSelect != null)
            {
                SelectedItem = itemToSelect;
            }
        }
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = _allContracts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(i => 
                (i.TenantName?.ToLowerInvariant().Contains(q) ?? false) ||
                (i.RoomName?.ToLowerInvariant().Contains(q) ?? false));
        }

        IEnumerable<ContractDisplayItem> sorted;
        if (_currentSortField == "StartDate")
        {
            sorted = _isSortAscending 
                ? filtered.OrderBy(i => i.StartDate) 
                : filtered.OrderByDescending(i => i.StartDate);
        }
        else if (_currentSortField == "EndDate")
        {
            sorted = _isSortAscending 
                ? filtered.OrderBy(i => i.EndDate ?? DateTimeOffset.MaxValue) 
                : filtered.OrderByDescending(i => i.EndDate ?? DateTimeOffset.MinValue);
        }
        else
        {
            sorted = _isSortAscending 
                ? filtered.OrderBy(i => i.TenantName) 
                : filtered.OrderByDescending(i => i.TenantName);
        }

        Contracts.Clear();
        foreach (var item in sorted)
        {
            Contracts.Add(item);
        }
    }

    private string _currentSortField = "Tenant";
    private bool _isSortAscending = true;

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

    public string TenantSortIndicator => GetSortIndicator("Tenant");
    public string StartDateSortIndicator => GetSortIndicator("StartDate");
    public string EndDateSortIndicator => GetSortIndicator("EndDate");

    private string GetSortIndicator(string field)
    {
        if (_currentSortField != field) return string.Empty;
        return _isSortAscending ? " ▲" : " ▼";
    }

    private void NotifySortIndicators()
    {
        OnPropertyChanged(nameof(TenantSortIndicator));
        OnPropertyChanged(nameof(StartDateSortIndicator));
        OnPropertyChanged(nameof(EndDateSortIndicator));
    }

    public void Sort(string? field)
    {
        if (string.IsNullOrEmpty(field)) return;
        
        if (_currentSortField == field)
        {
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            _currentSortField = field;
            _isSortAscending = true;
        }
        
        NotifySortIndicators();
        ApplyFiltersAndSort();
    }

    private void LoadAvailableTenants()
    {
        AvailableTenants.Clear();
        foreach (var tenant in _db.Tenants.Where(t => t.PropertyId == _currentPropertyId).OrderBy(t => t.FullName))
        {
            AvailableTenants.Add(tenant);
        }
    }

    private void LoadAvailableRooms()
    {
        AvailableRooms.Clear();
        foreach (var room in _db.Rooms.Where(r => r.IsActive && r.PropertyId == _currentPropertyId).OrderBy(r => r.Name))
        {
            AvailableRooms.Add(room);
        }
    }

    private void StartNewContract()
    {
        SelectedItem = null;
        _editingContract = null;
        EditSelectedTenant = AvailableTenants.FirstOrDefault();
        EditSelectedRoom = AvailableRooms.FirstOrDefault();
        EditFilePath = string.Empty;
        EditFileContent = null;
        EditStartDate = null;
        EditEndDate = null;
        EditMonthlyRent = 0;
        EditDepositAmount = 0;
        EditExpensePaymentType = ExpensePaymentType.Variable;
        EditFixedExpenseAmount = 0;
        var roomCount = AvailableRooms.Count;
        EditVariableExpensePercentage = roomCount > 0 ? 100m / roomCount : 0m;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditContract()
    {
        if (SelectedItem == null)
            return;

        _editingContract = SelectedItem.Contract;
        EditSelectedTenant = AvailableTenants.FirstOrDefault(t => t.Id == _editingContract.TenantId);
        EditSelectedRoom = AvailableRooms.FirstOrDefault(r => r.Id == _editingContract.RoomId);
        EditFilePath = _editingContract.FilePath;
        EditFileContent = _editingContract.FileContent;
        EditStartDate = _editingContract.StartDate != default
            ? _editingContract.StartDate
            : null;
        EditEndDate = _editingContract.EndDate is DateTimeOffset ed
            ? ed
            : null;
        EditMonthlyRent = _editingContract.MonthlyRent;
        EditDepositAmount = _editingContract.DepositAmount;
        EditPaymentDay = _editingContract.PaymentDay;
        EditExpensePaymentType = _editingContract.ExpensePaymentType;
        EditFixedExpenseAmount = _editingContract.FixedExpenseAmount;
        EditVariableExpensePercentage = _editingContract.VariableExpensePercentage;
        EditNotes = _editingContract.Notes;
        IsEditing = true;
    }

    private void SaveContract()
    {
        if (EditSelectedTenant == null || EditSelectedRoom == null || EditEndDate == null)
            return;

        if (_editingContract == null)
        {
            var contract = new RentalContract
            {
                PropertyId = _currentPropertyId,
                TenantId = EditSelectedTenant.Id,
                RoomId = EditSelectedRoom.Id,
                FilePath = EditFilePath.Trim(),
                FileContent = EditFileContent,
                StartDate = EditStartDate ?? DateTimeOffset.Now,
                EndDate = EditEndDate,
                MonthlyRent = EditMonthlyRent,
                DepositAmount = EditDepositAmount,
                PaymentDay = EditPaymentDay,
                ExpensePaymentType = EditExpensePaymentType,
                FixedExpenseAmount = EditExpensePaymentType == ExpensePaymentType.Fixed ? EditFixedExpenseAmount : 0,
                VariableExpensePercentage = EditExpensePaymentType == ExpensePaymentType.Variable ? EditVariableExpensePercentage : 0,
                Notes = EditNotes?.Trim()
            };
            _db.RentalContracts.Add(contract);
        }
        else
        {
            _editingContract.TenantId = EditSelectedTenant.Id;
            _editingContract.RoomId = EditSelectedRoom.Id;
            _editingContract.FilePath = EditFilePath.Trim();
            _editingContract.FileContent = EditFileContent;
            _editingContract.StartDate = EditStartDate ?? DateTimeOffset.Now;
            _editingContract.EndDate = EditEndDate;
            _editingContract.MonthlyRent = EditMonthlyRent;
            _editingContract.DepositAmount = EditDepositAmount;
            _editingContract.PaymentDay = EditPaymentDay;
            _editingContract.ExpensePaymentType = EditExpensePaymentType;
            _editingContract.FixedExpenseAmount = EditExpensePaymentType == ExpensePaymentType.Fixed ? EditFixedExpenseAmount : 0;
            _editingContract.VariableExpensePercentage = EditExpensePaymentType == ExpensePaymentType.Variable ? EditVariableExpensePercentage : 0;
            _editingContract.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadContracts(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingContract = null;
        EditSelectedTenant = null;
        EditSelectedRoom = null;
        EditFilePath = string.Empty;
        EditFileContent = null;
        EditStartDate = null;
        EditEndDate = null;
        EditMonthlyRent = 0;
        EditDepositAmount = 0;
        EditExpensePaymentType = ExpensePaymentType.Variable;
        EditFixedExpenseAmount = 0;
        EditNotes = null;
        IsEditing = false;
        SelectedItem = null;
    }

    private void OpenFile()
    {
        if (SelectedItem == null || !SelectedItem.FileExists)
            return;

        try
        {
            string targetPath = SelectedItem.Contract.FilePath;

            if (SelectedItem.Contract.FileContent != null && SelectedItem.Contract.FileContent.Length > 0)
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "TenantManagerContracts");
                Directory.CreateDirectory(tempDir);
                
                var fileName = string.IsNullOrWhiteSpace(targetPath) ? $"contract_{SelectedItem.Contract.Id}.pdf" : Path.GetFileName(targetPath);
                targetPath = Path.Combine(tempDir, fileName);
                
                File.WriteAllBytes(targetPath, SelectedItem.Contract.FileContent);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private ContractDisplayItem? _contractToDelete;
    private bool _isConfirmingDeleteContract;
    public bool IsConfirmingDeleteContract
    {
        get => _isConfirmingDeleteContract;
        set => SetProperty(ref _isConfirmingDeleteContract, value);
    }

    private void DeleteContract(object? param)
    {
        if (param is ContractDisplayItem item)
        {
            _contractToDelete = item;
            IsConfirmingDeleteContract = true;
        }
    }

    private void ConfirmDeleteContract()
    {
        if (_contractToDelete != null)
        {
            _db.RentalContracts.Remove(_contractToDelete.Contract);
            _db.SaveChanges();
            
            if (SelectedItem?.Contract?.Id == _contractToDelete.Contract.Id)
            {
                SelectedItem = null;
            }
            
            _contractToDelete = null;
            IsConfirmingDeleteContract = false;
            LoadContracts(_currentPropertyId);
        }
    }

    private void CancelDeleteContract()
    {
        _contractToDelete = null;
        IsConfirmingDeleteContract = false;
    }

    private void LoadExtensions()
    {
        Extensions.Clear();
        if (SelectedItem != null)
        {
            var exts = _db.RentalContractExtensions
                .Where(e => e.RentalContractId == SelectedItem.Contract.Id)
                .AsEnumerable()
                .OrderByDescending(e => e.StartDate)
                .ToList();

            foreach (var ext in exts)
            {
                Extensions.Add(ext);
            }
        }
    }

    private void StartNewExtension()
    {
        if (SelectedItem == null) return;

        _editingExtension = null;
        ExtensionEditStartDate = DateTimeOffset.Now;
        ExtensionEditEndDate = null;
        ExtensionEditMonthlyRent = 0;
        ExtensionEditExpensePaymentType = ExpensePaymentType.Variable;
        ExtensionEditFixedExpenseAmount = 0;
        var roomCount = AvailableRooms.Count;
        ExtensionEditVariableExpensePercentage = roomCount > 0 ? 100m / roomCount : 0m;
        ExtensionEditFilePath = string.Empty;
        ExtensionEditFileContent = null;
        ExtensionEditNotes = null;
        IsEditingExtension = true;
    }

    private void EditExtension()
    {
        if (SelectedExtension == null) return;

        _editingExtension = SelectedExtension;
        ExtensionEditStartDate = _editingExtension.StartDate;
        ExtensionEditEndDate = _editingExtension.EndDate;
        ExtensionEditMonthlyRent = _editingExtension.MonthlyRent;
        ExtensionEditExpensePaymentType = _editingExtension.ExpensePaymentType;
        ExtensionEditFixedExpenseAmount = _editingExtension.FixedExpenseAmount;
        ExtensionEditVariableExpensePercentage = _editingExtension.VariableExpensePercentage;
        ExtensionEditFilePath = _editingExtension.FilePath ?? string.Empty;
        ExtensionEditFileContent = _editingExtension.FileContent;
        ExtensionEditNotes = _editingExtension.Notes;
        IsEditingExtension = true;
    }

    private void SaveExtension()
    {
        if (SelectedItem == null) return;

        if (_editingExtension == null)
        {
            var ext = new RentalContractExtension
            {
                PropertyId = _currentPropertyId,
                RentalContractId = SelectedItem.Contract.Id,
                StartDate = ExtensionEditStartDate,
                EndDate = ExtensionEditEndDate,
                MonthlyRent = ExtensionEditMonthlyRent,
                ExpensePaymentType = ExtensionEditExpensePaymentType,
                FixedExpenseAmount = ExtensionEditExpensePaymentType == ExpensePaymentType.Fixed ? ExtensionEditFixedExpenseAmount : 0,
                VariableExpensePercentage = ExtensionEditExpensePaymentType == ExpensePaymentType.Variable ? ExtensionEditVariableExpensePercentage : 0,
                FilePath = ExtensionEditFilePath.Trim(),
                FileContent = ExtensionEditFileContent,
                Notes = ExtensionEditNotes?.Trim()
            };
            _db.RentalContractExtensions.Add(ext);
        }
        else
        {
            _editingExtension.StartDate = ExtensionEditStartDate;
            _editingExtension.EndDate = ExtensionEditEndDate;
            _editingExtension.MonthlyRent = ExtensionEditMonthlyRent;
            _editingExtension.ExpensePaymentType = ExtensionEditExpensePaymentType;
            _editingExtension.FixedExpenseAmount = ExtensionEditExpensePaymentType == ExpensePaymentType.Fixed ? ExtensionEditFixedExpenseAmount : 0;
            _editingExtension.VariableExpensePercentage = ExtensionEditExpensePaymentType == ExpensePaymentType.Variable ? ExtensionEditVariableExpensePercentage : 0;
            _editingExtension.FilePath = ExtensionEditFilePath.Trim();
            _editingExtension.FileContent = ExtensionEditFileContent;
            _editingExtension.Notes = ExtensionEditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadExtensions();
        CancelExtension();
        LoadContracts(_currentPropertyId); // Reload to update EndDate if it's derived from extensions visually (for later)
    }

    private void CancelExtension()
    {
        _editingExtension = null;
        ExtensionEditFilePath = string.Empty;
        ExtensionEditFileContent = null;
        IsEditingExtension = false;
    }

    private bool _isConfirmingDeleteExtension;
    public bool IsConfirmingDeleteExtension
    {
        get => _isConfirmingDeleteExtension;
        set => SetProperty(ref _isConfirmingDeleteExtension, value);
    }

    private void DeleteExtension()
    {
        if (SelectedExtension == null) return;
        IsConfirmingDeleteExtension = true;
    }

    private void ConfirmDeleteExtension()
    {
        if (SelectedExtension == null) return;

        _db.RentalContractExtensions.Remove(SelectedExtension);
        _db.SaveChanges();
        IsConfirmingDeleteExtension = false;
        LoadExtensions();
        LoadContracts(_currentPropertyId);
    }

    private void CancelDeleteExtension()
    {
        IsConfirmingDeleteExtension = false;
    }

    private void OpenFileExtension()
    {
        if (SelectedExtension == null)
            return;

        try
        {
            string targetPath = SelectedExtension.FilePath ?? string.Empty;

            if (SelectedExtension.FileContent != null && SelectedExtension.FileContent.Length > 0)
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "TenantManagerContracts");
                Directory.CreateDirectory(tempDir);
                
                var fileName = string.IsNullOrWhiteSpace(targetPath) ? $"ext_{SelectedExtension.Id}.pdf" : Path.GetFileName(targetPath);
                targetPath = Path.Combine(tempDir, fileName);
                
                File.WriteAllBytes(targetPath, SelectedExtension.FileContent);
            }

            if (!string.IsNullOrEmpty(targetPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
        }
    }
}
