using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KHSX.Converters
{
    public class CustomConfigBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool hasCustom && hasCustom)
                return new SolidColorBrush(Color.FromRgb(255, 248, 220)); // Màu vàng nhạt (cornsilk)
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
