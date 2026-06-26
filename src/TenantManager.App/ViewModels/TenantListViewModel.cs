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
    private decimal _editDepositAmount;
    private string? _editNotes;
    private Room? _editSelectedRoom;
    private int _currentPropertyId;

    public TenantListViewModel()
    {
        _db = new AppDbContext();
        Tenants = new ObservableCollection<Tenant>();
        AvailableRooms = new ObservableCollection<Room>();

        LoadTenantsCommand = new RelayCommand(_ => LoadTenants(_currentPropertyId));
        NewTenantCommand = new RelayCommand(_ => StartNewTenant());
        EditTenantCommand = new RelayCommand(_ => EditTenant());
        SaveTenantCommand = new RelayCommand(_ => SaveTenant());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        ToggleTenantActiveCommand = new RelayCommand(param => ToggleTenantActive(param));
    }

    public ObservableCollection<Tenant> Tenants { get; }
    public ObservableCollection<Room> AvailableRooms { get; }

    public RelayCommand LoadTenantsCommand { get; }
    public RelayCommand NewTenantCommand { get; }
    public RelayCommand EditTenantCommand { get; }
    public RelayCommand SaveTenantCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand ToggleTenantActiveCommand { get; }

    public Tenant? SelectedTenant
    {
        get => _selectedTenant;
        set
        {
            if (SetProperty(ref _selectedTenant, value))
            {
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

    public decimal EditDepositAmount
    {
        get => _editDepositAmount;
        set => SetProperty(ref _editDepositAmount, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public Room? EditSelectedRoom
    {
        get => _editSelectedRoom;
        set => SetProperty(ref _editSelectedRoom, value);
    }

    public void LoadTenants(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();
        LoadAvailableRooms();

        Tenants.Clear();
        foreach (var tenant in _db.Tenants.Where(t => t.PropertyId == propertyId).OrderBy(t => t.FullName))
        {
            Tenants.Add(tenant);
        }
    }

    private void LoadAvailableRooms()
    {
        AvailableRooms.Clear();
        AvailableRooms.Add(new Room { Id = 0, Name = "(None)" });
        foreach (var room in _db.Rooms.Where(r => r.IsActive && r.PropertyId == _currentPropertyId).OrderBy(r => r.Name))
        {
            AvailableRooms.Add(room);
        }
    }

    private void StartNewTenant()
    {
        _editingTenant = null;
        EditFullName = string.Empty;
        EditPhone = null;
        EditEmail = null;
        EditDepositAmount = 0;
        EditNotes = null;
        EditSelectedRoom = AvailableRooms.FirstOrDefault();
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
        EditDepositAmount = _editingTenant.DepositAmount;
        EditNotes = _editingTenant.Notes;
        EditSelectedRoom = _editingTenant.RoomId is int roomId
            ? AvailableRooms.FirstOrDefault(r => r.Id == roomId)
            : AvailableRooms.FirstOrDefault();
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
                DepositAmount = EditDepositAmount,
                Notes = EditNotes?.Trim(),
                PropertyId = _currentPropertyId,
                IsActive = true,
                RoomId = EditSelectedRoom?.Id > 0 ? EditSelectedRoom.Id : null
            };
            _db.Tenants.Add(tenant);
        }
        else
        {
            _editingTenant.FullName = EditFullName.Trim();
            _editingTenant.Phone = EditPhone?.Trim();
            _editingTenant.Email = EditEmail?.Trim();
            _editingTenant.DepositAmount = EditDepositAmount;
            _editingTenant.Notes = EditNotes?.Trim();
            _editingTenant.RoomId = EditSelectedRoom?.Id > 0 ? EditSelectedRoom.Id : null;
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
        EditDepositAmount = 0;
        EditNotes = null;
        EditSelectedRoom = null;
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
}
