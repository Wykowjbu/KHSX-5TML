using System;
using System.Globalization;
using System.Windows.Data;

namespace KHSX.Converters
{
    // Converter để chuyển đổi tỷ lệ % thành chiều rộng thực tế dựa trên ActualWidth của parent
    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && 
                values[0] is double percentage && 
                values[1] is double containerWidth &&
                containerWidth > 0)
            {
                // Trừ đi padding và margin
                double availableWidth = containerWidth - 40; // 20px padding mỗi bên
                double width = availableWidth * percentage;
                return Math.Max(60, Math.Min(width, availableWidth)); // Min 60, max = available
            }
            return 100.0; // Default width
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) 
            => throw new NotImplementedException();
    }
}
