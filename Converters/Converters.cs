using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PDFWin.Converters;

public class BoolToPdfIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isPdf && isPdf)
        {
            return "\uEA90"; // PDF / Document icon
        }
        return "\uEB9F"; // Photo / Image icon
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Visible when value is null (thumbnail not loaded yet, show icon)
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
