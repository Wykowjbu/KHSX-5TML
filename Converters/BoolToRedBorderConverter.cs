using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KHSX.Converters
{
    public class BoolToRedBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Brushes.Red;
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
