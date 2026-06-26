using System;
using System.Collections.ObjectModel;
using System.Linq;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class PropertyListViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private readonly Action _onPropertiesChanged;
    
    private Property? _editingProperty;
    private Property? _selectedProperty;
    private bool _isEditing;
    private string _editName = string.Empty;
    private string? _editAddress;
    private string? _editCity;
    private string? _editPostalCode;
    private string? _editNotes;

    public PropertyListViewModel(Action onPropertiesChanged)
    {
        _db = new AppDbContext();
        _onPropertiesChanged = onPropertiesChanged;
        Properties = new ObservableCollection<Property>();

        LoadPropertiesCommand = new RelayCommand(_ => LoadProperties());
        NewPropertyCommand = new RelayCommand(_ => StartNewProperty());
        EditPropertyCommand = new RelayCommand(_ => EditProperty());
        SavePropertyCommand = new RelayCommand(_ => SaveProperty());
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        TogglePropertyActiveCommand = new RelayCommand(param => TogglePropertyActive(param));

        LoadProperties();
    }

    public ObservableCollection<Property> Properties { get; }

    public RelayCommand LoadPropertiesCommand { get; }
    public RelayCommand NewPropertyCommand { get; }
    public RelayCommand EditPropertyCommand { get; }
    public RelayCommand SavePropertyCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand TogglePropertyActiveCommand { get; }

    public Property? SelectedProperty
    {
        get => _selectedProperty;
        set
        {
            if (SetProperty(ref _selectedProperty, value))
            {
                if (_selectedProperty != null) EditProperty();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public string? EditAddress
    {
        get => _editAddress;
        set => SetProperty(ref _editAddress, value);
    }

    public string? EditCity
    {
        get => _editCity;
        set => SetProperty(ref _editCity, value);
    }

    public string? EditPostalCode
    {
        get => _editPostalCode;
        set => SetProperty(ref _editPostalCode, value);
    }

    public string? EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public void LoadProperties()
    {
        _db.ChangeTracker.Clear();
        Properties.Clear();
        foreach (var property in _db.Properties.OrderBy(p => p.Name))
        {
            Properties.Add(property);
        }
    }

    private void StartNewProperty()
    {
        _editingProperty = null;
        EditName = string.Empty;
        EditAddress = null;
        EditCity = null;
        EditPostalCode = null;
        EditNotes = null;
        IsEditing = true;
    }

    private void EditProperty()
    {
        if (SelectedProperty == null)
            return;

        _editingProperty = SelectedProperty;
        EditName = _editingProperty.Name;
        EditAddress = _editingProperty.Address;
        EditCity = _editingProperty.City;
        EditPostalCode = _editingProperty.PostalCode;
        EditNotes = _editingProperty.Notes;
        IsEditing = true;
    }

    private void SaveProperty()
    {
        if (string.IsNullOrWhiteSpace(EditName))
            return;

        if (_editingProperty == null)
        {
            var property = new Property
            {
                Name = EditName.Trim(),
                Address = EditAddress?.Trim(),
                City = EditCity?.Trim(),
                PostalCode = EditPostalCode?.Trim(),
                Notes = EditNotes?.Trim(),
                IsActive = true
            };
            _db.Properties.Add(property);
        }
        else
        {
            _editingProperty.Name = EditName.Trim();
            _editingProperty.Address = EditAddress?.Trim();
            _editingProperty.City = EditCity?.Trim();
            _editingProperty.PostalCode = EditPostalCode?.Trim();
            _editingProperty.Notes = EditNotes?.Trim();
        }

        _db.SaveChanges();
        LoadProperties();
        CancelEdit();
        _onPropertiesChanged();
    }

    private void CancelEdit()
    {
        _editingProperty = null;
        EditName = string.Empty;
        EditAddress = null;
        EditCity = null;
        EditPostalCode = null;
        EditNotes = null;
        IsEditing = false;
    }

    private void TogglePropertyActive(object? parameter)
    {
        if (parameter is Property property)
        {
            property.IsActive = !property.IsActive;
            _db.SaveChanges();
            LoadProperties();
            CancelEdit();
            _onPropertiesChanged();
        }
    }
}
