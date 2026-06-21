using System;
using System.IO;

namespace TenantManager.App.Data;

public static class DatabasePath
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenantManager");

    public static string FullPath => Path.Combine(BaseDirectory, "tenantmanager.db");

    public static string ConnectionString => $"Data Source={FullPath}";
}
