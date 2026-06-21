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
    private DateTimeOffset? _editMoveInDate;
    private DateTimeOffset? _editMoveOutDate;
    private decimal _editDepositAmount;
    private string? _editNotes;
    private Room? _editSelectedRoom;

    public TenantListViewModel()
    {
        _db = new AppDbContext();
        Tenants = new ObservableCollection<Tenant>();
        AvailableRooms = new ObservableCollection<Room>();

        LoadTenantsCommand = new RelayCommand(_ => LoadTenants());
        NewTenantCommand = new RelayCommand(_ => StartNewTenant());
        EditTenantCommand = new RelayCommand(_ => EditTenant());
        SaveTenantCommand = new RelayCommand(_ => SaveTenant());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        DeactivateTenantCommand = new RelayCommand(_ => DeactivateTenant());

        LoadTenants();
    }

    public ObservableCollection<Tenant> Tenants { get; }
    public ObservableCollection<Room> AvailableRooms { get; }

    public RelayCommand LoadTenantsCommand { get; }
    public RelayCommand NewTenantCommand { get; }
    public RelayCommand EditTenantCommand { get; }
    public RelayCommand SaveTenantCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand DeactivateTenantCommand { get; }

    public Tenant? SelectedTenant
    {
        get => _selectedTenant;
        set => SetProperty(ref _selectedTenant, value);
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

    public DateTimeOffset? EditMoveInDate
    {
        get => _editMoveInDate;
        set => SetProperty(ref _editMoveInDate, value);
    }

    public DateTimeOffset? EditMoveOutDate
    {
        get => _editMoveOutDate;
        set => SetProperty(ref _editMoveOutDate, value);
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

    public void LoadTenants()
    {
        LoadAvailableRooms();

        Tenants.Clear();
        foreach (var tenant in _db.Tenants.OrderBy(t => t.FullName))
        {
            Tenants.Add(tenant);
        }
    }

    private void LoadAvailableRooms()
    {
        AvailableRooms.Clear();
        AvailableRooms.Add(new Room { Id = 0, Name = "(None)" });
        foreach (var room in _db.Rooms.Where(r => r.IsActive).OrderBy(r => r.Name))
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
        EditMoveInDate = null;
        EditMoveOutDate = null;
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
        EditMoveInDate = _editingTenant.MoveInDate != default
            ? new DateTimeOffset(_editingTenant.MoveInDate)
            : null;
        EditMoveOutDate = _editingTenant.MoveOutDate is DateTime mo
            ? new DateTimeOffset(mo)
            : null;
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
                MoveInDate = EditMoveInDate?.DateTime ?? DateTime.Today,
                MoveOutDate = EditMoveOutDate?.DateTime,
                DepositAmount = EditDepositAmount,
                Notes = EditNotes?.Trim(),
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
            _editingTenant.MoveInDate = EditMoveInDate?.DateTime ?? DateTime.Today;
            _editingTenant.MoveOutDate = EditMoveOutDate?.DateTime;
            _editingTenant.DepositAmount = EditDepositAmount;
            _editingTenant.Notes = EditNotes?.Trim();
            _editingTenant.RoomId = EditSelectedRoom?.Id > 0 ? EditSelectedRoom.Id : null;
        }

        _db.SaveChanges();
        LoadTenants();
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingTenant = null;
        EditFullName = string.Empty;
        EditPhone = null;
        EditEmail = null;
        EditMoveInDate = null;
        EditMoveOutDate = null;
        EditDepositAmount = 0;
        EditNotes = null;
        EditSelectedRoom = null;
        IsEditing = false;
    }

    private void DeactivateTenant()
    {
        if (SelectedTenant == null)
            return;

        SelectedTenant.IsActive = false;
        _db.SaveChanges();
        LoadTenants();
    }
}
