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
                            vm?.SaveConfigurationCommand.Execute(null);
                        }
                    }
                }
            }
        }

        private void DayCell_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DayCell day && !day.IsWeekend)
            {
                ShowEditShiftDialog(day);
            }
        }

        private void LineName_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is ProductionLine line)
            {
                ShowEditLineNameDialog(line);
            }
        }

        private void ShowEditLineNameDialog(ProductionLine line)
        {
            var dialog = new Window
            {
                Title = "Chỉnh sửa tên Line",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // TextBox for line name
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
            namePanel.Children.Add(new TextBlock { Text = "Tên Line:", Width = 80, VerticalAlignment = VerticalAlignment.Center });
            var nameBox = new TextBox
            {
                Width = 200,
                Text = line.LineName
            };
            namePanel.Children.Add(nameBox);
            Grid.SetRow(namePanel, 0);
            grid.Children.Add(namePanel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 2);

            var saveButton = new Button
            {
                Content = "Lưu",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            saveButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    line.LineName = nameBox.Text;
                    var vm = this.DataContext as MainViewModel;
                    vm?.SaveConfigurationCommand.Execute(null);
                    dialog.DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Tên line không được để trống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            var cancelButton = new Button
            {
                Content = "Hủy",
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            nameBox.Focus();
            nameBox.SelectAll();
            dialog.ShowDialog();
        }

        private void ShowEditShiftDialog(DayCell day)
        {
            var dialog = new Window
            {
                Title = $"Chỉnh sửa ca làm việc - {day.Date:dd/MM/yyyy}",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Shift A
            var shiftAPanel = CreateShiftPanel("Ca A", day.ShiftA);
            Grid.SetRow(shiftAPanel, 0);
            grid.Children.Add(shiftAPanel);

            // Shift B
            var shiftBPanel = CreateShiftPanel("Ca B", day.ShiftB);
            Grid.SetRow(shiftBPanel, 2);
            grid.Children.Add(shiftBPanel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 4);

            var saveButton = new Button
            {
                Content = "Lưu",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            saveButton.Click += (s, e) =>
            {
                var vm = this.DataContext as MainViewModel;
                vm?.SaveConfigurationCommand.Execute(null);
                dialog.DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "Hủy",
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        private StackPanel CreateShiftPanel(string title, ShiftConfig shift)
        {
            var panel = new StackPanel();

            // Title
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(titleBlock);

            // Workers
            var workersPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            workersPanel.Children.Add(new TextBlock { Text = "Số người:", Width = 100, VerticalAlignment = VerticalAlignment.Center });
            var workersBox = new TextBox
            {
                Width = 100,
                Text = shift.Workers.ToString()
            };
            workersBox.LostFocus += (s, e) =>
            {
                if (int.TryParse(workersBox.Text, out int value) && value >= 0)
                    shift.Workers = value;
                else
                    workersBox.Text = shift.Workers.ToString(); // Reset nếu không hợp lệ
            };
            workersPanel.Children.Add(workersBox);
            panel.Children.Add(workersPanel);

            // Minutes
            var minutesPanel = new StackPanel { Orientation = Orientation.Horizontal };
            minutesPanel.Children.Add(new TextBlock { Text = "Số phút/người:", Width = 100, VerticalAlignment = VerticalAlignment.Center });
            var minutesBox = new TextBox
            {
                Width = 100,
                Text = shift.Minutes.ToString()
            };
            minutesBox.LostFocus += (s, e) =>
            {
                if (double.TryParse(minutesBox.Text, out double value) && value >= 0)
                    shift.Minutes = value;
                else
                    minutesBox.Text = shift.Minutes.ToString(); // Reset nếu không hợp lệ
            };
            minutesPanel.Children.Add(minutesBox);
            panel.Children.Add(minutesPanel);

            return panel;
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