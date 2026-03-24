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
            
            if (this.DataContext is MainViewModel vm)
            {
                vm.RequestDeadlineDialog += ShowDeadlineConfigDialog;
                vm.RequestConfigGroupsDialog += ShowConfigGroupsDialog;
                vm.RequestProductOrderSettingsDialog += ShowProductOrderSettingsDialog;
                vm.RequestExportOrderDialog += ShowExportOrderDialog;
            }
        }

        private void ShowProductOrderSettingsDialog()
        {
            var dialog = new Views.ProductOrderSettingsDialog
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? ShowExportOrderDialog(
            System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? lineBlockData)
        {
            if (lineBlockData == null) return null;

            var dialog = new Views.ExportOrderDialog(lineBlockData)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.IsConfirmed)
            {
                // Merge: dialog chỉ trả về line có >1 block, cần thêm line có 1 block
                var result = dialog.ResultBlockOrder ?? new();
                foreach (var kvp in lineBlockData)
                {
                    if (!result.ContainsKey(kvp.Key))
                        result[kvp.Key] = kvp.Value;
                }
                return result;
            }

            return null; // User hủy
        }

        private void ShowConfigGroupsDialog()
        {
            var groups = Services.JsonStorage.Load<System.Collections.Generic.List<ProductGroupData>>("productGroups.json");
            if (groups == null || groups.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu Marketing. Vui lòng Import Marketing (Bước 1) trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Window
            {
                Title = "Cấu Hình Product Groups",
                Width = 600,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                MinHeight = 300
            };

            var rootPanel = new DockPanel { Margin = new Thickness(20) };

            // Header info - docked top
            var topPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 5) };
            DockPanel.SetDock(topPanel, Dock.Top);

            var infoText = new TextBlock
            {
                Text = "Cập nhật Tên hiển thị và Production Group (Gr.xxx) mặc định cho mỗi nhóm sản phẩm.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            topPanel.Children.Add(infoText);

            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            
            var h1 = new TextBlock { Text = "Build Group", FontWeight = FontWeights.Bold };
            var h2 = new TextBlock { Text = "fuction", FontWeight = FontWeights.Bold };
            var h3 = new TextBlock { Text = "Gr.xxx Mặc Định", FontWeight = FontWeights.Bold };
            Grid.SetColumn(h1, 0); Grid.SetColumn(h2, 1); Grid.SetColumn(h3, 2);
            headerRow.Children.Add(h1); headerRow.Children.Add(h2); headerRow.Children.Add(h3);
            topPanel.Children.Add(headerRow);
            rootPanel.Children.Add(topPanel);

            var gridPanel = new Grid();
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });

            var nameBoxes = new System.Collections.Generic.Dictionary<string, TextBox>();
            var groupBoxes = new System.Collections.Generic.Dictionary<string, ComboBox>();

            // Lấy danh sách các Gr.xxx có thể có từ Products.json
            var products = Services.JsonStorage.Load<System.Collections.Generic.List<ProductData>>("products.json");
            var availableGr = new System.Collections.Generic.List<string> { "" }; // Option rỗng
            if (products != null)
            {
                var allGrs = products.SelectMany(p => p.QuantitiesByGroup.Keys).Distinct().OrderBy(g => g);
                availableGr.AddRange(allGrs);
            }

            int rowIdx = 0;
            foreach (var group in groups)
            {
                gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var idLabel = new TextBlock { Text = group.GroupId, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 10, 5) };
                Grid.SetRow(idLabel, rowIdx);
                Grid.SetColumn(idLabel, 0);
                gridPanel.Children.Add(idLabel);

                var nameBox = new TextBox { Text = group.Name, Margin = new Thickness(0, 5, 10, 5), Padding = new Thickness(2) };
                nameBoxes[group.GroupId] = nameBox;
                Grid.SetRow(nameBox, rowIdx);
                Grid.SetColumn(nameBox, 1);
                gridPanel.Children.Add(nameBox);

                var defaultGroupBox = new ComboBox 
                { 
                    Margin = new Thickness(0, 5, 0, 5), 
                    Padding = new Thickness(2),
                    ItemsSource = availableGr
                };
                
                // Mặc định chọn Gr lớn nhất nếu chưa có
                if (string.IsNullOrEmpty(group.ProductionGroup) && products != null)
                {
                    var groupProducts = products.Where(p => p.GroupId == group.GroupId).ToList();
                    string maxGr = "";
                    
                    var allGroupKeys = groupProducts.SelectMany(p => p.QuantitiesByGroup.Keys)
                                                    .Where(k => !string.IsNullOrEmpty(k))
                                                    .Distinct()
                                                    .ToList();
                    if (allGroupKeys.Any())
                    {
                        // Sắp xếp giảm dần theo chuỗi (VD: Gr.289 đứng trước Gr.284)
                        maxGr = allGroupKeys.OrderByDescending(k => k).First();
                    }
                    
                    defaultGroupBox.SelectedItem = maxGr;
                }
                else
                {
                    defaultGroupBox.SelectedItem = group.ProductionGroup;
                }
                
                groupBoxes[group.GroupId] = defaultGroupBox;
                Grid.SetRow(defaultGroupBox, rowIdx);
                Grid.SetColumn(defaultGroupBox, 2);
                gridPanel.Children.Add(defaultGroupBox);

                rowIdx++;
            }

            var scrollView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scrollView.Content = gridPanel;

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            
            var saveBtn = new Button { Content = "Lưu Cập Nhật", Width = 100, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)), Foreground = Brushes.White };
            saveBtn.Click += (s, e) =>
            {
                foreach (var group in groups)
                {
                    if (nameBoxes.TryGetValue(group.GroupId, out var nb)) group.Name = nb.Text;
                    if (groupBoxes.TryGetValue(group.GroupId, out var cb)) 
                    {
                        group.ProductionGroup = cb.SelectedItem as string ?? "";
                    }
                }
                Services.JsonStorage.Save("productGroups.json", groups);
                MessageBox.Show("Đã lưu cấu hình product groups!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                dialog.DialogResult = true;
            };

            var cancelBtn = new Button { Content = "Hủy", Width = 80, Height = 30 };
            cancelBtn.Click += (s, e) => dialog.DialogResult = false;

            var globalGroupSelector = new ComboBox
            {
                Width = 100, Height = 30, Margin = new Thickness(0, 0, 10, 0),
                ItemsSource = availableGr,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            
            var initialMaxGr = availableGr.Where(g => !string.IsNullOrEmpty(g)).OrderByDescending(g => g).FirstOrDefault();
            if (!string.IsNullOrEmpty(initialMaxGr)) globalGroupSelector.SelectedItem = initialMaxGr;

            var setMaxGrBtn = new Button { Content = "Áp dụng cho tất cả", Width = 130, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)), Foreground = Brushes.White };
            setMaxGrBtn.Click += (s, e) =>
            {
                var selectedGr = globalGroupSelector.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedGr)) return;

                var confirmed = MessageBox.Show(
                    $"Bạn có chắc chắn muốn thiết lập mặc định '{selectedGr}' cho TẤT CẢ các dòng không?",
                    "Xác nhận thay đổi hàng loạt",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmed == MessageBoxResult.Yes)
                {
                    foreach (var cb in groupBoxes.Values)
                    {
                        cb.SelectedItem = selectedGr;
                    }
                }
            };

            buttonPanel.Children.Add(globalGroupSelector);
            buttonPanel.Children.Add(setMaxGrBtn);
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            rootPanel.Children.Add(buttonPanel);
            rootPanel.Children.Add(scrollView);

            dialog.Content = rootPanel;
            dialog.ShowDialog();
        }

        private void ShowDeadlineConfigDialog()
        {
            // Đọc dữ liệu Product groups để hiển thị
            var groups = Services.JsonStorage.Load<System.Collections.Generic.List<ProductGroupData>>("productGroups.json");
            if (groups == null || groups.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu Marketing. Vui lòng Import Marketing (Bước 1) trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Window
            {
                Title = "Thiết Lập Deadline Cho Group",
                Width = 420,
                Height = 500,
                MinHeight = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize
            };

            var rootPanel = new DockPanel { Margin = new Thickness(20) };

            // Top info
            var infoText = new TextBlock
            {
                Text = "Nhập ngày deadline cho từng Production Group. Nếu bỏ trống, hệ thống sẽ sử dụng Deadline Tổng.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(infoText, Dock.Top);
            rootPanel.Children.Add(infoText);

            var scrollView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var gridPanel = new Grid();
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            // Load existing deadlines
            var existingDeadlines = Services.JsonStorage.Load<System.Collections.Generic.List<DeadlineData>>("deadlines.json") 
                ?? new System.Collections.Generic.List<DeadlineData>();

            // Lấy danh sách Gr.xxx duy nhất từ nhóm và từ sản phẩm
            var products = Services.JsonStorage.Load<System.Collections.Generic.List<ProductData>>("products.json");
            var allGroupsFromProducts = products?.SelectMany(p => p.QuantitiesByGroup.Keys) ?? System.Linq.Enumerable.Empty<string>();

            var productionGroups = groups.Select(g => g.ProductionGroup)
                                          .Concat(allGroupsFromProducts)
                                          .Where(g => !string.IsNullOrWhiteSpace(g))
                                          .Distinct()
                                          .OrderBy(g => g)
                                          .ToList();

            var datePickers = new System.Collections.Generic.Dictionary<string, DatePicker>();

            int rowIdx = 0;
            foreach (var grp in productionGroups)
            {
                gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var groupLabel = new TextBlock { Text = grp, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 10, 5), FontWeight = FontWeights.Bold };
                Grid.SetRow(groupLabel, rowIdx);
                Grid.SetColumn(groupLabel, 0);
                gridPanel.Children.Add(groupLabel);

                var datePicker = new DatePicker { Margin = new Thickness(0, 5, 0, 5) };
                var existing = existingDeadlines.Find(d => d.GroupNumber == grp);
                if (existing != null)
                {
                    datePicker.SelectedDate = existing.Deadline;
                }
                datePickers[grp] = datePicker;
                Grid.SetRow(datePicker, rowIdx);
                Grid.SetColumn(datePicker, 1);
                gridPanel.Children.Add(datePicker);

                rowIdx++;
            }

            scrollView.Content = gridPanel;

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            
            var saveBtn = new Button { Content = "Lưu Deadlines", Width = 100, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)), Foreground = Brushes.White };
            saveBtn.Click += (s, e) =>
            {
                var newDeadlines = new System.Collections.Generic.List<DeadlineData>();
                foreach (var kvp in datePickers)
                {
                    if (kvp.Value.SelectedDate.HasValue)
                    {
                        newDeadlines.Add(new DeadlineData { GroupNumber = kvp.Key, Deadline = kvp.Value.SelectedDate.Value });
                    }
                }
                Services.JsonStorage.Save("deadlines.json", newDeadlines);
                MessageBox.Show("Đã lưu thiết lập deadline thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                dialog.DialogResult = true;
            };

            var cancelBtn = new Button { Content = "Hủy", Width = 80, Height = 30 };
            cancelBtn.Click += (s, e) => dialog.DialogResult = false;

            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            rootPanel.Children.Add(buttonPanel);
            rootPanel.Children.Add(scrollView);

            dialog.Content = rootPanel;
            dialog.ShowDialog();
            
            // Refresh lại các vạch đỏ deadline trên grid
            if (this.DataContext is MainViewModel vm)
            {
                vm.RefreshDeadlines();
            }
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

        private void UnassignedPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ProductBlock"))
            {
                var block = e.Data.GetData("ProductBlock") as ProductBlock;
                var vm = this.DataContext as MainViewModel;
                // Chỉ cho phép drop block từ grid (không phải từ unassigned)
                if (block != null && vm != null && !vm.UnassignedBlocks.Contains(block))
                {
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void UnassignedPanel_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ProductBlock"))
            {
                var block = e.Data.GetData("ProductBlock") as ProductBlock;
                var vm = this.DataContext as MainViewModel;
                if (block != null && vm != null && !vm.UnassignedBlocks.Contains(block))
                {
                    vm.HandleReturnToUnassigned(block);
                    vm.SaveConfigurationCommand.Execute(null);
                }
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
                        var targetRow = FindParentRow(element);
                        if (targetRow != null)
                        {
                            var vm = this.DataContext as MainViewModel;
                            vm?.HandleDrop(block, targetDay, targetRow);
                            vm?.SaveConfigurationCommand.Execute(null);
                        }
                    }
                }
            }
        }

        private void DayCell_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DayCell day)
            {
                ShowEditShiftDialog(day);
            }
        }

        private void LineName_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is ShiftRow row)
            {
                ShowEditRowDialog(row);
            }
        }

        private void DisplayIndex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                vm.SaveConfigurationCommand.Execute(null);
            }
        }

        private void DisplayIndex_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    // Update layout and trigger binding just in case
                    var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                    bindingExpression?.UpdateSource();
                }

                if (this.DataContext is MainViewModel vm)
                {
                    vm.SaveConfigurationCommand.Execute(null);
                }
                
                // Remove focus to trigger normal view
                Keyboard.ClearFocus();
            }
        }

        private void LineName_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is ShiftRow row)
            {
                ShowEditRowConfigDialog(row);
            }
        }

        private void ShowEditRowDialog(ShiftRow row)
        {
            var dialog = new Window
            {
                Title = $"Cấu hình {row.RowName}",
                Width = 500,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var rootPanel = new DockPanel { Margin = new Thickness(20) };

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            var applyButton = new Button
            {
                Content = "Áp dụng cho tất cả ngày",
                Width = 140,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White
            };
            var saveButton = new Button { Content = "Lưu", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Hủy", Width = 80, Height = 30 };
            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            rootPanel.Children.Add(buttonPanel);

            // Content
            var contentPanel = new StackPanel();

            var nameTitle = new TextBlock { Text = "TÊN CA LÀM VIỆC", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) };
            contentPanel.Children.Add(nameTitle);

            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            namePanel.Children.Add(new TextBlock { Text = "Tên:", Width = 100, VerticalAlignment = VerticalAlignment.Center });
            var nameBox = new TextBox { Width = 300, Text = row.RowName, FontSize = 14 };
            namePanel.Children.Add(nameBox);
            contentPanel.Children.Add(namePanel);

            var infoText = new TextBlock
            {
                Text = "CẤU HÌNH MẶC ĐỊNH CHO TẤT CẢ CÁC NGÀY TRONG CA LÀM VIỆC NÀY",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DarkBlue,
                Margin = new Thickness(0, 0, 0, 15),
                FontSize = 12
            };
            contentPanel.Children.Add(infoText);

            var configPanel = CreateShiftPanel("Cấu hình ca", row.DefaultConfig);
            contentPanel.Children.Add(configPanel);
            contentPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 0) });
            rootPanel.Children.Add(contentPanel);

            applyButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    row.RowName = nameBox.Text;
                    row.ApplyDefaultShiftToAllDays();
                    var vm = this.DataContext as MainViewModel;
                    vm?.SaveConfigurationCommand.Execute(null);
                    MessageBox.Show($"Đã áp dụng cấu hình cho tất cả ngày trong {row.RowName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.DialogResult = true;
                }
                else { MessageBox.Show("Tên ca không được để trống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            saveButton.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    row.RowName = nameBox.Text;
                    var vm = this.DataContext as MainViewModel;
                    vm?.SaveConfigurationCommand.Execute(null);
                    dialog.DialogResult = true;
                }
                else { MessageBox.Show("Tên ca không được để trống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            dialog.Content = rootPanel;
            nameBox.Focus();
            nameBox.SelectAll();
            dialog.ShowDialog();
        }

        private void ShowEditRowConfigDialog(ShiftRow row)
        {
            var dialog = new Window
            {
                Title = $"Cấu hình mặc định cho {row.RowName}",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var rootPanel = new DockPanel { Margin = new Thickness(20) };

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            var applyButton = new Button { Content = "Áp dụng cho tất cả", Width = 120, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var saveButton = new Button { Content = "Lưu", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Hủy", Width = 80, Height = 30 };
            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            rootPanel.Children.Add(buttonPanel);

            // Content
            var contentPanel = new StackPanel();

            var infoText = new TextBlock
            {
                Text = "Cấu hình này sẽ áp dụng cho TẤT CẢ các ngày trong ca (trừ các ngày đã custom riêng)",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DarkBlue,
                Margin = new Thickness(0, 0, 0, 15),
                FontStyle = FontStyles.Italic
            };
            contentPanel.Children.Add(infoText);

            var configPanel = CreateShiftPanel("Cấu hình ca", row.DefaultConfig);
            contentPanel.Children.Add(configPanel);
            contentPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 0) });
            rootPanel.Children.Add(contentPanel);

            applyButton.Click += (s, e) =>
            {
                row.ApplyDefaultShiftToAllDays();
                var vm = this.DataContext as MainViewModel;
                vm?.SaveConfigurationCommand.Execute(null);
                MessageBox.Show($"Đã áp dụng cấu hình cho tất cả ngày trong {row.RowName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            saveButton.Click += (s, e) =>
            {
                var vm = this.DataContext as MainViewModel;
                vm?.SaveConfigurationCommand.Execute(null);
                dialog.DialogResult = true;
            };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            dialog.Content = rootPanel;
            dialog.ShowDialog();
        }

        private void ShowEditShiftDialog(DayCell day)
        {
            // Tìm row chứa day này
            ShiftRow parentRow = null;
            var vm = this.DataContext as MainViewModel;
            if (vm != null)
            {
                foreach (var row in vm.Rows)
                {
                    if (row.Days.Contains(day))
                    {
                        parentRow = row;
                        break;
                    }
                }
            }

            if (parentRow == null) return;

            var dialog = new Window
            {
                Title = $"Chỉnh sửa cấu hình - {day.Date:dd/MM/yyyy} ({day.Date:dddd})",
                Width = 450,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var rootPanel = new DockPanel { Margin = new Thickness(20) };

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            // Reset button (only show if has custom config or day off toggled)
            if (day.HasCustomConfig || (day.IsWeekend != day.IsDayOff))
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
                    day.IsDayOff = day.IsWeekend; // Reset về mặc định theo lịch
                    day.Config.Workers = parentRow.DefaultConfig.Workers;
                    day.Config.Minutes = parentRow.DefaultConfig.Minutes;
                    day.Config.Efficiency = parentRow.DefaultConfig.Efficiency;
                    day.HasCustomConfig = false;
                    if (vm != null)
                    {
                        vm.RepackRowBlocks(parentRow);
                        var siblingRow = vm.Rows.FirstOrDefault(r => r != parentRow && r.ParentLineName == parentRow.ParentLineName);
                        if (siblingRow != null)
                            vm.RepackRowBlocks(siblingRow);
                    }
                    vm?.SaveConfigurationCommand.Execute(null);
                    MessageBox.Show("Đã reset về cấu hình mặc định của ca", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.DialogResult = true;
                };
                buttonPanel.Children.Add(resetButton);
            }

            var saveButton = new Button { Content = "Lưu", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Hủy", Width = 80, Height = 30 };

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            rootPanel.Children.Add(buttonPanel);

            // Content
            var contentPanel = new StackPanel();

            // === Quick Mass Actions ===
            var massActionPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15), Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)) };
            massActionPanel.Children.Add(new TextBlock { Text = "CÀI ĐẶT NHANH CHO TẤT CẢ CÁC LINE (CÙNG NGÀY)", FontWeight = FontWeights.Bold, Padding = new Thickness(5), Background = Brushes.LightGray });
            
            var btnContainer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10) };
            
            var btnMassOff = new Button { Content = "🔴 NGHỈ TOÀN BỘ", Width = 135, Height = 35, Margin = new Thickness(0,0,10,0), Background = Brushes.Crimson, Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            var btnMassWork = new Button { Content = "🟢 LÀM TOÀN BỘ", Width = 135, Height = 35, Background = Brushes.ForestGreen, Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            
            Action<bool> massApply = (isOff) => {
                string strAction = isOff ? "ĐÓNG NGHỈ" : "MỞ LẠI LÀM VIỆC";
                if (MessageBox.Show($"Bạn có chắc chắn muốn {strAction} cho TOÀN BỘ các line trong ngày {day.Date:dd/MM/yyyy} không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (vm != null)
                    {
                        foreach (var row in vm.Rows)
                        {
                            var targetDay = row.Days.FirstOrDefault(d => d.Date.Date == day.Date.Date);
                            if (targetDay != null)
                            {
                                targetDay.IsDayOff = isOff;
                                if (!isOff)
                                {
                                    bool isDifferent =
                                        targetDay.Config.Workers != row.DefaultConfig.Workers ||
                                        targetDay.Config.Minutes != row.DefaultConfig.Minutes ||
                                        targetDay.Config.Efficiency != row.DefaultConfig.Efficiency;
                                    targetDay.HasCustomConfig = isDifferent || (targetDay.IsWeekend != targetDay.IsDayOff);
                                }
                                else
                                {
                                    targetDay.HasCustomConfig = (targetDay.IsWeekend != targetDay.IsDayOff);
                                }
                            }
                        }
                        if (vm.GetType().GetMethod("RepackAll") != null)
                        {
                            vm.GetType().GetMethod("RepackAll").Invoke(vm, null);
                        }
                        vm.SaveConfigurationCommand.Execute(null);
                        dialog.DialogResult = true;
                    }
                }
            };
            
            btnMassOff.Click += (s, e) => massApply(true);
            btnMassWork.Click += (s, e) => massApply(false);
            
            btnContainer.Children.Add(btnMassOff);
            btnContainer.Children.Add(btnMassWork);
            massActionPanel.Children.Add(btnContainer);
            
            contentPanel.Children.Add(massActionPanel);

            // === Toggle Ngày nghỉ / Ngày làm ===
            var dayOffPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };

            string statusText = day.IsDayOff ? "🔴 Ngày này đang NGHỈ" : "🟢 Ngày này đang LÀM VIỆC";
            if (day.IsWeekend) statusText += " (Chủ nhật)";

            var statusBlock = new TextBlock
            {
                Text = statusText,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8)
            };
            dayOffPanel.Children.Add(statusBlock);

            var toggleDayOffCheckBox = new System.Windows.Controls.CheckBox
            {
                Content = "Cài đặt trạng thái Ngày này thành NGÀY NGHỈ",
                IsChecked = day.IsDayOff,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            dayOffPanel.Children.Add(toggleDayOffCheckBox);

            contentPanel.Children.Add(dayOffPanel);
            contentPanel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 10) });

            // === Cấu hình ca (ẩn/hiện theo trạng thái ngày nghỉ) ===
            var configSection = new StackPanel();

            var infoText = new TextBlock
            {
                Text = $"Chỉnh sửa riêng cho ô này{(day.HasCustomConfig ? " (đang dùng cấu hình riêng)" : " (đang dùng cấu hình mặc định của ca)")}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = day.HasCustomConfig ? Brushes.Orange : Brushes.DarkGreen,
                Margin = new Thickness(0, 0, 0, 15),
                FontStyle = FontStyles.Italic,
                FontWeight = FontWeights.Bold
            };
            configSection.Children.Add(infoText);

            var configPanel = CreateShiftPanel("Cấu hình ca", day.Config);
            configSection.Children.Add(configPanel);
            configSection.Visibility = day.IsDayOff ? Visibility.Collapsed : Visibility.Visible;

            // Toggle hiện/ẩn cấu hình khi check/uncheck ngày nghỉ
            toggleDayOffCheckBox.Checked += (s, e) => configSection.Visibility = Visibility.Collapsed;
            toggleDayOffCheckBox.Unchecked += (s, e) =>
            {
                configSection.Visibility = Visibility.Visible;
                // Nếu đang là chủ nhật mở lại làm → set workers/minutes/efficiency từ default của line
                if (day.IsDayOff && day.IsWeekend)
                {
                    day.Config.Workers = parentRow.DefaultConfig.Workers;
                    day.Config.Minutes = parentRow.DefaultConfig.Minutes;
                    day.Config.Efficiency = parentRow.DefaultConfig.Efficiency;
                }
            };

            contentPanel.Children.Add(configSection);
            contentPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 0) });
            rootPanel.Children.Add(contentPanel);

            // Save logic
            saveButton.Click += (s, e) =>
            {
                bool newIsDayOff = toggleDayOffCheckBox.IsChecked == true;
                day.IsDayOff = newIsDayOff;

                if (!newIsDayOff)
                {
                    // Ngày làm việc: kiểm tra custom config
                    bool isDifferent =
                        day.Config.Workers != parentRow.DefaultConfig.Workers ||
                        day.Config.Minutes != parentRow.DefaultConfig.Minutes ||
                        day.Config.Efficiency != parentRow.DefaultConfig.Efficiency;
                    day.HasCustomConfig = isDifferent || (day.IsWeekend != day.IsDayOff);
                }
                else
                {
                    // Ngày nghỉ
                    day.HasCustomConfig = (day.IsWeekend != day.IsDayOff);
                }
                if (vm?.GetType().GetMethod("RepackRowBlocks") != null)
                {
                    vm.RepackRowBlocks(parentRow);
                    var siblingRow = vm.Rows.FirstOrDefault(r => r != parentRow && r.ParentLineName == parentRow.ParentLineName);
                    if (siblingRow != null)
                        vm.RepackRowBlocks(siblingRow);
                }

                vm?.SaveConfigurationCommand.Execute(null);
                dialog.DialogResult = true;
            };

            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            dialog.Content = rootPanel;
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

            // Efficiency
            var effPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            effPanel.Children.Add(new TextBlock { Text = "Hiệu suất:", Width = 100, VerticalAlignment = VerticalAlignment.Center });
            var effBox = new TextBox
            {
                Width = 100,
                Text = shift.Efficiency.ToString("0.##")
            };
            effPanel.Children.Add(effBox);
            effPanel.Children.Add(new TextBlock
            {
                Text = "  (VD: 1.15 = 115%)",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            effBox.TextChanged += (s, e) =>
            {
                if (double.TryParse(effBox.Text, out double value) && value > 0)
                {
                    shift.Efficiency = value;
                }
            };
            effBox.LostFocus += (s, e) =>
            {
                effBox.Text = shift.Efficiency.ToString("0.##");
            };
            panel.Children.Add(effPanel);

            return panel;
        }

        // Hàm helper để tìm ShiftRow chứa DayCell đang được drop
        private ShiftRow? FindParentRow(DependencyObject child)
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            while (parentObject != null)
            {
                if (parentObject is FrameworkElement fe && fe.DataContext is ShiftRow row)
                {
                    return row;
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