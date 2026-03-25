using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KHSX.Models;
using KHSX.ViewModels;

namespace KHSX.Views
{
    /// <summary>
    /// Dialog gộp cho cả cấu hình DayCell (ngày) và cấu hình Line (ca làm việc).
    /// Gọi InitForDayCell() hoặc InitForLine() sau khi tạo.
    /// </summary>
    public partial class EditShiftDialog : Wpf.Ui.Controls.FluentWindow
    {
        private ShiftConfig _config = null!;
        private DayCell? _dayCell;
        private ShiftRow? _parentRow;
        private MainViewModel? _vm;

        public EditShiftDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Khởi tạo dialog cho chỉnh sửa 1 ngày cụ thể (DayCell)
        /// </summary>
        public void InitForDayCell(DayCell day, ShiftRow parentRow, MainViewModel vm)
        {
            _dayCell = day;
            _parentRow = parentRow;
            _vm = vm;
            _config = day.Config;

            Title = $"Chỉnh sửa cấu hình - {day.Date:dd/MM/yyyy} ({day.Date:dddd})";

            // Hiện các panel cho DayCell
            MassActionPanel.Visibility = Visibility.Visible;
            DayOffPanel.Visibility = Visibility.Visible;
            DayOffSeparator.Visibility = Visibility.Visible;
            LineNamePanel.Visibility = Visibility.Collapsed;
            ApplyAllButton.Visibility = Visibility.Collapsed;

            // Status text
            string statusText = day.IsDayOff ? "🔴 Ngày này đang NGHỈ" : "🟢 Ngày này đang LÀM VIỆC";
            if (day.IsWeekend) statusText += " (Chủ nhật)";
            StatusBlock.Text = statusText;

            // Toggle
            ToggleDayOffCheckBox.IsChecked = day.IsDayOff;

            // Info text
            InfoText.Text = $"Chỉnh sửa riêng cho ô này{(day.HasCustomConfig ? " (đang dùng cấu hình riêng)" : " (đang dùng cấu hình mặc định của ca)")}";
            InfoText.Foreground = day.HasCustomConfig ? Brushes.Orange : Brushes.DarkGreen;

            // Reset button
            if (day.HasCustomConfig || (day.IsWeekend != day.IsDayOff))
            {
                ResetButton.Visibility = Visibility.Visible;
            }

            // Config visibility
            ConfigSection.Visibility = day.IsDayOff ? Visibility.Collapsed : Visibility.Visible;

            LoadConfig();
        }

        /// <summary>
        /// Khởi tạo dialog cho chỉnh sửa Line (ca làm việc)
        /// </summary>
        public void InitForLine(ShiftRow row, MainViewModel vm)
        {
            _parentRow = row;
            _vm = vm;
            _config = row.DefaultConfig;

            Title = $"Cấu hình {row.RowName}";

            // Hiện các panel cho Line
            MassActionPanel.Visibility = Visibility.Collapsed;
            DayOffPanel.Visibility = Visibility.Collapsed;
            DayOffSeparator.Visibility = Visibility.Collapsed;
            LineNamePanel.Visibility = Visibility.Visible;
            ResetButton.Visibility = Visibility.Collapsed;
            ApplyAllButton.Visibility = Visibility.Visible;

            LineNameBox.Text = row.RowName;

            InfoText.Text = "CẤU HÌNH MẶC ĐỊNH CHO TẤT CẢ CÁC NGÀY TRONG CA LÀM VIỆC NÀY";
            InfoText.Foreground = Brushes.DarkBlue;
            InfoText.FontSize = 12;

            LoadConfig();

            LineNameBox.Focus();
            LineNameBox.SelectAll();
        }

        private void LoadConfig()
        {
            WorkersBox.Text = _config.Workers.ToString("0.##");
            MinutesBox.Text = _config.Minutes.ToString("0.##");
            EfficiencyBox.Text = _config.Efficiency.ToString("0.##");
        }

        private void UpdateConfigFromUI()
        {
            if (double.TryParse(WorkersBox.Text, out double w) && w >= 0) _config.Workers = w;
            if (double.TryParse(MinutesBox.Text, out double m) && m >= 0) _config.Minutes = m;
            if (double.TryParse(EfficiencyBox.Text, out double eff) && eff > 0) _config.Efficiency = eff;
        }

        // === Event Handlers ===

        private void DayOff_Checked(object sender, RoutedEventArgs e)
        {
            ConfigSection.Visibility = Visibility.Collapsed;
        }

