using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TenantManager.App.ViewModels;

public class BooleanToActiveStatusConverter : IValueConverter
{
    public static readonly BooleanToActiveStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (Avalonia.Application.Current != null)
            {
                var key = b ? "ActiveBadge" : "InactiveBadge";
                if (Avalonia.Application.Current.TryGetResource(key, null, out var resource) && resource is string s)
                {
                    return s;
                }
            }
            return b ? "Active" : "Inactive";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
