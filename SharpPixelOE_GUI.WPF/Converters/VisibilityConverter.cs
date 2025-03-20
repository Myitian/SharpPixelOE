using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Myitian.Converters;

public class VisibilityConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is null or false ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
