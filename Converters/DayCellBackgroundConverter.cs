using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KHSX.Converters
{
    public class DayCellBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2)
            {
                bool isDayOff = values[0] is bool w && w;
                bool hasCustomConfig = values[1] is bool c && c;

                if (isDayOff)
                {
                    // Ngày nghỉ với custom: vàng nhạt hơn xám
                    if (hasCustomConfig)
                        return new SolidColorBrush(Color.FromRgb(240, 230, 200));
                    // Ngày nghỉ bình thường: xám
                    return new SolidColorBrush(Color.FromRgb(220, 220, 220));
                }
                else
                {
                    // Ngày làm với custom: vàng nhạt
                    if (hasCustomConfig)
                        return new SolidColorBrush(Color.FromRgb(255, 248, 220));
                    // Ngày làm bình thường: trắng
                    return Brushes.White;
                }
            }
            return Brushes.White;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
