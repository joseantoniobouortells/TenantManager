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
    public string FileStatus { get; set; } = string.Empty;
    public bool FileExists { get; set; }
}

public class ContractListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private ContractDisplayItem? _selectedItem;
    private RentalContract? _editingContract;
    private bool _isEditing;
    private Tenant? _editSelectedTenant;
    private string _editFilePath = string.Empty;
    private byte[]? _editFileContent;
    private DateTimeOffset? _editStartDate;
    private DateTimeOffset? _editEndDate;
    private decimal _editMonthlyRent;
    private ExpensePaymentType _editExpensePaymentType;
    private decimal _editFixedExpenseAmount;
    private string? _editNotes;
    private int _currentPropertyId;

    public ContractListViewModel()
    {
        _db = new AppDbContext();
        Contracts = new ObservableCollection<ContractDisplayItem>();
        AvailableTenants = new ObservableCollection<Tenant>();
        AvailableExpenseTypes = new ObservableCollection<ExpensePaymentType>(Enum.GetValues<ExpensePaymentType>());
        Extensions = new ObservableCollection<RentalContractExtension>();

        LoadContractsCommand = new RelayCommand(_ => LoadContracts(_currentPropertyId));
        NewContractCommand = new RelayCommand(_ => StartNewContract());
        EditContractCommand = new RelayCommand(_ => EditContract());
        SaveContractCommand = new RelayCommand(_ => SaveContract());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        OpenFileCommand = new RelayCommand(_ => OpenFile());
        DeleteContractCommand = new RelayCommand(_ => DeleteContract());

        NewExtensionCommand = new RelayCommand(_ => StartNewExtension());
        EditExtensionCommand = new RelayCommand(_ => EditExtension());
        SaveExtensionCommand = new RelayCommand(_ => SaveExtension());
        CancelExtensionCommand = new RelayCommand(_ => CancelExtension());
        DeleteExtensionCommand = new RelayCommand(_ => DeleteExtension());
        OpenFileExtensionCommand = new RelayCommand(_ => OpenFileExtension());
    }

    public ObservableCollection<ContractDisplayItem> Contracts { get; }
    public ObservableCollection<Tenant> AvailableTenants { get; }
    public ObservableCollection<ExpensePaymentType> AvailableExpenseTypes { get; }
    public ObservableCollection<RentalContractExtension> Extensions { get; }

    public RelayCommand LoadContractsCommand { get; }
    public RelayCommand NewContractCommand { get; }
    public RelayCommand EditContractCommand { get; }
    public RelayCommand SaveContractCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand DeleteContractCommand { get; }

    public RelayCommand NewExtensionCommand { get; }
    public RelayCommand EditExtensionCommand { get; }
    public RelayCommand SaveExtensionCommand { get; }
    public RelayCommand CancelExtensionCommand { get; }
    public RelayCommand DeleteExtensionCommand { get; }
    public RelayCommand OpenFileExtensionCommand { get; }

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

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    private RentalContractExtension? _selectedExtension;
    public RentalContractExtension? SelectedExtension
    {
        get => _selectedExtension;
        set => SetProperty(ref _selectedExtension, value);
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

        _db.ChangeTracker.Clear();
        LoadAvailableTenants();

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        Contracts.Clear();
        foreach (var contract in _db.RentalContracts.Where(c => c.PropertyId == propertyId).AsEnumerable().OrderBy(c => c.StartDate))
        {
            var exists = contract.FileContent != null || (!string.IsNullOrWhiteSpace(contract.FilePath) && File.Exists(contract.FilePath));
            Contracts.Add(new ContractDisplayItem
            {
                Contract = contract,
                TenantName = tenantLookup.TryGetValue(contract.TenantId, out var name) ? name : $"(id={contract.TenantId})",
                FileExists = exists,
                FileStatus = exists ? "Yes" : "No"
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

    private void StartNewContract()
    {
        _editingContract = null;
        EditSelectedTenant = AvailableTenants.FirstOrDefault();
        EditFilePath = string.Empty;
        EditFileContent = null;
        EditStartDate = null;
        EditEndDate = null;
        EditMonthlyRent = 0;
        EditExpensePaymentType = ExpensePaymentType.Variable;
        EditFixedExpenseAmount = 0;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditContract()
    {
        if (SelectedItem == null)
            return;

        _editingContract = SelectedItem.Contract;
        EditSelectedTenant = AvailableTenants.FirstOrDefault(t => t.Id == _editingContract.TenantId);
        EditFilePath = _editingContract.FilePath;
        EditFileContent = _editingContract.FileContent;
        EditStartDate = _editingContract.StartDate != default
            ? _editingContract.StartDate
            : null;
        EditEndDate = _editingContract.EndDate is DateTimeOffset ed
            ? ed
            : null;
        EditMonthlyRent = _editingContract.MonthlyRent;
        EditExpensePaymentType = _editingContract.ExpensePaymentType;
        EditFixedExpenseAmount = _editingContract.FixedExpenseAmount;
        EditNotes = _editingContract.Notes;
        IsEditing = true;
    }

    private void SaveContract()
    {
        if (EditSelectedTenant == null)
            return;

        if (_editingContract == null)
        {
            var contract = new RentalContract
            {
                PropertyId = _currentPropertyId,
                TenantId = EditSelectedTenant.Id,
                FilePath = EditFilePath.Trim(),
                FileContent = EditFileContent,
                StartDate = EditStartDate ?? DateTimeOffset.Now,
                EndDate = EditEndDate,
                MonthlyRent = EditMonthlyRent,
                ExpensePaymentType = EditExpensePaymentType,
                FixedExpenseAmount = EditExpensePaymentType == ExpensePaymentType.Fixed ? EditFixedExpenseAmount : 0,
                Notes = EditNotes?.Trim()
            };
            _db.RentalContracts.Add(contract);
        }
        else
        {
            _editingContract.TenantId = EditSelectedTenant.Id;
            _editingContract.FilePath = EditFilePath.Trim();
            _editingContract.FileContent = EditFileContent;
            _editingContract.StartDate = EditStartDate ?? DateTimeOffset.Now;
            _editingContract.EndDate = EditEndDate;
            _editingContract.MonthlyRent = EditMonthlyRent;
            _editingContract.ExpensePaymentType = EditExpensePaymentType;
            _editingContract.FixedExpenseAmount = EditExpensePaymentType == ExpensePaymentType.Fixed ? EditFixedExpenseAmount : 0;
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
        EditFilePath = string.Empty;
        EditFileContent = null;
        EditStartDate = null;
        EditEndDate = null;
        EditMonthlyRent = 0;
        EditExpensePaymentType = ExpensePaymentType.Variable;
        EditFixedExpenseAmount = 0;
        EditNotes = null;
        IsEditing = false;
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

    private void DeleteContract()
    {
        if (SelectedItem == null)
            return;

        _db.RentalContracts.Remove(SelectedItem.Contract);
        _db.SaveChanges();
        LoadContracts(_currentPropertyId);
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

    private void DeleteExtension()
    {
        if (SelectedExtension == null) return;

        _db.RentalContractExtensions.Remove(SelectedExtension);
        _db.SaveChanges();
        LoadExtensions();
        LoadContracts(_currentPropertyId);
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
