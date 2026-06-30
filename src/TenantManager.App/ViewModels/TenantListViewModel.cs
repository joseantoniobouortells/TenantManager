using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class TenantListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private List<Tenant> _allTenants = new();
    private string _searchQuery = string.Empty;
    private string _sortColumn = "Name";
    private bool _sortAscending = true;
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
        SortCommand = new RelayCommand(param => Sort(param as string));
        NewTenantCommand = new RelayCommand(_ => StartNewTenant());
        SaveTenantCommand = new RelayCommand(_ => SaveTenant());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        DeleteTenantCommand = new RelayCommand(param => DeleteTenant(param));
        ConfirmDeleteTenantCommand = new RelayCommand(_ => ConfirmDeleteTenant());
        CancelDeleteTenantCommand = new RelayCommand(_ => CancelDeleteTenant());
    }

    public ObservableCollection<Tenant> Tenants { get; }

    public RelayCommand LoadTenantsCommand { get; }
    public RelayCommand SortCommand { get; }
    public RelayCommand NewTenantCommand { get; }
    public RelayCommand SaveTenantCommand { get; }
    public RelayCommand CancelEditCommand { get; }
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

    public string NameSortIndicator => _sortColumn == "Name" ? (_sortAscending ? "▲" : "▼") : "";
    public string PhoneSortIndicator => _sortColumn == "Phone" ? (_sortAscending ? "▲" : "▼") : "";

    public void LoadTenants(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();

        _allTenants.Clear();
        foreach (var tenant in _db.Tenants.Where(t => t.PropertyId == propertyId).ToList())
        {
            _allTenants.Add(tenant);
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

        OnPropertyChanged(nameof(NameSortIndicator));
        OnPropertyChanged(nameof(PhoneSortIndicator));

        ApplyFiltersAndSort();
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = _allTenants.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(t => 
                (t.FullName?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.Phone?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.Email?.ToLowerInvariant().Contains(q) ?? false));
        }

        filtered = _sortColumn switch
        {
            "Name" => _sortAscending ? filtered.OrderBy(t => t.FullName) : filtered.OrderByDescending(t => t.FullName),
            "Phone" => _sortAscending ? filtered.OrderBy(t => t.Phone) : filtered.OrderByDescending(t => t.Phone),
            _ => filtered.OrderBy(t => t.FullName)
        };

        Tenants.Clear();
        foreach (var t in filtered)
        {
            Tenants.Add(t);
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
                PropertyId = _currentPropertyId
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
