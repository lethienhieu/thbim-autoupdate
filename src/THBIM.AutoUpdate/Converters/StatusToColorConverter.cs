using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using THBIM.AutoUpdate.Models;

namespace THBIM.AutoUpdate.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AddinStatus status) return Brushes.Gray;
        return status switch
        {
            AddinStatus.UpToDate => new SolidColorBrush(Color.FromRgb(52, 199, 89)),
            AddinStatus.UpdateAvailable => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            AddinStatus.Updating => new SolidColorBrush(Color.FromRgb(255, 159, 10)),
            AddinStatus.Error => new SolidColorBrush(Color.FromRgb(255, 69, 58)),
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AddinStatus status) return Brushes.Transparent;
        return status switch
        {
            AddinStatus.UpToDate => new SolidColorBrush(Color.FromArgb(30, 52, 199, 89)),
            AddinStatus.UpdateAvailable => new SolidColorBrush(Color.FromArgb(30, 255, 215, 0)),
            AddinStatus.Updating => new SolidColorBrush(Color.FromArgb(30, 255, 159, 10)),
            AddinStatus.Error => new SolidColorBrush(Color.FromArgb(30, 255, 69, 58)),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AddinStatus status) return "Unknown";
        return status switch
        {
            AddinStatus.UpToDate => "LATEST",
            AddinStatus.UpdateAvailable => "UPDATE",
            AddinStatus.Updating => "UPDATING",
            AddinStatus.Error => "ERROR",
            _ => "UNKNOWN"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AddinStatus status) return new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        return status switch
        {
            AddinStatus.UpToDate => new SolidColorBrush(Color.FromArgb(50, 52, 199, 89)),
            AddinStatus.UpdateAvailable => new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            AddinStatus.Updating => new SolidColorBrush(Color.FromArgb(75, 255, 159, 10)),
            AddinStatus.Error => new SolidColorBrush(Color.FromArgb(50, 255, 69, 58)),
            _ => new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
