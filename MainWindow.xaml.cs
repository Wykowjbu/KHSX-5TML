using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Text.RegularExpressions;
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
                vm.RequestMissingFpMappingsDialog += ShowMissingFpMappingsDialog;
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
            var mappings = Services.JsonStorage.Load<System.Collections.Generic.List<ModuleMappingData>>("moduleMappings.json");
            if (mappings == null || mappings.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu Module List. Vui lòng Import Module List trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingSettings = Services.JsonStorage.Load<System.Collections.Generic.List<BuildGroupShiftSettingData>>("buildGroupSettings.json")
                ?? new System.Collections.Generic.List<BuildGroupShiftSettingData>();
            var settingMap = existingSettings
                .Where(s => !string.IsNullOrWhiteSpace(s.BuildGroup))
                .GroupBy(s => s.BuildGroup)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            var buildGroups = mappings
                .GroupBy(m => m.BuildGroup)
                .Select(g =>
                {
                    settingMap.TryGetValue(g.Key, out var setting);
                    return new
                    {
                        BuildGroup = g.Key,
                        FunctionName = setting?.FunctionName ?? g.First().FunctionName,
                        Fps = string.Join(", ", g.Select(x => x.FP).OrderBy(x => x)),
                        Setting = setting ?? new BuildGroupShiftSettingData
                        {
                            BuildGroup = g.Key,
                            FunctionName = g.First().FunctionName,
                            UseShiftA = true,
                            UseShiftB = false,
                            WorkersA = 1,
                            WorkersB = 1
                        }
                    };
                })
                .OrderBy(x => x.BuildGroup)
                .ToList();

            var dialog = new Window
            {
                Title = "Cấu Hình BuildGroup / Ca",
                Width = 900,
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
                Text = "Chọn ca làm A/B và số người cho từng BuildGroup. FP được lấy từ Module List.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            topPanel.Children.Add(infoText);

            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var h1 = new TextBlock { Text = "Build Group", FontWeight = FontWeights.Bold };
            var h2 = new TextBlock { Text = "Function", FontWeight = FontWeights.Bold };
            var h3 = new TextBlock { Text = "FP", FontWeight = FontWeights.Bold };
            var h4 = new TextBlock { Text = "Ca A", FontWeight = FontWeights.Bold };
            var h5 = new TextBlock { Text = "Người A", FontWeight = FontWeights.Bold };
            var h6 = new TextBlock { Text = "Ca B", FontWeight = FontWeights.Bold };
            var h7 = new TextBlock { Text = "Người B", FontWeight = FontWeights.Bold };
            var h8 = new TextBlock { Text = "Xoá", FontWeight = FontWeights.Bold };
            Grid.SetColumn(h1, 0); Grid.SetColumn(h2, 1); Grid.SetColumn(h3, 2); Grid.SetColumn(h4, 3); Grid.SetColumn(h5, 4); Grid.SetColumn(h6, 5); Grid.SetColumn(h7, 6); Grid.SetColumn(h8, 7);
            headerRow.Children.Add(h1); headerRow.Children.Add(h2); headerRow.Children.Add(h3); headerRow.Children.Add(h4); headerRow.Children.Add(h5); headerRow.Children.Add(h6); headerRow.Children.Add(h7); headerRow.Children.Add(h8);
            topPanel.Children.Add(headerRow);
            rootPanel.Children.Add(topPanel);

            var gridPanel = new Grid();
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var useABoxes = new System.Collections.Generic.Dictionary<string, CheckBox>();
            var useBBoxes = new System.Collections.Generic.Dictionary<string, CheckBox>();
            var workersABoxes = new System.Collections.Generic.Dictionary<string, TextBox>();
            var workersBBoxes = new System.Collections.Generic.Dictionary<string, TextBox>();
            var deletedBuildGroups = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int rowIdx = 0;
            foreach (var group in buildGroups)
            {
                gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var idLabel = new TextBlock { Text = group.BuildGroup, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 10, 5) };
                var rowControls = new System.Collections.Generic.List<UIElement> { idLabel };
                Grid.SetRow(idLabel, rowIdx);
                Grid.SetColumn(idLabel, 0);
                gridPanel.Children.Add(idLabel);

                var functionLabel = new TextBlock { Text = group.FunctionName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 10, 5), TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(functionLabel, rowIdx);
                Grid.SetColumn(functionLabel, 1);
                gridPanel.Children.Add(functionLabel);
                rowControls.Add(functionLabel);

                var fpLabel = new TextBlock { Text = group.Fps, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 10, 5), TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(fpLabel, rowIdx);
                Grid.SetColumn(fpLabel, 2);
                gridPanel.Children.Add(fpLabel);
                rowControls.Add(fpLabel);

                var useA = new CheckBox { IsChecked = group.Setting.UseShiftA, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                useABoxes[group.BuildGroup] = useA;
                Grid.SetRow(useA, rowIdx);
                Grid.SetColumn(useA, 3);
                gridPanel.Children.Add(useA);
                rowControls.Add(useA);

                var workersA = new TextBox { Text = group.Setting.WorkersA.ToString("0.##"), Margin = new Thickness(0, 5, 10, 5), Padding = new Thickness(2) };
                workersABoxes[group.BuildGroup] = workersA;
                Grid.SetRow(workersA, rowIdx);
                Grid.SetColumn(workersA, 4);
                gridPanel.Children.Add(workersA);
                rowControls.Add(workersA);

                var useB = new CheckBox { IsChecked = group.Setting.UseShiftB, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                useBBoxes[group.BuildGroup] = useB;
                Grid.SetRow(useB, rowIdx);
                Grid.SetColumn(useB, 5);
                gridPanel.Children.Add(useB);
                rowControls.Add(useB);

                var workersB = new TextBox { Text = group.Setting.WorkersB.ToString("0.##"), Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(2) };
                workersBBoxes[group.BuildGroup] = workersB;
                Grid.SetRow(workersB, rowIdx);
                Grid.SetColumn(workersB, 6);
                gridPanel.Children.Add(workersB);
                rowControls.Add(workersB);

                var deleteBtn = new Button { Content = "Xoá", Width = 52, Height = 26, Margin = new Thickness(8, 5, 0, 5) };
                deleteBtn.Click += (s, e) =>
                {
                    deletedBuildGroups.Add(group.BuildGroup);
                    foreach (var control in rowControls)
                        control.Visibility = Visibility.Collapsed;
                    deleteBtn.Visibility = Visibility.Collapsed;
                };
                Grid.SetRow(deleteBtn, rowIdx);
                Grid.SetColumn(deleteBtn, 7);
                gridPanel.Children.Add(deleteBtn);

                rowIdx++;
            }

            var scrollView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scrollView.Content = gridPanel;

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            var addBtn = new Button { Content = "Thêm BuildGroup", Width = 130, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            addBtn.Click += (s, e) =>
            {
                var added = ShowAddBuildGroupDialog();
                if (added == null) return;

                var allMappings = Services.JsonStorage.Load<System.Collections.Generic.List<ModuleMappingData>>("moduleMappings.json");
                allMappings.RemoveAll(m => string.Equals(m.BuildGroup, added.Value.Mapping.BuildGroup, StringComparison.OrdinalIgnoreCase)
                                           && string.Equals(m.FP, added.Value.Mapping.FP, StringComparison.OrdinalIgnoreCase));
                allMappings.Add(added.Value.Mapping);
                Services.JsonStorage.Save("moduleMappings.json", allMappings);

                var allSettings = Services.JsonStorage.Load<System.Collections.Generic.List<BuildGroupShiftSettingData>>("buildGroupSettings.json");
                allSettings.RemoveAll(s => string.Equals(s.BuildGroup, added.Value.Setting.BuildGroup, StringComparison.OrdinalIgnoreCase));
                allSettings.Add(added.Value.Setting);
                Services.JsonStorage.Save("buildGroupSettings.json", allSettings);

                MessageBox.Show("Đã thêm BuildGroup. Mở lại cấu hình để xem dòng mới.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                dialog.DialogResult = true;
            };
            
            var saveBtn = new Button { Content = "Lưu Cập Nhật", Width = 100, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)), Foreground = Brushes.White };
            saveBtn.Click += (s, e) =>
            {
                var newSettings = new System.Collections.Generic.List<BuildGroupShiftSettingData>();
                foreach (var group in buildGroups)
                {
                    if (deletedBuildGroups.Contains(group.BuildGroup)) continue;

                    var useA = useABoxes[group.BuildGroup].IsChecked == true;
                    var useB = useBBoxes[group.BuildGroup].IsChecked == true;
                    if (!useA && !useB)
                    {
                        MessageBox.Show($"BuildGroup {group.BuildGroup} phải chọn ít nhất một ca A hoặc B.", "Thiếu cấu hình", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    double.TryParse(workersABoxes[group.BuildGroup].Text, out var workersA);
                    double.TryParse(workersBBoxes[group.BuildGroup].Text, out var workersB);

                    newSettings.Add(new BuildGroupShiftSettingData
                    {
                        BuildGroup = group.BuildGroup,
                        FunctionName = group.FunctionName,
                        UseShiftA = useA,
                        UseShiftB = useB,
                        WorkersA = workersA > 0 ? workersA : 1,
                        WorkersB = workersB > 0 ? workersB : 1
                    });
                }

                var remainingMappings = mappings
                    .Where(m => !deletedBuildGroups.Contains(m.BuildGroup))
                    .ToList();
                Services.JsonStorage.Save("moduleMappings.json", remainingMappings);
                Services.JsonStorage.Save("buildGroupSettings.json", newSettings);
                MessageBox.Show("Đã lưu cấu hình BuildGroup/Ca!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                dialog.DialogResult = true;
            };

            var cancelBtn = new Button { Content = "Hủy", Width = 80, Height = 30 };
            cancelBtn.Click += (s, e) => dialog.DialogResult = false;
            buttonPanel.Children.Add(addBtn);
            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            rootPanel.Children.Add(buttonPanel);
            rootPanel.Children.Add(scrollView);

            dialog.Content = rootPanel;
            dialog.ShowDialog();
        }

        private (ModuleMappingData Mapping, BuildGroupShiftSettingData Setting)? ShowAddBuildGroupDialog()
        {
            var dialog = new Window
            {
                Title = "Thêm BuildGroup",
                Width = 420,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            TextBox AddTextBox(string label, string text = "")
            {
                panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
                var box = new TextBox { Text = text, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(4) };
                panel.Children.Add(box);
                return box;
            }

            var buildGroupBox = AddTextBox("BuildGroup");
            var functionBox = AddTextBox("Function");
            var fpBox = AddTextBox("FP (nếu bỏ trống sẽ dùng BuildGroup)");

            var shiftPanel = new Grid { Margin = new Thickness(0, 4, 0, 10) };
            shiftPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            shiftPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shiftPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            shiftPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var useA = new CheckBox { Content = "Ca A", IsChecked = true, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var workersA = new TextBox { Text = "1", Width = 60, Margin = new Thickness(0, 0, 16, 0), Padding = new Thickness(4) };
            var useB = new CheckBox { Content = "Ca B", IsChecked = false, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var workersB = new TextBox { Text = "1", Width = 60, Padding = new Thickness(4) };
            Grid.SetColumn(useA, 0); Grid.SetColumn(workersA, 1); Grid.SetColumn(useB, 2); Grid.SetColumn(workersB, 3);
            shiftPanel.Children.Add(useA); shiftPanel.Children.Add(workersA); shiftPanel.Children.Add(useB); shiftPanel.Children.Add(workersB);
            panel.Children.Add(shiftPanel);

            (ModuleMappingData Mapping, BuildGroupShiftSettingData Setting)? result = null;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var saveBtn = new Button { Content = "Thêm", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)), Foreground = Brushes.White };
            saveBtn.Click += (s, e) =>
            {
                var buildGroup = buildGroupBox.Text.Trim().ToUpperInvariant();
                var functionName = functionBox.Text.Trim();
                var fp = string.IsNullOrWhiteSpace(fpBox.Text) ? buildGroup : fpBox.Text.Trim().ToUpperInvariant();
                var useShiftA = useA.IsChecked == true;
                var useShiftB = useB.IsChecked == true;

                if (string.IsNullOrWhiteSpace(buildGroup) || string.IsNullOrWhiteSpace(functionName))
                {
                    MessageBox.Show("BuildGroup và Function là bắt buộc.", "Thiếu dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!useShiftA && !useShiftB)
                {
                    MessageBox.Show("Phải chọn ít nhất một ca A hoặc B.", "Thiếu ca", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                double.TryParse(workersA.Text, out var workerAValue);
                double.TryParse(workersB.Text, out var workerBValue);

                var mapping = new ModuleMappingData
                {
                    FP = fp,
                    BuildGroup = buildGroup,
                    FunctionName = functionName,
                    IsManual = true
                };
                var setting = new BuildGroupShiftSettingData
                {
                    BuildGroup = buildGroup,
                    FunctionName = functionName,
                    UseShiftA = useShiftA,
                    UseShiftB = useShiftB,
                    WorkersA = workerAValue > 0 ? workerAValue : 1,
                    WorkersB = workerBValue > 0 ? workerBValue : 1
                };

                result = (mapping, setting);
                dialog.DialogResult = true;
            };
            var cancelBtn = new Button { Content = "Hủy", Width = 80, Height = 30 };
            cancelBtn.Click += (s, e) => dialog.DialogResult = false;
            buttons.Children.Add(saveBtn);
            buttons.Children.Add(cancelBtn);
            panel.Children.Add(buttons);

            dialog.Content = panel;
            return dialog.ShowDialog() == true ? result : null;
        }

        private void ShowDeadlineConfigDialog()
        {
            var planningGroups = Services.JsonStorage.Load<System.Collections.Generic.List<PlanningBlockData>>("planningBlocks.json")
                .Select(b => NormalizeProductionGroup(b.ProductionGroup));
            var openGroups = Services.JsonStorage.Load<System.Collections.Generic.List<OpenMinutesBlockData>>("openMinutes.json")
                .Select(b => NormalizeProductionGroup(b.ProductionGroup));

            var existingDeadlines = Services.JsonStorage.Load<System.Collections.Generic.List<DeadlineData>>("deadlines.json") 
                ?? new System.Collections.Generic.List<DeadlineData>();
            var existingGroups = existingDeadlines.Select(d => NormalizeProductionGroup(d.GroupNumber));

            var requiredGroups = planningGroups
                .Concat(openGroups)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();

            var productionGroups = requiredGroups
                .Concat(existingGroups)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();

            var dialog = new Window
            {
                Title = "Thiết Lập Deadline Cho Group",
                Width = 560,
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
                Text = "Nhập deadline cho Gr.xxx đang dùng. Gr.xxx cũ không còn trong Planning/MES có thể xoá bằng cách để trống ngày rồi lưu.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(infoText, Dock.Top);
            rootPanel.Children.Add(infoText);

            var scrollView = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var gridPanel = new Grid();
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            gridPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var datePickers = new System.Collections.Generic.Dictionary<string, DatePicker>();
            var deletedGroups = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int rowIdx = 0;
            void AddDeadlineRow(string rawGroup, DateTime? selectedDate, bool isRequired)
            {
                var grp = NormalizeProductionGroup(rawGroup);
                if (string.IsNullOrWhiteSpace(grp) || datePickers.ContainsKey(grp)) return;

                gridPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var groupLabel = new TextBlock 
                { 
                    Text = isRequired ? $"{grp} *" : $"{grp} (tự thêm/cũ)",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5),
                    FontWeight = FontWeights.Bold,
                    ToolTip = isRequired ? "Gr.xxx đang có trong Planning/MES, bắt buộc nhập deadline." : "Có thể xoá dòng này."
                };
                Grid.SetRow(groupLabel, rowIdx);
                Grid.SetColumn(groupLabel, 0);
                gridPanel.Children.Add(groupLabel);

                var datePicker = new DatePicker { Margin = new Thickness(0, 5, 0, 5) };
                datePicker.SelectedDate = selectedDate;
                datePickers[grp] = datePicker;
                Grid.SetRow(datePicker, rowIdx);
                Grid.SetColumn(datePicker, 1);
                gridPanel.Children.Add(datePicker);

                var deleteBtn = new Button { Content = "Xoá", Width = 52, Height = 26, Margin = new Thickness(8, 5, 0, 5) };
                deleteBtn.Click += (s, e) =>
                {
                    if (requiredGroups.Contains(grp, StringComparer.OrdinalIgnoreCase))
                    {
                        MessageBox.Show($"{grp} đang có trong Planning/MES nên không thể xoá.", "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    deletedGroups.Add(grp);
                    datePickers.Remove(grp);
                    groupLabel.Visibility = Visibility.Collapsed;
                    datePicker.Visibility = Visibility.Collapsed;
                    deleteBtn.Visibility = Visibility.Collapsed;
                };
                Grid.SetRow(deleteBtn, rowIdx);
                Grid.SetColumn(deleteBtn, 2);
                gridPanel.Children.Add(deleteBtn);

                rowIdx++;
            }

            foreach (var group in productionGroups)
            {
                var normalized = NormalizeProductionGroup(group);
                var existing = existingDeadlines.Find(d =>
                    string.Equals(NormalizeProductionGroup(d.GroupNumber), normalized, StringComparison.OrdinalIgnoreCase));
                AddDeadlineRow(normalized, existing?.Deadline, requiredGroups.Contains(normalized, StringComparer.OrdinalIgnoreCase));
            }

            scrollView.Content = gridPanel;

            // Button panel - docked bottom
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            var newGroupBox = new TextBox
            {
                Width = 95,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "Nhập 285 hoặc Gr.285"
            };

            var addBtn = new Button { Content = "Thêm Gr", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            addBtn.Click += (s, e) =>
            {
                var grp = NormalizeProductionGroup(newGroupBox.Text);
                if (string.IsNullOrWhiteSpace(grp))
                {
                    MessageBox.Show("Nhập Gr.xxx trước khi thêm.", "Thiếu Gr.xxx", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (datePickers.ContainsKey(grp))
                {
                    MessageBox.Show($"{grp} đã có trong danh sách.", "Trùng Gr.xxx", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                deletedGroups.Remove(grp);
                AddDeadlineRow(grp, null, false);
                newGroupBox.Text = "";
            };
            
            var saveBtn = new Button { Content = "Lưu Deadlines", Width = 100, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)), Foreground = Brushes.White };
            saveBtn.Click += (s, e) =>
            {
                var missing = datePickers
                    .Where(kvp => requiredGroups.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase) && !kvp.Value.SelectedDate.HasValue)
                    .Select(kvp => kvp.Key)
                    .ToList();
                if (missing.Count > 0)
                {
                    MessageBox.Show("Phải nhập deadline cho: " + string.Join(", ", missing), "Thiếu Deadline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newDeadlines = new System.Collections.Generic.List<DeadlineData>();
                foreach (var kvp in datePickers)
                {
                    if (!deletedGroups.Contains(kvp.Key) && kvp.Value.SelectedDate.HasValue)
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

            buttonPanel.Children.Add(newGroupBox);
            buttonPanel.Children.Add(addBtn);
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

        private System.Collections.Generic.List<ModuleMappingData>? ShowMissingFpMappingsDialog(System.Collections.Generic.List<string> missingFps)
        {
            if (missingFps == null || missingFps.Count == 0) return new System.Collections.Generic.List<ModuleMappingData>();

            var dialog = new Window
            {
                Title = "Tạo mapping FP thiếu",
                Width = 620,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                MinHeight = 260
            };

            var rootPanel = new DockPanel { Margin = new Thickness(20) };
            var infoText = new TextBlock
            {
                Text = "Các FP dưới đây chưa có trong Module List. Nhập BuildGroup và Function để lưu lại dùng cho các lần import sau.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(infoText, Dock.Top);
            rootPanel.Children.Add(infoText);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var fpHeader = new TextBlock { Text = "FP", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 10, 5) };
            var bgHeader = new TextBlock { Text = "BuildGroup", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 10, 5) };
            var fnHeader = new TextBlock { Text = "Function", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(fpHeader, 0); Grid.SetColumn(fpHeader, 0);
            Grid.SetRow(bgHeader, 0); Grid.SetColumn(bgHeader, 1);
            Grid.SetRow(fnHeader, 0); Grid.SetColumn(fnHeader, 2);
            grid.Children.Add(fpHeader); grid.Children.Add(bgHeader); grid.Children.Add(fnHeader);

            var buildGroupBoxes = new System.Collections.Generic.Dictionary<string, TextBox>();
            var functionBoxes = new System.Collections.Generic.Dictionary<string, TextBox>();

            int rowIndex = 1;
            foreach (var fp in missingFps.OrderBy(x => x))
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var fpLabel = new TextBlock { Text = fp, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 10, 5), FontWeight = FontWeights.Bold };
                Grid.SetRow(fpLabel, rowIndex);
                Grid.SetColumn(fpLabel, 0);
                grid.Children.Add(fpLabel);

                var bgBox = new TextBox { Text = fp, Margin = new Thickness(0, 5, 10, 5), Padding = new Thickness(2) };
                buildGroupBoxes[fp] = bgBox;
                Grid.SetRow(bgBox, rowIndex);
                Grid.SetColumn(bgBox, 1);
                grid.Children.Add(bgBox);

                var fnBox = new TextBox { Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(2) };
                functionBoxes[fp] = fnBox;
                Grid.SetRow(fnBox, rowIndex);
                Grid.SetColumn(fnBox, 2);
                grid.Children.Add(fnBox);

                rowIndex++;
            }

            var scroll = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            var resultMappings = new System.Collections.Generic.List<ModuleMappingData>();
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            DockPanel.SetDock(buttonPanel, Dock.Bottom);

            var saveButton = new Button { Content = "Lưu Mapping", Width = 110, Height = 30, Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)), Foreground = Brushes.White };
            saveButton.Click += (s, e) =>
            {
                resultMappings.Clear();
                foreach (var fp in missingFps)
                {
                    var buildGroup = buildGroupBoxes[fp].Text.Trim();
                    var functionName = functionBoxes[fp].Text.Trim();
                    if (string.IsNullOrWhiteSpace(buildGroup) || string.IsNullOrWhiteSpace(functionName))
                    {
                        MessageBox.Show($"FP {fp} phải có BuildGroup và Function.", "Thiếu mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    resultMappings.Add(new ModuleMappingData
                    {
                        FP = fp,
                        BuildGroup = buildGroup,
                        FunctionName = functionName,
                        IsManual = true
                    });
                }

                dialog.DialogResult = true;
            };

            var cancelButton = new Button { Content = "Hủy", Width = 80, Height = 30 };
            cancelButton.Click += (s, e) => dialog.DialogResult = false;

            buttonPanel.Children.Add(saveButton);
            buttonPanel.Children.Add(cancelButton);
            rootPanel.Children.Add(buttonPanel);
            rootPanel.Children.Add(scroll);

            dialog.Content = rootPanel;
            return dialog.ShowDialog() == true ? resultMappings : null;
        }

        private void ProductBlock_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element)
            {
                if (element.DataContext is ProductBlock block)
                {
                    var data = new DataObject("ProductBlock", block);
                    DragDrop.DoDragDrop(element, data, DragDropEffects.Move);
                }
            }
        }

        private void DayCell_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("ProductBlock") && !e.Data.GetDataPresent("DayCellBlocks"))
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
            if (e.Data.GetDataPresent("DayCellBlocks"))
            {
                var sourceDay = e.Data.GetData("DayCellBlocks") as DayCell;
                if (sourceDay != null && sender is FrameworkElement element && element.Tag is DayCell targetDay)
                {
                    var targetRow = FindParentRow(element);
                    var vm = this.DataContext as MainViewModel;
                    if (targetRow != null)
                    {
                        vm?.HandleDropCellGroup(sourceDay, targetDay, targetRow);
                        vm?.SaveConfigurationCommand.Execute(null);
                    }
                }
                return;
            }

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
                    if (vm != null)
                    {
                        vm.RepackRowBlocks(row);
                        var siblingRow = vm.Rows.FirstOrDefault(r => r != row && r.ParentLineName == row.ParentLineName);
                        if (siblingRow != null)
                            vm.RepackRowBlocks(siblingRow);
                        vm.SaveConfigurationCommand.Execute(null);
                    }
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
                if (vm != null)
                {
                    vm.RepackRowBlocks(row);
                    var siblingRow = vm.Rows.FirstOrDefault(r => r != row && r.ParentLineName == row.ParentLineName);
                    if (siblingRow != null)
                        vm.RepackRowBlocks(siblingRow);
                    vm.SaveConfigurationCommand.Execute(null);
                }
                MessageBox.Show($"Đã áp dụng cấu hình cho tất cả ngày trong {row.RowName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            saveButton.Click += (s, e) =>
            {
                var vm = this.DataContext as MainViewModel;
                if (vm != null)
                {
                    vm.RepackRowBlocks(row);
                    var siblingRow = vm.Rows.FirstOrDefault(r => r != row && r.ParentLineName == row.ParentLineName);
                    if (siblingRow != null)
                        vm.RepackRowBlocks(siblingRow);
                    vm.SaveConfigurationCommand.Execute(null);
                }
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
                        vm.RepackRowBlocksKeepingOverflowInDay(parentRow, day);
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

        private DayCell? FindParentDayCell(DependencyObject child)
        {
            DependencyObject parentObject = child;
            while (parentObject != null)
            {
                if (parentObject is FrameworkElement fe && fe.Tag is DayCell day)
                {
                    return day;
                }
                parentObject = VisualTreeHelper.GetParent(parentObject);
            }
            return null;
        }

        private static string NormalizeProductionGroup(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var grMatch = Regex.Match(text, @"Gr[\.\s]*(\d+)", RegexOptions.IgnoreCase);
            if (grMatch.Success) return $"Gr.{grMatch.Groups[1].Value}";

            var numericMatch = Regex.Match(text, @"^\d{3,}$");
            if (numericMatch.Success) return $"Gr.{numericMatch.Value}";

            return text;
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
