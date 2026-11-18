using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HarrowStudio.Desktop;

public sealed class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? new SolidColorBrush(Color.FromRgb(167, 243, 107)) : new SolidColorBrush(Color.FromRgb(103, 115, 128));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
