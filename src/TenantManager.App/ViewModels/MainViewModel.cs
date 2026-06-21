namespace TenantManager.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        RoomList = new RoomListViewModel();
        TenantList = new TenantListViewModel();
    }

    public RoomListViewModel RoomList { get; }
    public TenantListViewModel TenantList { get; }
}
