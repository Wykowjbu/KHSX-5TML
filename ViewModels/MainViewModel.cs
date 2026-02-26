using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KHSX.Models;
using KHSX.Services;

namespace KHSX.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ExcelImportService _excelService;
        private readonly ConfigurationService _configService;

        [ObservableProperty]
        private ObservableCollection<ProductBlock> unassignedBlocks = new ObservableCollection<ProductBlock>();

        [ObservableProperty]
        private ObservableCollection<ProductionLine> lines = new ObservableCollection<ProductionLine>();

        [ObservableProperty]
        private DateTime deadlineDate = DateTime.Today.AddDays(7); // Default 7 days

        [ObservableProperty]
        private DateTime startDate = DateTime.Today;

        public MainViewModel()
        {
            _excelService = new ExcelImportService();
            _configService = new ConfigurationService();
            
            var config = _configService.LoadConfiguration();
            if (config == null)
            {
                // Nếu chưa có config, khởi tạo mặc định
                InitializeLines();
            }
            else
            {
                // Load từ config
                LoadConfiguration();
            }
        }

        private void InitializeLines()
        {
            Lines.Clear();
            for (int i = 1; i <= 5; i++)
            {
                var line = new ProductionLine($"Line {i}");
                
                // Add 30 days from start date
                for (int d = 0; d < 30; d++)
                {
                    var date = StartDate.AddDays(d);
                    var dayCell = new DayCell(date);
                    // Update deadline status
                    dayCell.IsDeadline = date.Date == DeadlineDate.Date;
                    line.Days.Add(dayCell);
                }
                
                Lines.Add(line);
            }
        }

        partial void OnDeadlineDateChanged(DateTime value)
        {
            // Update deadline flags on all day cells
            foreach (var line in Lines)
            {
                foreach (var day in line.Days)
                {
                    day.IsDeadline = day.Date.Date == value.Date;
                    CheckBlockExceeding(day); // Recheck block limits
                }
            }
            SaveConfiguration();
        }

        partial void OnStartDateChanged(DateTime value)
        {
            InitializeLines();
        }

        [RelayCommand]
        private void ImportExcel()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm",
                Title = "Chọn file dữ liệu sản xuất"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var blocks = _excelService.ImportProducts(dialog.FileName);
                    UnassignedBlocks.Clear();
                    foreach (var block in blocks)
                    {
                        UnassignedBlocks.Add(block);
                    }
                    MessageBox.Show($"Đã import thành công {blocks.Count} sản phẩm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void SaveConfiguration()
        {
            try
            {
                _configService.SaveConfiguration(StartDate, DeadlineDate, Lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void LoadConfiguration()
        {
            try
            {
                var config = _configService.LoadConfiguration();
                if (config != null)
                {
                    _configService.ApplyConfiguration(config, Lines, 
                        date => StartDate = date, 
                        date => DeadlineDate = date);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đọc cấu hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AddLine()
        {
            var newLineName = $"Line {Lines.Count + 1}";
            var line = new ProductionLine(newLineName);
            
            // Add 30 days from start date
            for (int d = 0; d < 30; d++)
            {
                var date = StartDate.AddDays(d);
                var dayCell = new DayCell(date);
                dayCell.IsDeadline = date.Date == DeadlineDate.Date;
                line.Days.Add(dayCell);
            }
            
            Lines.Add(line);
            SaveConfiguration();
        }

        [RelayCommand]
        private void RemoveLine(ProductionLine line)
        {
            if (line == null) return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa '{line.LineName}'?\nTất cả sản phẩm đã gán sẽ bị mất.",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Lines.Remove(line);
                SaveConfiguration();
            }
        }

        public void HandleDrop(ProductBlock droppedBlock, DayCell targetDay, ProductionLine targetLine)
        {
            // Nếu block này là từ panel trái (unassigned)
            if (UnassignedBlocks.Contains(droppedBlock))
            {
                UnassignedBlocks.Remove(droppedBlock);
                AssignBlockRecursively(droppedBlock, targetDay, targetLine);
            }
            else
            {
                // Nếu block được di chuyển từ lưới (grid), phải remove khỏi chỗ cũ trước
                RemoveBlockFromGrid(droppedBlock.ParentId ?? droppedBlock.Id);
                // Gán lại
                AssignBlockRecursively(droppedBlock, targetDay, targetLine);
            }
        }

        private void RemoveBlockFromGrid(Guid parentId)
        {
            foreach (var line in Lines)
            {
                foreach (var day in line.Days)
                {
                    var blocksToRemove = day.Blocks.Where(b => b.ParentId == parentId || b.Id == parentId).ToList();
                    foreach (var b in blocksToRemove)
                    {
                        day.Blocks.Remove(b);
                    }
                }
            }
        }

        private void AssignBlockRecursively(ProductBlock block, DayCell currentDay, ProductionLine currentLine)
        {
            double remainingMin = block.AllocatedMinutes;
            int dayIndex = currentLine.Days.IndexOf(currentDay);

            while (remainingMin > 0 && dayIndex < currentLine.Days.Count)
            {
                var workDay = currentLine.Days[dayIndex];
                
                if (workDay.IsWeekend)
                {
                    dayIndex++;
                    continue; // Skip sunday
                }

                double available = workDay.AvailableMinutes;
                
                if (available > 0)
                {
                    double toAssign = Math.Min(available, remainingMin);
                    
                    var splitBlock = block.CloneWithSplit(toAssign);
                    splitBlock.IsExceedingDeadline = workDay.Date > DeadlineDate;

                    workDay.Blocks.Add(splitBlock);
                    
                    remainingMin -= toAssign;
                }

                dayIndex++;
            }

            // Nếu vòng lặp kết thúc mà vẫn còn dư -> tức là tràn đến ngày cuối cùng, ta cho hiển thị cảnh báo
            if (remainingMin > 0)
            {
                // Để biểu diễn số phút còn dư chưa gán được, ta trả lại vào Unassigned? 
                // Hoặc báo lỗi.
                MessageBox.Show($"Line này không đủ công suất cho sản phẩm {block.Code}. Còn thừa {remainingMin} phút chưa được xếp lịch.", 
                              "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                var unassignedPortion = block.CloneWithSplit(remainingMin);
                UnassignedBlocks.Add(unassignedPortion);
            }
        }

        private void CheckBlockExceeding(DayCell day)
        {
            bool hasExceeded = day.Date > DeadlineDate;
            foreach (var block in day.Blocks)
            {
                block.IsExceedingDeadline = hasExceeded;
            }
        }
    }
}
