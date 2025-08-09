using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SourceFlow.UI.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BooleanToVisibilityConverter : IValueConverter
{
    public static readonly InverseBooleanConverter InverseBooleanConverter = InverseBooleanConverter.Instance;
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}