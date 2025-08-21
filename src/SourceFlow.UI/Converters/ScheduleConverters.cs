using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SourceFlow.Core.Enums;

namespace SourceFlow.UI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScheduleStatus status)
        {
            return status switch
            {
                ScheduleStatus.Active => Colors.Green,
                ScheduleStatus.Inactive => Colors.Gray,
                ScheduleStatus.Paused => Colors.Orange,
                ScheduleStatus.Error => Colors.Red,
                ScheduleStatus.Completed => Colors.Blue,
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}