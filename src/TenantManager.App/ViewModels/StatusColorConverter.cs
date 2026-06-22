using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace TenantManager.App.ViewModels;

public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? new SolidColorBrush(Color.Parse("#E6F4EA")) : new SolidColorBrush(Color.Parse("#FCE8E6"));
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusTextColorConverter : IValueConverter
{
    public static readonly StatusTextColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? new SolidColorBrush(Color.Parse("#137333")) : new SolidColorBrush(Color.Parse("#C5221F"));
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
