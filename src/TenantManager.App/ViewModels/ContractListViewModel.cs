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
    private DateTimeOffset? _editStartDate;
    private DateTimeOffset? _editEndDate;
    private string? _editNotes;

    public ContractListViewModel()
    {
        _db = new AppDbContext();
        Contracts = new ObservableCollection<ContractDisplayItem>();
        AvailableTenants = new ObservableCollection<Tenant>();

        LoadContractsCommand = new RelayCommand(_ => LoadContracts());
        NewContractCommand = new RelayCommand(_ => StartNewContract());
        EditContractCommand = new RelayCommand(_ => EditContract());
        SaveContractCommand = new RelayCommand(_ => SaveContract());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        OpenFileCommand = new RelayCommand(_ => OpenFile());
        DeleteContractCommand = new RelayCommand(_ => DeleteContract());

        LoadContracts();
    }

    public ObservableCollection<ContractDisplayItem> Contracts { get; }
    public ObservableCollection<Tenant> AvailableTenants { get; }

    public RelayCommand LoadContractsCommand { get; }
    public RelayCommand NewContractCommand { get; }
    public RelayCommand EditContractCommand { get; }
    public RelayCommand SaveContractCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand DeleteContractCommand { get; }

    public ContractDisplayItem? SelectedItem
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

    public string EditFilePath
    {
        get => _editFilePath;
        set => SetProperty(ref _editFilePath, value);
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

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public void LoadContracts()
    {
        LoadAvailableTenants();

        var tenantLookup = _db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        Contracts.Clear();
        foreach (var contract in _db.RentalContracts.OrderBy(c => c.StartDate))
        {
            var exists = !string.IsNullOrWhiteSpace(contract.FilePath) && File.Exists(contract.FilePath);
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
        foreach (var tenant in _db.Tenants.Where(t => t.IsActive).OrderBy(t => t.FullName))
        {
            AvailableTenants.Add(tenant);
        }
    }

    private void StartNewContract()
    {
        _editingContract = null;
        EditSelectedTenant = AvailableTenants.FirstOrDefault();
        EditFilePath = string.Empty;
        EditStartDate = null;
        EditEndDate = null;
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
        EditStartDate = _editingContract.StartDate != default
            ? new DateTimeOffset(_editingContract.StartDate)
            : null;
        EditEndDate = _editingContract.EndDate is DateTime ed
            ? new DateTimeOffset(ed)
            : null;
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
                TenantId = EditSelectedTenant.Id,
                FilePath = EditFilePath.Trim(),
                StartDate = EditStartDate?.DateTime ?? DateTime.Today,
                EndDate = EditEndDate?.DateTime,
                Notes = EditNotes?.Trim()
            };
            _db.RentalContracts.Add(contract);
        }
        else
        {
            _editingContract.TenantId = EditSelectedTenant.Id;
            _editingContract.FilePath = EditFilePath.Trim();
            _editingContract.StartDate = EditStartDate?.DateTime ?? DateTime.Today;
            _editingContract.EndDate = EditEndDate?.DateTime;
            _editingContract.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadContracts();
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingContract = null;
        EditSelectedTenant = null;
        EditFilePath = string.Empty;
        EditStartDate = null;
        EditEndDate = null;
        EditNotes = null;
        IsEditing = false;
    }

    private void OpenFile()
    {
        if (SelectedItem == null || !SelectedItem.FileExists)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedItem.Contract.FilePath,
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
        LoadContracts();
    }
}
