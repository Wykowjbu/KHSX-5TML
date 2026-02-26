using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using KHSX.Models;
using KHSX.ViewModels;

namespace KHSX
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ProductBlock_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element)
            {
                if (element.DataContext is ProductBlock block)
                {
                    // Đóng gói data để kéo thả
                    DataObject data = new DataObject("ProductBlock", block);
                    DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
                }
            }
        }

        private void DayCell_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("ProductBlock"))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DayCell_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ProductBlock"))
            {
                var block = e.Data.GetData("ProductBlock") as ProductBlock;
                if (block != null && sender is FrameworkElement element)
                {
                    if (element.Tag is DayCell targetDay)
                    {
                        var targetLine = FindParentLine(element);
                        if (targetLine != null)
                        {
                            var vm = this.DataContext as MainViewModel;
                            vm?.HandleDrop(block, targetDay, targetLine);
                        }
                    }
                }
            }
        }

        // Hàm helper để tìm ProductionLine chứa DayCell đang được drop
        private ProductionLine? FindParentLine(DependencyObject child)
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            while (parentObject != null)
            {
                if (parentObject is FrameworkElement fe && fe.DataContext is ProductionLine line)
                {
                    return line;
                }
                parentObject = VisualTreeHelper.GetParent(parentObject);
            }
            return null;
        }
    }

    // --- Converters ---

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

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class WeekendColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(220, 220, 220)); // Xám cho chủ nhật
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}