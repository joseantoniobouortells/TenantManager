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
    private decimal _editBaseRent;
    private string? _editNotes;
    private bool _isEditing;
    private int _currentPropertyId;

    public RoomListViewModel()
    {
        _db = new AppDbContext();
        Rooms = new ObservableCollection<Room>();

        LoadRoomsCommand = new RelayCommand(_ => LoadRooms(_currentPropertyId));
        NewRoomCommand = new RelayCommand(_ => StartNewRoom());
        EditRoomCommand = new RelayCommand(_ => EditRoom());
        SaveRoomCommand = new RelayCommand(_ => SaveRoom());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        ToggleRoomActiveCommand = new RelayCommand(param => ToggleRoomActive(param));
        DeleteRoomCommand = new RelayCommand(param => DeleteRoom(param));
        ConfirmDeleteRoomCommand = new RelayCommand(_ => ConfirmDeleteRoom());
        CancelDeleteRoomCommand = new RelayCommand(_ => CancelDeleteRoom());
    }

    public ObservableCollection<Room> Rooms { get; }

    public RelayCommand LoadRoomsCommand { get; }
    public RelayCommand NewRoomCommand { get; }
    public RelayCommand EditRoomCommand { get; }
    public RelayCommand SaveRoomCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand ToggleRoomActiveCommand { get; }
    public RelayCommand DeleteRoomCommand { get; }
    public RelayCommand ConfirmDeleteRoomCommand { get; }
    public RelayCommand CancelDeleteRoomCommand { get; }



    public Room? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (SetProperty(ref _selectedRoom, value))
            {
                OnPropertyChanged(nameof(HasSelectedRoom));
                IsConfirmingDeleteRoom = false;
                if (_selectedRoom != null) EditRoom();
            }
        }
    }

    public bool HasSelectedRoom => SelectedRoom != null;

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public decimal EditBaseRent
    {
        get => _editBaseRent;
        set => SetProperty(ref _editBaseRent, value);
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



    public void LoadRooms(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();
        Rooms.Clear();
        foreach (var room in _db.Rooms.Where(r => r.PropertyId == propertyId).OrderBy(r => r.Name))
        {
            Rooms.Add(room);
        }
    }

    private void StartNewRoom()
    {
        _editingRoom = null;
        EditName = string.Empty;
        EditBaseRent = 0;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditRoom()
    {
        if (SelectedRoom == null)
            return;

        _editingRoom = SelectedRoom;
        EditName = _editingRoom.Name;
        EditBaseRent = _editingRoom.BaseRent;
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
                PropertyId = _currentPropertyId,
                Name = EditName.Trim(),
                BaseRent = EditBaseRent,
                Notes = EditNotes?.Trim(),
                IsActive = true
            };
            _db.Rooms.Add(room);
        }
        else
        {
            _editingRoom.Name = EditName.Trim();
            _editingRoom.BaseRent = EditBaseRent;
            _editingRoom.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadRooms(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingRoom = null;
        EditName = string.Empty;
        EditBaseRent = 0;
        EditNotes = null;
        IsEditing = false;
    }

    private void ToggleRoomActive(object? parameter)
    {
        if (parameter is Room room)
        {
            room.IsActive = !room.IsActive;
            _db.SaveChanges();
            LoadRooms(_currentPropertyId);
        }
    }

    private bool _isConfirmingDeleteRoom;
    public bool IsConfirmingDeleteRoom
    {
        get => _isConfirmingDeleteRoom;
        set => SetProperty(ref _isConfirmingDeleteRoom, value);
    }

    private Room? _roomToDelete;

    private void DeleteRoom(object? param)
    {
        if (param is Room room)
        {
            _roomToDelete = room;
            IsConfirmingDeleteRoom = true;
        }
    }

    private void ConfirmDeleteRoom()
    {
        if (_roomToDelete != null)
        {
            _db.Rooms.Remove(_roomToDelete);
            _db.SaveChanges();
            
            if (SelectedRoom?.Id == _roomToDelete.Id)
            {
                SelectedRoom = null;
            }
            
            _roomToDelete = null;
            IsConfirmingDeleteRoom = false;
            LoadRooms(_currentPropertyId);
        }
    }

    private void CancelDeleteRoom()
    {
        _roomToDelete = null;
        IsConfirmingDeleteRoom = false;
    }
}
