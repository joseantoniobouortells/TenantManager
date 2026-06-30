using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class TenantListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private Tenant? _editingTenant;
    private Tenant? _selectedTenant;
    private bool _isEditing;
    private string _editFullName = string.Empty;
    private string? _editPhone;
    private string? _editEmail;
    private string? _editNotes;
    private int _currentPropertyId;

    public TenantListViewModel()
    {
        _db = new AppDbContext();
        Tenants = new ObservableCollection<Tenant>();

        LoadTenantsCommand = new RelayCommand(_ => LoadTenants(_currentPropertyId));
        NewTenantCommand = new RelayCommand(_ => StartNewTenant());
        EditTenantCommand = new RelayCommand(_ => EditTenant());
        SaveTenantCommand = new RelayCommand(_ => SaveTenant());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        ToggleTenantActiveCommand = new RelayCommand(param => ToggleTenantActive(param));
        DeleteTenantCommand = new RelayCommand(param => DeleteTenant(param));
        ConfirmDeleteTenantCommand = new RelayCommand(_ => ConfirmDeleteTenant());
        CancelDeleteTenantCommand = new RelayCommand(_ => CancelDeleteTenant());
    }

    public ObservableCollection<Tenant> Tenants { get; }

    public RelayCommand LoadTenantsCommand { get; }
    public RelayCommand NewTenantCommand { get; }
    public RelayCommand EditTenantCommand { get; }
    public RelayCommand SaveTenantCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand ToggleTenantActiveCommand { get; }
    public RelayCommand DeleteTenantCommand { get; }
    public RelayCommand ConfirmDeleteTenantCommand { get; }
    public RelayCommand CancelDeleteTenantCommand { get; }

    public Tenant? SelectedTenant
    {
        get => _selectedTenant;
        set
        {
            if (SetProperty(ref _selectedTenant, value))
            {
                IsConfirmingDeleteTenant = false;
                if (_selectedTenant != null) EditTenant();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditFullName
    {
        get => _editFullName;
        set => SetProperty(ref _editFullName, value);
    }

    public string? EditPhone
    {
        get => _editPhone;
        set => SetProperty(ref _editPhone, value);
    }

    public string? EditEmail
    {
        get => _editEmail;
        set => SetProperty(ref _editEmail, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public void LoadTenants(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();

        Tenants.Clear();
        foreach (var tenant in _db.Tenants.Where(t => t.PropertyId == propertyId).OrderBy(t => t.FullName))
        {
            Tenants.Add(tenant);
        }
    }



    private void StartNewTenant()
    {
        _editingTenant = null;
        EditFullName = string.Empty;
        EditPhone = null;
        EditEmail = null;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditTenant()
    {
        if (SelectedTenant == null)
            return;

        _editingTenant = SelectedTenant;
        EditFullName = _editingTenant.FullName;
        EditPhone = _editingTenant.Phone;
        EditEmail = _editingTenant.Email;
        EditNotes = _editingTenant.Notes;
        IsEditing = true;
    }

    private void SaveTenant()
    {
        if (string.IsNullOrWhiteSpace(EditFullName))
            return;

        if (_editingTenant == null)
        {
            var tenant = new Tenant
            {
                FullName = EditFullName.Trim(),
                Phone = EditPhone?.Trim(),
                Email = EditEmail?.Trim(),
                Notes = EditNotes?.Trim(),
                PropertyId = _currentPropertyId,
                IsActive = true
            };
            _db.Tenants.Add(tenant);
        }
        else
        {
            _editingTenant.FullName = EditFullName.Trim();
            _editingTenant.Phone = EditPhone?.Trim();
            _editingTenant.Email = EditEmail?.Trim();
            _editingTenant.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadTenants(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingTenant = null;
        EditFullName = string.Empty;
        EditPhone = null;
        EditEmail = null;
        EditNotes = null;
        IsEditing = false;
    }

    private void ToggleTenantActive(object? parameter)
    {
        if (parameter is Tenant tenant)
        {
            tenant.IsActive = !tenant.IsActive;
            _db.SaveChanges();
            LoadTenants(_currentPropertyId);
        }
    }

    private bool _isConfirmingDeleteTenant;
    public bool IsConfirmingDeleteTenant
    {
        get => _isConfirmingDeleteTenant;
        set => SetProperty(ref _isConfirmingDeleteTenant, value);
    }

    private Tenant? _tenantToDelete;

    private void DeleteTenant(object? param)
    {
        if (param is Tenant tenant)
        {
            _tenantToDelete = tenant;
            IsConfirmingDeleteTenant = true;
        }
    }

    private void ConfirmDeleteTenant()
    {
        if (_tenantToDelete != null)
        {
            _db.Tenants.Remove(_tenantToDelete);
            _db.SaveChanges();
            
            if (SelectedTenant?.Id == _tenantToDelete.Id)
            {
                SelectedTenant = null;
            }
            
            _tenantToDelete = null;
            IsConfirmingDeleteTenant = false;
            LoadTenants(_currentPropertyId);
        }
    }

    private void CancelDeleteTenant()
    {
        _tenantToDelete = null;
        IsConfirmingDeleteTenant = false;
    }
}
