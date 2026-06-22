using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class RoomListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private Room? _editingRoom;
    private Room? _selectedRoom;
    private string _editName = string.Empty;
    private decimal _editMonthlyRent;
    private string? _editNotes;
    private bool _isEditing;

    public RoomListViewModel()
    {
        _db = new AppDbContext();
        Rooms = new ObservableCollection<Room>();
        RentPeriods = new ObservableCollection<RoomRentPeriod>();

        LoadRoomsCommand = new RelayCommand(_ => LoadRooms());
        NewRoomCommand = new RelayCommand(_ => StartNewRoom());
        EditRoomCommand = new RelayCommand(_ => EditRoom());
        SaveRoomCommand = new RelayCommand(_ => SaveRoom());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        DeactivateRoomCommand = new RelayCommand(_ => DeactivateRoom());
        ReactivateRoomCommand = new RelayCommand(_ => ReactivateRoom());

        NewRentPeriodCommand = new RelayCommand(_ => StartNewRentPeriod());
        EditRentPeriodCommand = new RelayCommand(_ => EditRentPeriod());
        SaveRentPeriodCommand = new RelayCommand(_ => SaveRentPeriod());
        CancelRentPeriodCommand = new RelayCommand(_ => CancelRentPeriod());
        DeleteRentPeriodCommand = new RelayCommand(_ => DeleteRentPeriod());

        LoadRooms();
    }

    public ObservableCollection<Room> Rooms { get; }

    public RelayCommand LoadRoomsCommand { get; }
    public RelayCommand NewRoomCommand { get; }
    public RelayCommand EditRoomCommand { get; }
    public RelayCommand SaveRoomCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand DeactivateRoomCommand { get; }
    public RelayCommand ReactivateRoomCommand { get; }

    public RelayCommand NewRentPeriodCommand { get; }
    public RelayCommand EditRentPeriodCommand { get; }
    public RelayCommand SaveRentPeriodCommand { get; }
    public RelayCommand CancelRentPeriodCommand { get; }
    public RelayCommand DeleteRentPeriodCommand { get; }

    public Room? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (SetProperty(ref _selectedRoom, value))
            {
                OnPropertyChanged(nameof(HasSelectedRoom));
                LoadRentPeriods();
                CancelRentPeriod();
            }
        }
    }

    public bool HasSelectedRoom => SelectedRoom != null;

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public decimal EditMonthlyRent
    {
        get => _editMonthlyRent;
        set => SetProperty(ref _editMonthlyRent, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public ObservableCollection<RoomRentPeriod> RentPeriods { get; }

    private RoomRentPeriod? _selectedRentPeriod;
    public RoomRentPeriod? SelectedRentPeriod
    {
        get => _selectedRentPeriod;
        set => SetProperty(ref _selectedRentPeriod, value);
    }

    private RoomRentPeriod? _editingRentPeriod;
    private bool _isEditingRentPeriod;
    public bool IsEditingRentPeriod
    {
        get => _isEditingRentPeriod;
        set => SetProperty(ref _isEditingRentPeriod, value);
    }

    private decimal _rentPeriodEditMonthlyRent;
    public decimal RentPeriodEditMonthlyRent
    {
        get => _rentPeriodEditMonthlyRent;
        set => SetProperty(ref _rentPeriodEditMonthlyRent, value);
    }

    private DateTimeOffset _rentPeriodEditStartDate = DateTimeOffset.Now;
    public DateTimeOffset RentPeriodEditStartDate
    {
        get => _rentPeriodEditStartDate;
        set => SetProperty(ref _rentPeriodEditStartDate, value);
    }

    private DateTimeOffset? _rentPeriodEditEndDate;
    public DateTimeOffset? RentPeriodEditEndDate
    {
        get => _rentPeriodEditEndDate;
        set => SetProperty(ref _rentPeriodEditEndDate, value);
    }

    private string? _rentPeriodEditNotes;
    public string? RentPeriodEditNotes
    {
        get => _rentPeriodEditNotes;
        set => SetProperty(ref _rentPeriodEditNotes, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public void LoadRooms()
    {
        Rooms.Clear();
        foreach (var room in _db.Rooms.OrderBy(r => r.Name))
        {
            Rooms.Add(room);
        }
    }

    private void StartNewRoom()
    {
        _editingRoom = null;
        EditName = string.Empty;
        EditMonthlyRent = 0;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditRoom()
    {
        if (SelectedRoom == null)
            return;

        _editingRoom = SelectedRoom;
        EditName = _editingRoom.Name;
        EditMonthlyRent = _editingRoom.MonthlyRent;
        EditNotes = _editingRoom.Notes;
        IsEditing = true;
    }

    private void SaveRoom()
    {
        if (string.IsNullOrWhiteSpace(EditName))
            return;

        if (_editingRoom == null)
        {
            var room = new Room
            {
                Name = EditName.Trim(),
                MonthlyRent = EditMonthlyRent,
                Notes = EditNotes?.Trim(),
                IsActive = true
            };
            _db.Rooms.Add(room);
        }
        else
        {
            _editingRoom.Name = EditName.Trim();
            _editingRoom.MonthlyRent = EditMonthlyRent;
            _editingRoom.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadRooms();
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingRoom = null;
        EditName = string.Empty;
        EditMonthlyRent = 0;
        EditNotes = null;
        IsEditing = false;
    }

    private void DeactivateRoom()
    {
        if (SelectedRoom == null)
            return;

        SelectedRoom.IsActive = false;
        _db.SaveChanges();
        LoadRooms();
    }

    private void ReactivateRoom()
    {
        if (SelectedRoom == null)
            return;

        SelectedRoom.IsActive = true;
        _db.SaveChanges();
        LoadRooms();
    }

    private void LoadRentPeriods()
    {
        RentPeriods.Clear();
        if (SelectedRoom != null)
        {
            var periods = _db.RoomRentPeriods
                .Where(rp => rp.RoomId == SelectedRoom.Id)
                .OrderByDescending(rp => rp.StartDate)
                .ToList();

            foreach (var period in periods)
            {
                RentPeriods.Add(period);
            }
        }
    }

    private void StartNewRentPeriod()
    {
        if (SelectedRoom == null) return;

        ErrorMessage = null;
        _editingRentPeriod = null;
        RentPeriodEditMonthlyRent = SelectedRoom.MonthlyRent;
        RentPeriodEditStartDate = DateTimeOffset.Now;
        RentPeriodEditEndDate = null;
        RentPeriodEditNotes = null;
        IsEditingRentPeriod = true;
    }

    private void EditRentPeriod()
    {
        if (SelectedRentPeriod == null) return;

        ErrorMessage = null;
        _editingRentPeriod = SelectedRentPeriod;
        RentPeriodEditMonthlyRent = _editingRentPeriod.MonthlyRent;
        RentPeriodEditStartDate = _editingRentPeriod.StartDate;
        RentPeriodEditEndDate = _editingRentPeriod.EndDate.HasValue ? new DateTimeOffset(_editingRentPeriod.EndDate.Value) : null;
        RentPeriodEditNotes = _editingRentPeriod.Notes;
        IsEditingRentPeriod = true;
    }

    private void SaveRentPeriod()
    {
        if (SelectedRoom == null) return;

        if (_editingRentPeriod == null)
        {
            var newPeriod = new RoomRentPeriod
            {
                RoomId = SelectedRoom.Id,
                MonthlyRent = RentPeriodEditMonthlyRent,
                StartDate = RentPeriodEditStartDate.Date,
                EndDate = RentPeriodEditEndDate?.Date,
                Notes = RentPeriodEditNotes?.Trim()
            };
            _db.RoomRentPeriods.Add(newPeriod);
        }
        else
        {
            _editingRentPeriod.MonthlyRent = RentPeriodEditMonthlyRent;
            _editingRentPeriod.StartDate = RentPeriodEditStartDate.Date;
            _editingRentPeriod.EndDate = RentPeriodEditEndDate?.Date;
            _editingRentPeriod.Notes = RentPeriodEditNotes?.Trim();
        }

        try
        {
            _db.SaveChanges();
            LoadRentPeriods();
            CancelRentPeriod();
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private void CancelRentPeriod()
    {
        ErrorMessage = null;
        _editingRentPeriod = null;
        IsEditingRentPeriod = false;
    }

    private void DeleteRentPeriod()
    {
        if (SelectedRentPeriod == null) return;

        _db.RoomRentPeriods.Remove(SelectedRentPeriod);
        _db.SaveChanges();
        LoadRentPeriods();
    }
}
