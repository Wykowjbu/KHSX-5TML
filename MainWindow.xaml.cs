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
                ShowEditLineDialog(line);
            }
        }

        private void LineName_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ProductionLine line)
            {
                ShowEditLineConfigDialog(line);
            }
        }

        private void ShowEditLineDialog(ProductionLine line)
        {
            var dialog = new Window
            {
                Title = $"Cấu hình {line.LineName}",
                Width = 500,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Line name section
            var nameTitle = new TextBlock
            {
                Text = "TÊN LINE",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            };
            mainPanel.Children.Add(nameTitle);

            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            namePanel.Children.Add(new TextBlock { Text = "Tên:", Width = 100, VerticalAlignment = VerticalAlignment.Center });
            var nameBox = new TextBox
            {
                Width = 300,
                Text = line.LineName,
                FontSize = 14
            };
            namePanel.Children.Add(nameBox);
            mainPanel.Children.Add(namePanel);

            // Info text
            var infoText = new TextBlock
            {
                Text = "CẤU HÌNH MẶC ĐỊNH CHO TẤT CẢ CÁC NGÀY TRONG LINE",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DarkBlue,
                Margin = new Thickness(0, 0, 0, 15),
                FontSize = 12
            };
            mainPanel.Children.Add(infoText);

            // Shift A
            var shiftAPanel = CreateShiftPanel("Ca A (Mặc định)", line.DefaultShiftA);
            mainPanel.Children.Add(shiftAPanel);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 15, 0, 15) });

            // Shift B
            var shiftBPanel = CreateShiftPanel("Ca B (Mặc định)", line.DefaultShiftB);
            mainPanel.Children.Add(shiftBPanel);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 15, 0, 15) });

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var applyButton = new Button
            {
                Content = "Áp dụng cho tất cả ngày",
                Width = 140,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White
            };
            applyButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    line.LineName = nameBox.Text;
                    line.ApplyDefaultShiftToAllDays();
                    var vm = this.DataContext as MainViewModel;
                    vm?.SaveConfigurationCommand.Execute(null);
                    MessageBox.Show($"Đã áp dụng cấu hình cho tất cả ngày trong {line.LineName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Tên line không được để trống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

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

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;
            nameBox.Focus();
            nameBox.SelectAll();
            dialog.ShowDialog();
        }

        private void ShowEditLineConfigDialog(ProductionLine line)
        {
            var dialog = new Window
            {
                Title = $"Cấu hình mặc định cho {line.LineName}",
                Width = 450,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Info text
            var infoText = new TextBlock
            {
                Text = "Cấu hình này sẽ áp dụng cho TẤT CẢ các ngày trong line (trừ các ngày đã custom riêng)",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DarkBlue,
                Margin = new Thickness(0, 0, 0, 20),
                FontStyle = FontStyles.Italic
            };
            mainPanel.Children.Add(infoText);

            // Shift A
            var shiftAPanel = CreateShiftPanel("Ca A (Mặc định)", line.DefaultShiftA);
            mainPanel.Children.Add(shiftAPanel);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 15, 0, 15) });

            // Shift B
            var shiftBPanel = CreateShiftPanel("Ca B (Mặc định)", line.DefaultShiftB);
            mainPanel.Children.Add(shiftBPanel);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 15, 0, 15) });

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var applyButton = new Button
            {
                Content = "Áp dụng cho tất cả",
                Width = 120,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            applyButton.Click += (s, e) =>
            {
                line.ApplyDefaultShiftToAllDays();
                var vm = this.DataContext as MainViewModel;
                vm?.SaveConfigurationCommand.Execute(null);
                MessageBox.Show($"Đã áp dụng cấu hình cho tất cả ngày trong {line.LineName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            };

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

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;
            dialog.ShowDialog();
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
            // Tìm line chứa day này
            ProductionLine parentLine = null;
            var vm = this.DataContext as MainViewModel;
            if (vm != null)
            {
                foreach (var line in vm.Lines)
                {
                    if (line.Days.Contains(day))
                    {
                        parentLine = line;
                        break;
                    }
                }
            }

            if (parentLine == null) return;

            var dialog = new Window
            {
                Title = $"Chỉnh sửa ca làm việc - {day.Date:dd/MM/yyyy}",
                Width = 450,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Info text
            var infoText = new TextBlock
            {
                Text = $"Chỉnh sửa riêng cho ngày này{(day.HasCustomConfig ? " (đang dùng cấu hình riêng)" : " (đang dùng cấu hình mặc định của line)")}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = day.HasCustomConfig ? Brushes.Orange : Brushes.DarkGreen,
                Margin = new Thickness(0, 0, 0, 15),
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Bold
            };
            mainPanel.Children.Add(infoText);

            // Shift A
            var shiftAPanel = CreateShiftPanel("Ca A", day.ShiftA);
            mainPanel.Children.Add(shiftAPanel);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 15, 0, 15) });

            // Shift B
            var shiftBPanel = CreateShiftPanel("Ca B", day.ShiftB);
            mainPanel.Children.Add(shiftBPanel);
            mainPanel.Children.Add(new Separator { Margin = new Thickness(0, 15, 0, 15) });

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Reset button (only show if has custom config)
            if (day.HasCustomConfig)
            {
                var resetButton = new Button
                {
                    Content = "Reset về mặc định",
                    Width = 120,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    Foreground = Brushes.White
                };
                resetButton.Click += (s, e) =>
                {
                    day.ShiftA.Workers = parentLine.DefaultShiftA.Workers;
                    day.ShiftA.Minutes = parentLine.DefaultShiftA.Minutes;
                    day.ShiftB.Workers = parentLine.DefaultShiftB.Workers;
                    day.ShiftB.Minutes = parentLine.DefaultShiftB.Minutes;
                    day.HasCustomConfig = false;
                    vm?.SaveConfigurationCommand.Execute(null);
                    MessageBox.Show("Đã reset về cấu hình mặc định của line", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.DialogResult = true;
                };
                buttonPanel.Children.Add(resetButton);
            }

            var saveButton = new Button
            {
                Content = "Lưu",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            saveButton.Click += (s, e) =>
            {
                // Chỉ đánh dấu custom nếu giá trị KHÁC với default của line
                bool isDifferent = 
                    day.ShiftA.Workers != parentLine.DefaultShiftA.Workers ||
                    day.ShiftA.Minutes != parentLine.DefaultShiftA.Minutes ||
                    day.ShiftB.Workers != parentLine.DefaultShiftB.Workers ||
                    day.ShiftB.Minutes != parentLine.DefaultShiftB.Minutes;

                day.HasCustomConfig = isDifferent;
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
            mainPanel.Children.Add(buttonPanel);

            dialog.Content = mainPanel;
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
                Text = shift.Workers.ToString("0.##") // Format với tối đa 2 chữ số thập phân
            };
            workersBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(workersBox.Text, out double value) && value >= 0)
                {
                    shift.Workers = value;
                }
            };
            workersBox.LostFocus += (s, e) =>
            {
                // Format text when losing focus
                workersBox.Text = shift.Workers.ToString("0.##");
            };
            workersPanel.Children.Add(workersBox);
            panel.Children.Add(workersPanel);

            // Minutes
            var minutesPanel = new StackPanel { Orientation = Orientation.Horizontal };
            minutesPanel.Children.Add(new TextBlock { Text = "Số phút/người:", Width = 100, VerticalAlignment = VerticalAlignment.Center });
            var minutesBox = new TextBox
            {
                Width = 100,
                Text = shift.Minutes.ToString("0.##")
            };
            minutesBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(minutesBox.Text, out double value) && value >= 0)
                {
                    shift.Minutes = value;
                }
            };
            minutesBox.LostFocus += (s, e) =>
            {
                // Format text when losing focus
                minutesBox.Text = shift.Minutes.ToString("0.##");
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

    public class DayCellBackgroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2)
            {
                bool isWeekend = values[0] is bool w && w;
                bool hasCustomConfig = values[1] is bool c && c;

                if (isWeekend)
                {
                    // Weekend với custom: vàng nhạt hơn xám
                    if (hasCustomConfig)
                        return new SolidColorBrush(Color.FromRgb(240, 230, 200));
                    // Weekend bình thường: xám
                    return new SolidColorBrush(Color.FromRgb(220, 220, 220));
                }
                else
                {
                    // Ngày thường với custom: vàng nhạt
                    if (hasCustomConfig)
                        return new SolidColorBrush(Color.FromRgb(255, 248, 220));
                    // Ngày thường bình thường: trắng
                    return Brushes.White;
                }
            }
            return Brushes.White;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

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