using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using KHSX.Models;
using KHSX.ViewModels;

namespace KHSX
{
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
            
            if (this.DataContext is MainViewModel vm)
            {
                vm.RequestDeadlineDialog += ShowDeadlineConfigDialog;
                vm.RequestConfigGroupsDialog += ShowConfigGroupsDialog;
                vm.RequestProductOrderSettingsDialog += ShowProductOrderSettingsDialog;
                vm.RequestExportOrderDialog += ShowExportOrderDialog;
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var currentTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                currentTheme == Wpf.Ui.Appearance.ApplicationTheme.Light 
                    ? Wpf.Ui.Appearance.ApplicationTheme.Dark 
                    : Wpf.Ui.Appearance.ApplicationTheme.Light
            );
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

            var dialog = new Views.ConfigGroupsDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void ShowDeadlineConfigDialog()
        {
            var groups = Services.JsonStorage.Load<System.Collections.Generic.List<ProductGroupData>>("productGroups.json");
            if (groups == null || groups.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu Marketing. Vui lòng Import Marketing (Bước 1) trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Views.DeadlineConfigDialog { Owner = this };
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
                var vm = this.DataContext as MainViewModel;
                if (vm == null) return;

                ShiftRow? parentRow = null;
                foreach (var row in vm.Rows)
                {
                    if (row.Days.Contains(day))
                    {
                        parentRow = row;
                        break;
                    }
                }
                if (parentRow == null) return;

                var dialog = new Views.EditShiftDialog { Owner = this };
                dialog.InitForDayCell(day, parentRow, vm);
                dialog.ShowDialog();
            }
        }

        private void LineName_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is ShiftRow row)
            {
                var vm = this.DataContext as MainViewModel;
                if (vm == null) return;

                var dialog = new Views.EditShiftDialog { Owner = this };
                dialog.InitForLine(row, vm);
                dialog.ShowDialog();
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
                    var bindingExpression = textBox.GetBindingExpression(TextBox.TextProperty);
                    bindingExpression?.UpdateSource();
                }

                if (this.DataContext is MainViewModel vm)
                {
                    vm.SaveConfigurationCommand.Execute(null);
                }
                
                Keyboard.ClearFocus();
            }
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
}