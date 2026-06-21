namespace TenantManager.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        RoomList = new RoomListViewModel();
        TenantList = new TenantListViewModel();
        ContractList = new ContractListViewModel();
    }

    public RoomListViewModel RoomList { get; }
    public TenantListViewModel TenantList { get; }
    public ContractListViewModel ContractList { get; }
}