        private void DayOff_Unchecked(object sender, RoutedEventArgs e)
        {
            ConfigSection.Visibility = Visibility.Visible;
            if (_dayCell != null && _dayCell.IsDayOff && _dayCell.IsWeekend && _parentRow != null)
            {
                _dayCell.Config.Workers = _parentRow.DefaultConfig.Workers;
                _dayCell.Config.Minutes = _parentRow.DefaultConfig.Minutes;
                _dayCell.Config.Efficiency = _parentRow.DefaultConfig.Efficiency;
                LoadConfig();
            }
        }

        private void MassOff_Click(object sender, RoutedEventArgs e) => MassApply(true);
        private void MassWork_Click(object sender, RoutedEventArgs e) => MassApply(false);

        private void MassApply(bool isOff)
        {
            if (_dayCell == null || _vm == null) return;
            string strAction = isOff ? "ĐÓNG NGHỈ" : "MỞ LẠI LÀM VIỆC";
            if (MessageBox.Show($"Bạn có chắc chắn muốn {strAction} cho TOÀN BỘ các line trong ngày {_dayCell.Date:dd/MM/yyyy} không?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var row in _vm.Rows)
                {
                    var targetDay = row.Days.FirstOrDefault(d => d.Date.Date == _dayCell.Date.Date);
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
                _vm.RepackAll();
                _vm.SaveConfigurationCommand.Execute(null);
                DialogResult = true;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (_dayCell == null || _parentRow == null || _vm == null) return;

            _dayCell.IsDayOff = _dayCell.IsWeekend;
            _dayCell.Config.Workers = _parentRow.DefaultConfig.Workers;
            _dayCell.Config.Minutes = _parentRow.DefaultConfig.Minutes;
            _dayCell.Config.Efficiency = _parentRow.DefaultConfig.Efficiency;
            _dayCell.HasCustomConfig = false;

            _vm.RepackRowBlocks(_parentRow);
            var siblingRow = _vm.Rows.FirstOrDefault(r => r != _parentRow && r.ParentLineName == _parentRow.ParentLineName);
            if (siblingRow != null) _vm.RepackRowBlocks(siblingRow);

            _vm.SaveConfigurationCommand.Execute(null);
            MessageBox.Show("Đã reset về cấu hình mặc định của ca", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void ApplyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_parentRow == null || _vm == null) return;

            if (LineNamePanel.Visibility == Visibility.Visible)
            {
                if (string.IsNullOrWhiteSpace(LineNameBox.Text))
                {
                    MessageBox.Show("Tên ca không được để trống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _parentRow.RowName = LineNameBox.Text;
            }

            UpdateConfigFromUI();
            _parentRow.ApplyDefaultShiftToAllDays();
            _vm.SaveConfigurationCommand.Execute(null);
            MessageBox.Show($"Đã áp dụng cấu hình cho tất cả ngày trong {_parentRow.RowName}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfigFromUI();

            if (_dayCell != null && _parentRow != null)
            {
                // Lưu DayCell
                bool newIsDayOff = ToggleDayOffCheckBox.IsChecked == true;
                _dayCell.IsDayOff = newIsDayOff;

                if (!newIsDayOff)
                {
                    bool isDifferent =
                        _dayCell.Config.Workers != _parentRow.DefaultConfig.Workers ||
                        _dayCell.Config.Minutes != _parentRow.DefaultConfig.Minutes ||
                        _dayCell.Config.Efficiency != _parentRow.DefaultConfig.Efficiency;
                    _dayCell.HasCustomConfig = isDifferent || (_dayCell.IsWeekend != _dayCell.IsDayOff);
                }
                else
                {
                    _dayCell.HasCustomConfig = (_dayCell.IsWeekend != _dayCell.IsDayOff);
                }

                _vm?.RepackRowBlocks(_parentRow);
                var siblingRow = _vm?.Rows.FirstOrDefault(r => r != _parentRow && r.ParentLineName == _parentRow.ParentLineName);
                if (siblingRow != null) _vm?.RepackRowBlocks(siblingRow);
            }
            else if (_parentRow != null)
            {
                // Lưu Line
                if (LineNamePanel.Visibility == Visibility.Visible)
                {
                    if (string.IsNullOrWhiteSpace(LineNameBox.Text))
                    {
                        MessageBox.Show("Tên ca không được để trống!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    _parentRow.RowName = LineNameBox.Text;
                }
            }

            _vm?.SaveConfigurationCommand.Execute(null);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void WorkersBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(WorkersBox.Text, out double value) && value >= 0)
                _config.Workers = value;
            WorkersBox.Text = _config.Workers.ToString("0.##");
        }

        private void MinutesBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(MinutesBox.Text, out double value) && value >= 0)
                _config.Minutes = value;
            MinutesBox.Text = _config.Minutes.ToString("0.##");
        }
    }
}
