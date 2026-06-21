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

        LoadRoomsCommand = new RelayCommand(_ => LoadRooms());
        NewRoomCommand = new RelayCommand(_ => StartNewRoom());
        EditRoomCommand = new RelayCommand(_ => EditRoom());
        SaveRoomCommand = new RelayCommand(_ => SaveRoom());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        DeactivateRoomCommand = new RelayCommand(_ => DeactivateRoom());

        LoadRooms();
    }

    public ObservableCollection<Room> Rooms { get; }

    public RelayCommand LoadRoomsCommand { get; }
    public RelayCommand NewRoomCommand { get; }
    public RelayCommand EditRoomCommand { get; }
    public RelayCommand SaveRoomCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand DeactivateRoomCommand { get; }

    public Room? SelectedRoom
    {
        get => _selectedRoom;
        set => SetProperty(ref _selectedRoom, value);
    }

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
}
