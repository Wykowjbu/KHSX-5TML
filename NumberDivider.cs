using System;
using System.Globalization;
using System.Windows.Data;

namespace KHSX
{
    public class NumberDivider : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double totalLength)
            {
                double divisor = 2; // Default to dividing by 2
                
                if (parameter != null && double.TryParse(parameter.ToString(), out double parsedParam))
                {
                    divisor = parsedParam;
                }

                // Trừ đi một chút lề (margin) để các block không bị sát vào viền quá hoặc rớt dòng sớm
                return (totalLength / divisor) - 4; 
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
