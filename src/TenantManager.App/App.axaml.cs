using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using TenantManager.App.Data;

namespace TenantManager.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppDbContext.DefaultConnectionString = DatabasePath.ConnectionString;
        SettingsPersistence.SettingsFilePath = DatabasePath.SettingsPath;

        var dbDir = System.IO.Path.GetDirectoryName(DatabasePath.FullPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            System.IO.Directory.CreateDirectory(dbDir);
        }

        using (var db = new AppDbContext())
        {
            db.Database.Migrate();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
