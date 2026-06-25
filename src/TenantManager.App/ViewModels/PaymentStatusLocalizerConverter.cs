using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TenantManager.App.ViewModels;

public class PaymentStatusLocalizerConverter : IValueConverter
{
    public static readonly PaymentStatusLocalizerConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            if (Avalonia.Application.Current != null)
            {
                var key = status + "Status";
                if (Avalonia.Application.Current.TryGetResource(key, null, out var resource) && resource is string s)
                {
                    return s;
                }
            }
            return status;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
