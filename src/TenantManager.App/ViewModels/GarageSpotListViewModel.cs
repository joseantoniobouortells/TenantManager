using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class GarageSpotListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private GarageSpot? _editingGarageSpot;
    private GarageSpot? _selectedGarageSpot;
    private string _editName = string.Empty;
    private decimal _editBaseRent;
    private string? _editNotes;
    private bool _isEditing;
    private int _currentPropertyId;

    public GarageSpotListViewModel()
    {
        _db = new AppDbContext();
        GarageSpots = new ObservableCollection<GarageSpot>();

        LoadGarageSpotsCommand = new RelayCommand(_ => LoadGarageSpots(_currentPropertyId));
        NewGarageSpotCommand = new RelayCommand(_ => StartNewGarageSpot());
        EditGarageSpotCommand = new RelayCommand(_ => EditGarageSpot());
        SaveGarageSpotCommand = new RelayCommand(_ => SaveGarageSpot());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        ToggleGarageSpotActiveCommand = new RelayCommand(param => ToggleGarageSpotActive(param));
        DeleteGarageSpotCommand = new RelayCommand(param => DeleteGarageSpot(param));
        ConfirmDeleteGarageSpotCommand = new RelayCommand(_ => ConfirmDeleteGarageSpot());
        CancelDeleteGarageSpotCommand = new RelayCommand(_ => CancelDeleteGarageSpot());
    }

    public ObservableCollection<GarageSpot> GarageSpots { get; }

    public RelayCommand LoadGarageSpotsCommand { get; }
    public RelayCommand NewGarageSpotCommand { get; }
    public RelayCommand EditGarageSpotCommand { get; }
    public RelayCommand SaveGarageSpotCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand ToggleGarageSpotActiveCommand { get; }
    public RelayCommand DeleteGarageSpotCommand { get; }
    public RelayCommand ConfirmDeleteGarageSpotCommand { get; }
    public RelayCommand CancelDeleteGarageSpotCommand { get; }

    public GarageSpot? SelectedGarageSpot
    {
        get => _selectedGarageSpot;
        set
        {
            if (SetProperty(ref _selectedGarageSpot, value))
            {
                OnPropertyChanged(nameof(HasSelectedGarageSpot));
                IsConfirmingDeleteGarageSpot = false;
                if (_selectedGarageSpot != null) EditGarageSpot();
            }
        }
    }

    public bool HasSelectedGarageSpot => SelectedGarageSpot != null;

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

    public void LoadGarageSpots(int propertyId)
    {
        _currentPropertyId = propertyId;
        if (_currentPropertyId == 0) return;

        _db.ChangeTracker.Clear();
        GarageSpots.Clear();
        foreach (var spot in _db.GarageSpots.Where(s => s.PropertyId == propertyId).OrderBy(s => s.Name))
        {
            GarageSpots.Add(spot);
        }
    }

    private void StartNewGarageSpot()
    {
        _editingGarageSpot = null;
        EditName = string.Empty;
        EditBaseRent = 0;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditGarageSpot()
    {
        if (SelectedGarageSpot == null)
            return;

        _editingGarageSpot = SelectedGarageSpot;
        EditName = _editingGarageSpot.Name;
        EditBaseRent = _editingGarageSpot.BaseRent;
        EditNotes = _editingGarageSpot.Notes;
        IsEditing = true;
    }

    private void SaveGarageSpot()
    {
        if (string.IsNullOrWhiteSpace(EditName))
            return;

        if (_editingGarageSpot == null)
        {
            var spot = new GarageSpot
            {
                PropertyId = _currentPropertyId,
                Name = EditName.Trim(),
                BaseRent = EditBaseRent,
                Notes = EditNotes?.Trim(),
                IsActive = true
            };
            _db.GarageSpots.Add(spot);
        }
        else
        {
            _editingGarageSpot.Name = EditName.Trim();
            _editingGarageSpot.BaseRent = EditBaseRent;
            _editingGarageSpot.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadGarageSpots(_currentPropertyId);
        CancelEdit();
    }

    private void CancelEdit()
    {
        _editingGarageSpot = null;
        EditName = string.Empty;
        EditBaseRent = 0;
        EditNotes = null;
        IsEditing = false;
    }

    private void ToggleGarageSpotActive(object? parameter)
    {
        if (parameter is GarageSpot spot)
        {
            spot.IsActive = !spot.IsActive;
            _db.SaveChanges();
            LoadGarageSpots(_currentPropertyId);
        }
    }

    private bool _isConfirmingDeleteGarageSpot;
    public bool IsConfirmingDeleteGarageSpot
    {
        get => _isConfirmingDeleteGarageSpot;
        set => SetProperty(ref _isConfirmingDeleteGarageSpot, value);
    }

    private GarageSpot? _garageSpotToDelete;

    private void DeleteGarageSpot(object? param)
    {
        if (param is GarageSpot spot)
        {
            _garageSpotToDelete = spot;
            IsConfirmingDeleteGarageSpot = true;
        }
    }

    private void ConfirmDeleteGarageSpot()
    {
        if (_garageSpotToDelete != null)
        {
            _db.GarageSpots.Remove(_garageSpotToDelete);
            _db.SaveChanges();
            
            if (SelectedGarageSpot?.Id == _garageSpotToDelete.Id)
            {
                SelectedGarageSpot = null;
            }
            
            _garageSpotToDelete = null;
            IsConfirmingDeleteGarageSpot = false;
            LoadGarageSpots(_currentPropertyId);
        }
    }

    private void CancelDeleteGarageSpot()
    {
        _garageSpotToDelete = null;
        IsConfirmingDeleteGarageSpot = false;
    }
}
