using System.Collections.ObjectModel;
using System.Linq;
using TenantManager.App.Data;
using TenantManager.App.Domain;

namespace TenantManager.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppDbContext _db;
    private Property? _selectedProperty;

    public string AppVersion 
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        }
    }

    public MainViewModel()
    {
        _db = new AppDbContext();
        Properties = new ObservableCollection<Property>();

        RoomList = new RoomListViewModel();
        TenantList = new TenantListViewModel();
        ContractList = new ContractListViewModel();
        PaymentList = new MonthlyPaymentListViewModel();
        ExpenseList = new ExpenseInvoiceListViewModel();
        Dashboard = new DashboardViewModel();
        Settings = new SettingsViewModel();
        PropertyList = new PropertyListViewModel(LoadProperties);

        LoadProperties();
    }

    public ObservableCollection<Property> Properties { get; }

    public Property? SelectedProperty
    {
        get => _selectedProperty;
        set
        {
            if (SetProperty(ref _selectedProperty, value))
            {
                RefreshAll();
            }
        }
    }

    public RoomListViewModel RoomList { get; }
    public TenantListViewModel TenantList { get; }
    public ContractListViewModel ContractList { get; }
    public MonthlyPaymentListViewModel PaymentList { get; }
    public ExpenseInvoiceListViewModel ExpenseList { get; }
    public DashboardViewModel Dashboard { get; }
    public SettingsViewModel Settings { get; }
    public PropertyListViewModel PropertyList { get; }

    public void RefreshAll()
    {
        if (SelectedProperty == null) return;

        RoomList.LoadRooms(SelectedProperty.Id);
        TenantList.LoadTenants(SelectedProperty.Id);
        ContractList.LoadContracts(SelectedProperty.Id);
        PaymentList.LoadPayments(SelectedProperty.Id);
        ExpenseList.LoadInvoices(SelectedProperty.Id);
        Dashboard.Refresh(SelectedProperty.Id);
    }

    private void LoadProperties()
    {
        _db.ChangeTracker.Clear();
        
        if (!_db.Properties.Any())
        {
            _db.Properties.Add(new Property { Name = "Vivienda Principal" });
            _db.SaveChanges();
        }

        var oldSelectedId = SelectedProperty?.Id;

        Properties.Clear();
        foreach (var prop in _db.Properties.Where(p => p.IsActive).OrderBy(p => p.Name))
        {
            Properties.Add(prop);
        }

        if (oldSelectedId != null)
        {
            SelectedProperty = Properties.FirstOrDefault(p => p.Id == oldSelectedId) ?? Properties.FirstOrDefault();
        }
        else if (Properties.Any())
        {
            SelectedProperty = Properties.First();
        }
    }
}
