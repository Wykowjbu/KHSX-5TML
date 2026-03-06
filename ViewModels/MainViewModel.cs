using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
                    dayCell.HasCustomConfig = false; // Dùng cấu hình mặc định
                    dayCell.ShiftA.Workers = line.DefaultShiftA.Workers;
                    dayCell.ShiftA.Minutes = line.DefaultShiftA.Minutes;
                    dayCell.ShiftB.Workers = line.DefaultShiftB.Workers;
                    dayCell.ShiftB.Minutes = line.DefaultShiftB.Minutes;
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
            
            // Hiển thị cảnh báo vượt deadline nếu có
            ShowDeadlineExceedWarning();
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
            
            // Add 30 days from start date with default shift config
            for (int d = 0; d < 30; d++)
            {
                var date = StartDate.AddDays(d);
                var dayCell = new DayCell(date);
                dayCell.IsDeadline = date.Date == DeadlineDate.Date;
                dayCell.HasCustomConfig = false; // Dùng cấu hình mặc định
                dayCell.ShiftA.Workers = line.DefaultShiftA.Workers;
                dayCell.ShiftA.Minutes = line.DefaultShiftA.Minutes;
                dayCell.ShiftB.Workers = line.DefaultShiftB.Workers;
                dayCell.ShiftB.Minutes = line.DefaultShiftB.Minutes;
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
                // Block được di chuyển từ lưới (grid)
                Guid parentId = droppedBlock.ParentId ?? droppedBlock.Id;
                
                // Kiểm tra xem block được kéo có VƯỢT deadline không
                if (droppedBlock.IsExceedingDeadline)
                {
                    // CHỈ di chuyển phần SAU deadline, GIỮ NGUYÊN phần trước deadline
                    HandleDropExceedingBlock(droppedBlock, parentId, targetDay, targetLine);
                }
                else
                {
                    // Block TRƯỚC deadline hoặc không vượt -> di chuyển TOÀN BỘ (logic cũ)
                    HandleDropFullBlock(droppedBlock, parentId, targetDay, targetLine);
                }
            }
        }

        /// <summary>
        /// Xử lý kéo thả block VƯỢT deadline - chỉ di chuyển phần sau deadline
        /// </summary>
        private void HandleDropExceedingBlock(ProductBlock droppedBlock, Guid parentId, DayCell targetDay, ProductionLine targetLine)
        {
            // Tính tổng phút TRƯỚC deadline và SAU deadline
            double minutesBeforeDeadline = 0;
            double minutesAfterDeadline = 0;
            
            foreach (var line in Lines)
            {
                foreach (var day in line.Days)
                {
                    foreach (var b in day.Blocks)
                    {
                        if (b.ParentId == parentId || b.Id == parentId)
                        {
                            if (day.Date <= DeadlineDate)
                            {
                                minutesBeforeDeadline += b.AllocatedMinutes;
                            }
                            else
                            {
                                minutesAfterDeadline += b.AllocatedMinutes;
                            }
                        }
                    }
                }
            }

            minutesBeforeDeadline = Math.Round(minutesBeforeDeadline, 2);
            minutesAfterDeadline = Math.Round(minutesAfterDeadline, 2);

            // Nếu không có phút nào sau deadline, không cần làm gì
            if (minutesAfterDeadline <= 0.01)
            {
                MessageBox.Show("Không có phút nào sau deadline để di chuyển.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Xóa CHỈ các block SAU deadline (giữ nguyên phần trước deadline)
            RemoveBlocksAfterDeadline(parentId);
            
            // Tạo block mới với số phút SAU deadline để gán vào line mới
            var blockToMove = new ProductBlock
            {
                ParentId = parentId,
                SourceId = droppedBlock.SourceId,
                Code = droppedBlock.Code,
                TotalMinutesRequired = droppedBlock.TotalMinutesRequired,
                AllocatedMinutes = minutesAfterDeadline,
                DisplayColor = droppedBlock.DisplayColor
            };
            
            AssignBlockRecursively(blockToMove, targetDay, targetLine);

            // Thông báo cho user
            string message = $"Đã di chuyển {minutesAfterDeadline:0.##} phút (phần sau deadline) của {droppedBlock.Code} sang {targetLine.LineName}.\n" +
                           $"Giữ nguyên {minutesBeforeDeadline:0.##} phút (phần trước deadline) ở vị trí cũ.";
            MessageBox.Show(message, "Điều phối thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Xử lý kéo thả block TOÀN BỘ (không vượt deadline hoặc kéo từ phần trước deadline)
        /// </summary>
        private void HandleDropFullBlock(ProductBlock droppedBlock, Guid parentId, DayCell targetDay, ProductionLine targetLine)
        {
            // Tính tổng AllocatedMinutes của tất cả split blocks cùng ParentId
            double totalMinutes = 0;
            foreach (var line in Lines)
            {
                foreach (var day in line.Days)
                {
                    foreach (var b in day.Blocks)
                    {
                        if (b.ParentId == parentId || b.Id == parentId)
                        {
                            totalMinutes += b.AllocatedMinutes;
                        }
                    }
                }
            }

            // Làm tròn để tránh floating point issues
            totalMinutes = Math.Round(totalMinutes, 2);

            // Remove tất cả split blocks khỏi grid
            RemoveBlockFromGrid(parentId);
            
            // Tạo block mới với tổng phút đầy đủ để gán lại
            var reconstructedBlock = new ProductBlock
            {
                ParentId = parentId,
                SourceId = droppedBlock.SourceId,
                Code = droppedBlock.Code,
                TotalMinutesRequired = droppedBlock.TotalMinutesRequired,
                AllocatedMinutes = totalMinutes,
                DisplayColor = droppedBlock.DisplayColor
            };
            
            AssignBlockRecursively(reconstructedBlock, targetDay, targetLine);
        }

        /// <summary>
        /// Xóa chỉ các block SAU deadline của một ParentId
        /// </summary>
        private void RemoveBlocksAfterDeadline(Guid parentId)
        {
            foreach (var line in Lines)
            {
                foreach (var day in line.Days)
                {
                    // Chỉ xóa block ở các ngày SAU deadline
                    if (day.Date > DeadlineDate)
                    {
                        var blocksToRemove = day.Blocks.Where(b => b.ParentId == parentId || b.Id == parentId).ToList();
                        foreach (var b in blocksToRemove)
                        {
                            day.Blocks.Remove(b);
                        }
                    }
                }
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
            double remainingMin = Math.Round(block.AllocatedMinutes, 2);
            int dayIndex = currentLine.Days.IndexOf(currentDay);

            while (remainingMin > 0.01 && dayIndex < currentLine.Days.Count) // Dùng 0.01 thay vì 0 để tránh floating point issues
            {
                var workDay = currentLine.Days[dayIndex];
                
                if (workDay.IsWeekend)
                {
                    dayIndex++;
                    continue; // Skip sunday
                }

                double available = Math.Round(workDay.AvailableMinutes, 2);
                
                if (available > 0.01)
                {
                    double toAssign = Math.Round(Math.Min(available, remainingMin), 2);
                    
                    var splitBlock = block.CloneWithSplit(toAssign);
                    splitBlock.IsExceedingDeadline = workDay.Date > DeadlineDate;

                    workDay.Blocks.Add(splitBlock);
                    
                    remainingMin = Math.Round(remainingMin - toAssign, 2);
                }

                dayIndex++;
            }

            // Nếu vòng lặp kết thúc mà vẫn còn dư -> tức là tràn đến ngày cuối cùng, ta cho hiển thị cảnh báo
            if (remainingMin > 0.01)
            {
                // Để biểu diễn số phút còn dư chưa gán được, ta trả lại vào Unassigned? 
                // Hoặc báo lỗi.
                MessageBox.Show($"Line này không đủ công suất cho sản phẩm {block.Code}. Còn thừa {remainingMin:0.##} phút chưa được xếp lịch.", 
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

        /// <summary>
        /// Hiển thị cảnh báo chi tiết về các sản phẩm vượt deadline
        /// </summary>
        private void ShowDeadlineExceedWarning()
        {
            var exceedingInfo = GetDeadlineExceedingInfo();
            
            if (exceedingInfo.Count == 0)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("⚠️ CÓ SẢN PHẨM VƯỢT DEADLINE:");
            sb.AppendLine();
            
            double totalExceedMinutes = 0;
            foreach (var info in exceedingInfo)
            {
                sb.AppendLine($"• {info.ProductCode}: {info.ExceedMinutes:0.##} phút ({info.LineName})");
                totalExceedMinutes += info.ExceedMinutes;
            }
            
            sb.AppendLine();
            sb.AppendLine($"📊 TỔNG: {exceedingInfo.Count} sản phẩm, {totalExceedMinutes:0.##} phút vượt deadline");
            sb.AppendLine();
            sb.AppendLine("💡 MẸO: Kéo block viền đỏ sang line khác để điều phối.");
            sb.AppendLine("   Chỉ phần vượt deadline sẽ được di chuyển.");

            MessageBox.Show(sb.ToString(), "Cảnh báo Vượt Deadline", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Lấy thông tin chi tiết các sản phẩm vượt deadline
        /// </summary>
        public List<DeadlineExceedInfo> GetDeadlineExceedingInfo()
        {
            var result = new List<DeadlineExceedInfo>();
            
            // Group theo ParentId để tránh đếm trùng các split blocks
            var exceedingByProduct = new Dictionary<Guid, DeadlineExceedInfo>();
            
            foreach (var line in Lines)
            {
                foreach (var day in line.Days)
                {
                    if (day.Date > DeadlineDate)
                    {
                        foreach (var block in day.Blocks)
                        {
                            var parentId = block.ParentId ?? block.Id;
                            
                            if (!exceedingByProduct.ContainsKey(parentId))
                            {
                                exceedingByProduct[parentId] = new DeadlineExceedInfo
                                {
                                    ProductCode = block.Code,
                                    ParentId = parentId,
                                    LineName = line.LineName,
                                    ExceedMinutes = 0
                                };
                            }
                            
                            exceedingByProduct[parentId].ExceedMinutes += block.AllocatedMinutes;
                        }
                    }
                }
            }
            
            return exceedingByProduct.Values.ToList();
        }
    }

    /// <summary>
    /// Thông tin sản phẩm vượt deadline
    /// </summary>
    public class DeadlineExceedInfo
    {
        public string ProductCode { get; set; } = string.Empty;
        public Guid ParentId { get; set; }
        public string LineName { get; set; } = string.Empty;
        public double ExceedMinutes { get; set; }
    }
}
