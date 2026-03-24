using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KHSX.Converters
{
    /// <summary>
    /// Converter để tăng độ dày border cho block vượt deadline
    /// </summary>
    public class BoolToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new Thickness(3); // Viền dày hơn cho block vượt deadline
            return new Thickness(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
