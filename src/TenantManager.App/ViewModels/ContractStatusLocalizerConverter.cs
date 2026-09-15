using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TenantManager.App.ViewModels;

public class ContractStatusLocalizerConverter : IValueConverter
{
    public static readonly ContractStatusLocalizerConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ContractStatusFilter filter)
        {
            var key = "Filter" + filter + "Contracts";
            if (Avalonia.Application.Current != null &&
                Avalonia.Application.Current.TryGetResource(key, null, out var resource) &&
                resource is string s)
            {
                return s;
            }

            return filter switch
            {
                ContractStatusFilter.Active => "Activos",
                ContractStatusFilter.Expired => "Finalizados",
                ContractStatusFilter.All => "Todos",
                _ => filter.ToString()
            };
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
