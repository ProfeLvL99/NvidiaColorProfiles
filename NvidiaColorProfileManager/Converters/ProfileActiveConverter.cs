using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NvidiaColorProfileManager.Converters;

public class ProfileActiveConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is int cardId && values[1] is int selectedId)
            return cardId == selectedId ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
