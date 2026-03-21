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
        private List<DeadlineData> _customDeadlines = new();
        private bool _isLoading = false;

        [ObservableProperty]
        private ObservableCollection<ProductBlock> unassignedBlocks = new ObservableCollection<ProductBlock>();

        [ObservableProperty]
        private ObservableCollection<ShiftRow> rows = new ObservableCollection<ShiftRow>();

        [ObservableProperty]
        private DateTime deadlineDate = DateTime.Today.AddDays(7); // Default 7 days

        [ObservableProperty]
        private DateTime startDate = DateTime.Today;

        public ObservableCollection<DaySummary> DaySummaries { get; } = new ObservableCollection<DaySummary>();

        public MainViewModel()
        {
            _excelService = new ExcelImportService();
            _configService = new ConfigurationService();
            
            var config = _configService.LoadConfiguration();
            if (config == null)
            {
                // Nếu chưa có config, khởi tạo mặc định
                InitializeRows();
            }
            else
            {
                // Load từ config
                LoadConfiguration();
            }
        }

        private void InitializeRows()
        {
            Rows.Clear();
            for (int i = 1; i <= 5; i++)
            {
                var rowA = new ShiftRow($"Line {i}", "A") { DisplayIndex = i.ToString() };
                var rowB = new ShiftRow($"Line {i}", "B") { DisplayIndex = i.ToString() };
                
                for (int d = 0; d < 30; d++)
                {
                    var date = StartDate.AddDays(d);
                    
                    var dayCellA = new DayCell(date);
                    dayCellA.IsDeadline = false; // Deadline tổng đã bỏ, chỉ dùng deadline theo Gr.xxx
                    dayCellA.HasCustomConfig = false;
                    dayCellA.Config.Workers = rowA.DefaultConfig.Workers;
                    dayCellA.Config.Minutes = rowA.DefaultConfig.Minutes;
                    dayCellA.Config.Efficiency = rowA.DefaultConfig.Efficiency;
                    rowA.Days.Add(dayCellA);
                    
                    var dayCellB = new DayCell(date);
                    dayCellB.IsDeadline = false;
                    dayCellB.HasCustomConfig = false;
                    dayCellB.Config.Workers = rowB.DefaultConfig.Workers;
                    dayCellB.Config.Minutes = rowB.DefaultConfig.Minutes;
                    dayCellB.Config.Efficiency = rowB.DefaultConfig.Efficiency;
                    rowB.Days.Add(dayCellB);
                }
                
                Rows.Add(rowA);
                Rows.Add(rowB);
            }
        }

        partial void OnDeadlineDateChanged(DateTime value)
        {
            // Deadline tổng đã bị loại bỏ khỏi UI.
            // Giữ method này để tránh crash nếu config cũ load giá trị vào.
        }

        partial void OnStartDateChanged(DateTime value)
        {
            if (_isLoading) return; // Bỏ qua nếu đang nạp dữ liệu từ json

            if (Rows.Count == 0 || Rows[0].Days.Count == 0)
            {
                InitializeRows();
                SaveConfiguration();
                return;
            }

            var oldStartDate = Rows[0].Days[0].Date.Date;
            var newStartDate = value.Date;
            var dayOffset = (newStartDate - oldStartDate).Days;
            if (dayOffset == 0) return;

            // BƯỚC 1: Thu thập toàn bộ blocks của từng row (gộp các split cùng ParentId)
            // Lưu theo cấu trúc: rowName -> danh sách block đã gộp (mỗi parentId 1 block)
            var rowBlocksMap = new Dictionary<string, List<ProductBlock>>();
            foreach (var row in Rows)
            {
                var mergedBlocks = new Dictionary<Guid, ProductBlock>();
                foreach (var day in row.Days)
                {
                    foreach (var block in day.Blocks)
                    {
                        var pid = block.ParentId ?? block.Id;
                        if (mergedBlocks.TryGetValue(pid, out var existing))
                        {
                            existing.AllocatedMinutes = Math.Round(existing.AllocatedMinutes + block.AllocatedMinutes, 2);
                        }
                        else
                        {
                            mergedBlocks[pid] = new ProductBlock
                            {
                                ParentId = pid,
                                SourceId = block.SourceId,
                                Code = block.Code,
                                ProductionGroup = block.ProductionGroup,
                                FunctionName = block.FunctionName,
                                TotalMinutesRequired = block.TotalMinutesRequired,
                                AllocatedMinutes = block.AllocatedMinutes,
                                DisplayColor = block.DisplayColor
                            };
                        }
                    }
                }
                rowBlocksMap[row.RowName] = mergedBlocks.Values.ToList();
            }

            // BƯỚC 2a: Snapshot custom configs theo ngày tuyệt đối TRƯỚC khi dịch
            // Key: (rowName, date) → vì config có thể khác nhau giữa row A và B 
            var customConfigSnapshot = new Dictionary<(string rowName, DateTime date), (double workers, double minutes, double efficiency, bool isDayOff)>();
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    if (day.HasCustomConfig)
                    {
                        customConfigSnapshot[(row.RowName, day.Date.Date)] = (
                            day.Config.Workers,
                            day.Config.Minutes,
                            day.Config.Efficiency,
                            day.IsDayOff
                        );
                    }
                }
            }

            // BƯỚC 2b: Dịch chuyển ngày, sau đó áp custom config theo ngày tuyệt đối mới
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    day.Date = day.Date.AddDays(dayOffset);
                    day.IsWeekend = day.Date.DayOfWeek == DayOfWeek.Sunday;
                    day.IsDeadline = false;

                    // Kiểm tra ngày mới có custom config đã lưu không
                    if (customConfigSnapshot.TryGetValue((row.RowName, day.Date.Date), out var saved))
                    {
                        // Ngày mới này có custom config đã lưu → áp dụng lại đúng ngày
                        day.Config.Workers = saved.workers;
                        day.Config.Minutes = saved.minutes;
                        day.Config.Efficiency = saved.efficiency;
                        day.IsDayOff = saved.isDayOff;
                        day.HasCustomConfig = true;
                    }
                    else
                    {
                        // Không có custom config → reset về default của row
                        day.Config.Workers = row.DefaultConfig.Workers;
                        day.Config.Minutes = row.DefaultConfig.Minutes;
                        day.Config.Efficiency = row.DefaultConfig.Efficiency;
                        day.HasCustomConfig = false;
                        day.IsDayOff = day.IsWeekend;
                    }
                }
            }

            // BƯỚC 3: Xoá sạch blocks khỏi lưới
            foreach (var row in Rows)
                foreach (var day in row.Days)
                    day.Blocks.Clear();

            // BƯỚC 4: Tái phân bổ blocks liên tục bằng AssignBlockRecursively
            foreach (var row in Rows)
            {
                if (!rowBlocksMap.TryGetValue(row.RowName, out var blocks)) continue;
                foreach (var block in blocks)
                {
                    if (block.AllocatedMinutes > 0.01)
                        AssignBlockRecursively(block, row.Days[0], row);
                }
            }

            // BƯỚC 5: Cập nhật cờ deadline (viền đỏ) cho mọi block sau khi re-pack
            foreach (var row in Rows)
                foreach (var day in row.Days)
                    CheckBlockExceeding(day);

            SaveConfiguration();
        }

        [RelayCommand]
        private void ImportMarketing()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm",
                Title = "Chọn file dữ liệu Marketing (chứa Gr.xxx và Sản phẩm)"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _excelService.ImportMarketing(dialog.FileName);
                    MessageBox.Show("Nhập dữ liệu Marketing thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi import file Marketing: {ex.Message}", "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ImportMES()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm",
                Title = "Chọn file dữ liệu MES (chứa Open Minutes)"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _excelService.ImportMES(dialog.FileName);
                    
                    // Lấy ra các block mới từ dữ liệu MES vừa import
                    var newBlocks = _excelService.GenerateBlocksFromData();
                    
                    // 1. Cập nhật các Block MỚI và CŨ chưa gán
                    foreach (var newBlock in newBlocks)
                    {
                        var unassignedMatch = UnassignedBlocks.FirstOrDefault(b => b.Code == newBlock.Code);
                        if (unassignedMatch != null)
                        {
                            // Đã có trong unassigned -> chỉ cập nhật lại số phút
                            unassignedMatch.TotalMinutesRequired = newBlock.TotalMinutesRequired;
                            unassignedMatch.AllocatedMinutes = newBlock.AllocatedMinutes;
                        }
                        else
                        {
                            // Kiểm tra xem block có nằm trên Grid không (tìm theo Code)
                            double allocatedOnGrid = 0;
                            List<ProductBlock> splitsOnGrid = new List<ProductBlock>();
                            
                            foreach (var r in Rows)
                            {
                                foreach (var d in r.Days)
                                {
                                    foreach (var b in d.Blocks)
                                    {
                                        if (b.Code == newBlock.Code)
                                        {
                                            allocatedOnGrid += b.AllocatedMinutes;
                                            splitsOnGrid.Add(b);
                                        }
                                    }
                                }
                            }
                            
                            if (splitsOnGrid.Any())
                            {
                                // Block đã nằm trên grid
                                double minuteDiff = newBlock.TotalMinutesRequired - allocatedOnGrid;
                                
                                if (Math.Abs(minuteDiff) > 0.01) // Sản lượng thay đổi (Tăng hoặc Giảm)
                                {
                                    // Lấy tất cả các line chứa block này (để repack sau)
                                    var affectedRows = splitsOnGrid
                                        .SelectMany(b => Rows.Where(row => row.Days.Any(day => day.Blocks.Contains(b))))
                                        .Distinct()
                                        .ToList();

                                    // Xác định line đầu tiên block xuất hiện
                                    var firstSplitInfo = splitsOnGrid
                                        .SelectMany(b => Rows.SelectMany(row => row.Days.Where(day => day.Blocks.Contains(b)).Select(day => new { Block = b, Day = day, Row = row })))
                                        .OrderBy(x => x.Day.Date)
                                        .FirstOrDefault();
                                        
                                    if (firstSplitInfo != null)
                                    {
                                        // Xoá SẠCH TẤT CẢ các mảnh của block này trên mọi DayCell
                                        foreach (var r in Rows)
                                        {
                                            foreach (var d in r.Days)
                                            {
                                                var splitsToRemove = d.Blocks.Where(b => b.Code == newBlock.Code).ToList();
                                                foreach (var split in splitsToRemove)
                                                {
                                                    d.Blocks.Remove(split);
                                                }
                                            }
                                        }
                                        
                                        // Repack các line bị ảnh hưởng TRƯỚC (để các block khác dồn lên trước)
                                        foreach (var affectedRow in affectedRows)
                                        {
                                            RepackRowBlocks(affectedRow);
                                        }

                                        // Gom toàn bộ số lượng mới thành 1 block nguyên bản
                                        var reconstructedBlock = new ProductBlock
                                        {
                                            Id = Guid.NewGuid(),
                                            SourceId = newBlock.SourceId,
                                            Code = newBlock.Code,
                                            ProductionGroup = newBlock.ProductionGroup,
                                            FunctionName = newBlock.FunctionName,
                                            TotalMinutesRequired = newBlock.TotalMinutesRequired,
                                            AllocatedMinutes = newBlock.TotalMinutesRequired,
                                            DisplayColor = splitsOnGrid.First().DisplayColor
                                        };
                                        reconstructedBlock.ParentId = reconstructedBlock.Id;

                                        // Thả lại block đó VÀO LINE ĐẦU TIÊN (lúc này sp2 đã dồn lên, sp1 sẽ nối tiếp sau sp2)
                                        AssignBlockRecursively(reconstructedBlock, firstSplitInfo.Day, firstSplitInfo.Row);
                                    }
                                }
                            }
                            else
                            {
                                // Block hoàn toàn mới chưa từng xuất hiện trên Grid lẫn Unassigned
                                UnassignedBlocks.Add(newBlock);
                            }
                        }
                    }
                    
                    MessageBox.Show($"Đã cập nhật/tạo thành công dữ liệu từ hệ thống MES!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    SaveConfiguration(); // Lưu kết quả thay đổi ngay

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Lỗi Import MES", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        [RelayCommand]
        private void ConfigGroups()
        {
            RequestConfigGroupsDialog?.Invoke(); // UI sẽ mở dialog (ShowDialog là block synchronously)
            RefreshBlocksMetadata(); // Reload cấu hình và update blocks
            SaveConfiguration(); // Lưu lại vào config json
        }

        [RelayCommand]
        private void ConfigDeadlines()
        {
            // Được gọi từ UI MainWindow
            RequestDeadlineDialog?.Invoke();
        }

        public void RefreshBlocksMetadata()
        {
            var groups = JsonStorage.Load<List<ProductGroupData>>("productGroups.json");
            if (groups == null) return;
            
            var groupDict = groups.ToDictionary(g => g.GroupId, g => g);
            
            // 1. Cập nhật các blocks chưa gán
            foreach (var block in UnassignedBlocks)
            {
                if (groupDict.TryGetValue(block.Code, out var matchedGroup))
                {
                    block.FunctionName = matchedGroup.Name ?? string.Empty;
                    block.ProductionGroup = matchedGroup.ProductionGroup ?? string.Empty;
                }
            }
            
            // 2. Cập nhật các blocks đang nằm trên Grid
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    foreach (var block in day.Blocks)
                    {
                        if (groupDict.TryGetValue(block.Code, out var matchedGroup))
                        {
                            block.FunctionName = matchedGroup.Name ?? string.Empty;
                            block.ProductionGroup = matchedGroup.ProductionGroup ?? string.Empty;
                        }
                    }
                    // Kiểm tra lại deadline vì ProductionGroup có thể đã thay đổi
                    CheckBlockExceeding(day);
                }
            }
        }

        public void RefreshDeadlines()
        {
            LoadCustomDeadlines();
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    CheckBlockExceeding(day);
                }
            }
        }

        private void LoadCustomDeadlines()
        {
            try
            {
                var dls = JsonStorage.Load<List<DeadlineData>>("deadlines.json");
                if (dls != null)
                {
                    _customDeadlines = dls;
                }
            }
            catch { }
        }

        private DateTime GetDeadlineForBlock(ProductBlock block)
        {
            if (!string.IsNullOrEmpty(block.ProductionGroup))
            {
                var dl = _customDeadlines.FirstOrDefault(d => d.GroupNumber == block.ProductionGroup);
                if (dl != null)
                {
                    return dl.Deadline.Date;
                }
            }
            // Fallback: không có Gr.xxx deadline -> không giới hạn (không bao giờ vượt)
            return DateTime.MaxValue;
        }

        public event Action RequestDeadlineDialog;
        public event Action RequestConfigGroupsDialog;

        public void UpdateDaySummaries()
        {
            if (Rows == null || !Rows.Any()) return;
            
            if (DaySummaries.Count == 0)
            {
                for (int i = 0; i < 30; i++)
                {
                    DaySummaries.Add(new DaySummary(StartDate.AddDays(i)));
                }
            }

            for (int i = 0; i < 30; i++)
            {
                var date = StartDate.AddDays(i);
                if (i < DaySummaries.Count)
                {
                    DaySummaries[i].Date = date;
                    
                    double workers = 0;
                    bool allOff = true;
                    
                    foreach (var row in Rows)
                    {
                        var cell = row.Days.FirstOrDefault(d => d.Date.Date == date.Date);
                        if (cell != null && !cell.IsDayOff)
                        {
                            workers += cell.Config.Workers;
                            allOff = false;
                        }
                    }
                    
                    DaySummaries[i].TotalWorkers = workers;
                    DaySummaries[i].IsDayOff = allOff;
                }
            }
        }

        public void RepackRowBlocks(ShiftRow row)
        {
            if (row == null || row.Days.Count == 0) return;

            var orderedBlocks = new List<ProductBlock>();
            var processedIds = new HashSet<Guid>();
            
            foreach (var day in row.Days)
            {
                foreach (var split in day.Blocks.ToList())
                {
                    var pid = split.ParentId ?? split.Id;
                    if (!processedIds.Contains(pid))
                    {
                        processedIds.Add(pid);
                        var totalOnThisRow = Math.Round(row.Days.SelectMany(d => d.Blocks)
                                                     .Where(b => (b.ParentId ?? b.Id) == pid)
                                                     .Sum(b => b.AllocatedMinutes), 2);
                        
                        var reconstructed = new ProductBlock
                        {
                            Id = Guid.NewGuid(),
                            ParentId = pid,
                            SourceId = split.SourceId,
                            Code = split.Code,
                            ProductionGroup = split.ProductionGroup,
                            FunctionName = split.FunctionName,
                            TotalMinutesRequired = totalOnThisRow,
                            AllocatedMinutes = totalOnThisRow,
                            DisplayColor = split.DisplayColor
                        };
                        orderedBlocks.Add(reconstructed);
                    }
                    day.Blocks.Remove(split);
                }
            }

            var firstDay = row.Days.First();
            foreach (var block in orderedBlocks)
            {
                AssignBlockRecursively(block, firstDay, row);
            }
        }

        public void RepackAll()
        {
            foreach (var row in Rows)
            {
                RepackRowBlocks(row);
            }
        }

        public void UpdateLineDeadlines()
        {
            if (Rows == null || !Rows.Any()) return;
            
            foreach (var row in Rows)
            {
                DateTime? maxDeadline = null;
                foreach (var day in row.Days)
                {
                    foreach (var block in day.Blocks)
                    {
                        var dl = GetDeadlineForBlock(block);
                        if (maxDeadline == null || dl > maxDeadline.Value)
                        {
                            maxDeadline = dl;
                        }
                    }
                }

                foreach (var day in row.Days)
                {
                    day.IsWithinLineDeadline = maxDeadline.HasValue && day.Date.Date <= maxDeadline.Value.Date;
                }
            }
        }

        [RelayCommand]
        private void SaveConfiguration()
        {
            try
            {
                UpdateDaySummaries();
                UpdateLineDeadlines();

                // 1. Lưu cấu hình Line, Ngày làm việc
                _configService.SaveConfiguration(StartDate, DeadlineDate, Rows);

                // 2. Lưu danh sách Block chưa gán
                var unassignedList = UnassignedBlocks.Select(b => new BlockData
                {
                    BlockId = b.Id,
                    ParentId = b.ParentId,
                    SourceId = b.SourceId,
                    GroupId = b.Code,
                    ProductionGroup = b.ProductionGroup,
                    FunctionName = b.FunctionName,
                    TotalMinutesRequired = b.TotalMinutesRequired,
                    AllocatedMinutes = b.AllocatedMinutes,
                    DisplayColorHex = b.DisplayColorHex
                }).ToList();
                JsonStorage.Save("blocks.json", unassignedList);

                // 3. Lưu Schedule (Các block đã gán trên Grid)
                var schedules = new List<ScheduleData>();
                foreach (var row in Rows)
                {
                    foreach (var day in row.Days)
                    {
                        foreach (var block in day.Blocks)
                        {
                            schedules.Add(new ScheduleData
                            {
                                RowId = row.RowName, 
                                Date = day.Date,
                                BlockInfo = new BlockData
                                {
                                    BlockId = block.Id,
                                    ParentId = block.ParentId,
                                    SourceId = block.SourceId,
                                    GroupId = block.Code,
                                    ProductionGroup = block.ProductionGroup,
                                    FunctionName = block.FunctionName,
                                    TotalMinutesRequired = block.TotalMinutesRequired,
                                    AllocatedMinutes = block.AllocatedMinutes,
                                    DisplayColorHex = block.DisplayColorHex
                                }
                            });
                        }
                    }
                }
                JsonStorage.Save("schedule.json", schedules);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu cấu hình và lịch: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void LoadConfiguration()
        {
            try
            {
                _isLoading = true; // Bật cờ isLoading để chặn các event OnChanged

                // 1. Load cấu hình cơ bản
                var config = _configService.LoadConfiguration();
                if (config != null)
                {
                    _configService.ApplyConfiguration(config, Rows, 
                        date => StartDate = date, 
                        date => DeadlineDate = date);
                }

                // 2. Load blocks chưa gán
                var unassignedList = JsonStorage.Load<List<BlockData>>("blocks.json");
                if (unassignedList != null)
                {
                    UnassignedBlocks.Clear();
                    foreach (var b in unassignedList)
                    {
                        UnassignedBlocks.Add(new ProductBlock(b));
                    }
                }

                // Tiền nạp custom deadlines
                LoadCustomDeadlines();

                // 3. Load schedule và gắn vào grid
                var schedules = JsonStorage.Load<List<ScheduleData>>("schedule.json");
                if (schedules != null)
                {
                    foreach (var sched in schedules)
                    {
                        var row = Rows.FirstOrDefault(r => r.RowName == sched.RowId);
                        if (row != null)
                        {
                            var dayCell = row.Days.FirstOrDefault(d => d.Date.Date == sched.Date.Date);
                            if (dayCell != null && sched.BlockInfo != null)
                            {
                                var block = new ProductBlock(sched.BlockInfo);
                                block.IsExceedingDeadline = dayCell.Date > GetDeadlineForBlock(block);
                                dayCell.Blocks.Add(block);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phần khôi phục dữ liệu hệ thống: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false; // Tắt cờ isLoading
            }
        }

        [RelayCommand]
        private void AddLine()
        {
            // Thay vì count row, lấy số line = count/2
            int newIndex = (Rows.Count / 2) + 1;
            var newLineName = $"Line {newIndex}";
            var rowA = new ShiftRow(newLineName, "A") { DisplayIndex = newIndex.ToString() };
            var rowB = new ShiftRow(newLineName, "B") { DisplayIndex = newIndex.ToString() };
            
            for (int d = 0; d < 30; d++)
            {
                var date = StartDate.AddDays(d);
                
                var dayCellA = new DayCell(date);
                dayCellA.IsDeadline = false;
                dayCellA.HasCustomConfig = false;
                dayCellA.Config.Workers = rowA.DefaultConfig.Workers;
                dayCellA.Config.Minutes = rowA.DefaultConfig.Minutes;
                dayCellA.Config.Efficiency = rowA.DefaultConfig.Efficiency;
                rowA.Days.Add(dayCellA);
                
                var dayCellB = new DayCell(date);
                dayCellB.IsDeadline = false;
                dayCellB.HasCustomConfig = false;
                dayCellB.Config.Workers = rowB.DefaultConfig.Workers;
                dayCellB.Config.Minutes = rowB.DefaultConfig.Minutes;
                dayCellB.Config.Efficiency = rowB.DefaultConfig.Efficiency;
                rowB.Days.Add(dayCellB);
            }
            
            Rows.Add(rowA);
            Rows.Add(rowB);
            SaveConfiguration();
        }

        [RelayCommand]
        private void RemoveLine(ShiftRow row)
        {
            if (row == null) return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa '{row.RowName}'?\nTất cả sản phẩm đã gán sẽ bị mất.",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Lưu các block vào unassigned
                foreach (var day in row.Days)
                {
                    foreach (var block in day.Blocks.ToList())
                    {
                        var reconstructedBlock = new ProductBlock
                        {
                            ParentId = block.ParentId ?? block.Id,
                            SourceId = block.SourceId,
                            Code = block.Code,
                            ProductionGroup = block.ProductionGroup,
                            FunctionName = block.FunctionName,
                            TotalMinutesRequired = block.TotalMinutesRequired,
                            AllocatedMinutes = block.AllocatedMinutes,
                            DisplayColor = block.DisplayColor
                        };
                        
                        // Ghép với block đã có trong unassigned nếu cùng parentId
                        var existingUnassigned = UnassignedBlocks.FirstOrDefault(b => b.ParentId == reconstructedBlock.ParentId || b.Id == reconstructedBlock.ParentId);
                        if (existingUnassigned != null)
                        {
                            existingUnassigned.AllocatedMinutes += reconstructedBlock.AllocatedMinutes;
                        }
                        else
                        {
                            UnassignedBlocks.Add(reconstructedBlock);
                        }
                    }
                }

                Rows.Remove(row);
                SaveConfiguration();
            }
        }

        [RelayCommand]
        private void ClearAllBlocks()
        {
            var result = MessageBox.Show(
                "Bạn thực sự muốn reset tất cả?\nToàn bộ sản phẩm đã gán trên các line sẽ bị trả lại về mục chưa gán.",
                "Xác nhận xoá toàn bộ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool hasBlocks = false;
                // Di chuyển tất cả các block trên grid về unassigned
                foreach (var row in Rows)
                {
                    foreach (var day in row.Days)
                    {
                        if (day.Blocks.Count > 0)
                        {
                            hasBlocks = true;
                            foreach (var block in day.Blocks.ToList())
                            {
                                var reconstructedBlock = new ProductBlock
                                {
                                    ParentId = block.ParentId ?? block.Id,
                                    SourceId = block.SourceId,
                                    Code = block.Code,
                                    ProductionGroup = block.ProductionGroup,
                                    FunctionName = block.FunctionName,
                                    TotalMinutesRequired = block.TotalMinutesRequired,
                                    AllocatedMinutes = block.AllocatedMinutes,
                                    DisplayColor = block.DisplayColor
                                };
                                
                                // Ghép với block đã có trong unassigned nếu cùng parentId
                                var existingUnassigned = UnassignedBlocks.FirstOrDefault(b => b.ParentId == reconstructedBlock.ParentId || b.Id == reconstructedBlock.ParentId);
                                if (existingUnassigned != null)
                                {
                                    existingUnassigned.AllocatedMinutes += reconstructedBlock.AllocatedMinutes;
                                }
                                else
                                {
                                    UnassignedBlocks.Add(reconstructedBlock);
                                }
                            }
                            day.Blocks.Clear();
                        }
                    }
                }

                if (hasBlocks)
                {
                    SaveConfiguration();
                    MessageBox.Show("Đã trả toàn bộ sản phẩm về khu vực chưa gán.", "Thành Công!", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Các line hiện tại đang trống, không có sản phẩm nào để xoá.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public void HandleDrop(ProductBlock droppedBlock, DayCell targetDay, ShiftRow targetRow)
        {
            // Nếu block này là từ panel trái (unassigned)
            if (UnassignedBlocks.Contains(droppedBlock))
            {
                UnassignedBlocks.Remove(droppedBlock);
                AssignBlockRecursively(droppedBlock, targetDay, targetRow);
            }
            else
            {
                // Block được di chuyển từ lưới (grid)
                Guid parentId = droppedBlock.ParentId ?? droppedBlock.Id;
                
                // Kiểm tra xem block được kéo có VƯỢT deadline không
                if (droppedBlock.IsExceedingDeadline)
                {
                    // CHỈ di chuyển phần SAU deadline, GIỮ NGUYÊN phần trước deadline
                    HandleDropExceedingBlock(droppedBlock, parentId, targetDay, targetRow);
                }
                else
                {
                    // Block TRƯỚC deadline hoặc không vượt -> di chuyển TOÀN BỘ (logic cũ)
                    HandleDropFullBlock(droppedBlock, parentId, targetDay, targetRow);
                }
            }

            // Lưu lại thông tin lưới sau khi kéo thả thành công
            SaveConfiguration();
        }

        /// <summary>
        /// Kéo block từ grid trả về danh sách chưa gán
        /// </summary>
        public void HandleReturnToUnassigned(ProductBlock droppedBlock)
        {
            Guid parentId = droppedBlock.ParentId ?? droppedBlock.Id;

            // Tính tổng phút của tất cả splits cùng parentId trên grid
            double totalMinutes = 0;
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
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
            totalMinutes = Math.Round(totalMinutes, 2);

            // Xóa tất cả splits khỏi grid
            RemoveBlockFromGrid(parentId);

            // Ghép với block đã có trong unassigned nếu cùng parentId
            var existingUnassigned = UnassignedBlocks.FirstOrDefault(b => b.ParentId == parentId || b.Id == parentId);
            if (existingUnassigned != null)
            {
                existingUnassigned.AllocatedMinutes += totalMinutes;
            }
            else
            {
                var returnedBlock = new ProductBlock
                {
                    ParentId = parentId,
                    SourceId = droppedBlock.SourceId,
                    Code = droppedBlock.Code,
                    ProductionGroup = droppedBlock.ProductionGroup,
                    FunctionName = droppedBlock.FunctionName,
                    TotalMinutesRequired = droppedBlock.TotalMinutesRequired,
                    AllocatedMinutes = totalMinutes,
                    DisplayColor = droppedBlock.DisplayColor
                };
                UnassignedBlocks.Add(returnedBlock);
            }

            SaveConfiguration();
        }

        /// <summary>
        /// Xử lý kéo thả block VƯỢT deadline - chỉ di chuyển phần sau deadline
        /// </summary>
        private void HandleDropExceedingBlock(ProductBlock droppedBlock, Guid parentId, DayCell targetDay, ShiftRow targetRow)
        {
            // Tính tổng phút TRƯỚC deadline và SAU deadline
            double minutesBeforeDeadline = 0;
            double minutesAfterDeadline = 0;
            
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    foreach (var b in day.Blocks)
                    {
                        if (b.ParentId == parentId || b.Id == parentId)
                        {
                            if (day.Date <= GetDeadlineForBlock(b))
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
                ProductionGroup = droppedBlock.ProductionGroup,
                FunctionName = droppedBlock.FunctionName,
                TotalMinutesRequired = droppedBlock.TotalMinutesRequired,
                AllocatedMinutes = minutesAfterDeadline,
                DisplayColor = droppedBlock.DisplayColor
            };
            
            AssignBlockRecursively(blockToMove, targetDay, targetRow);

            // Thông báo cho user
            string message = $"Đã di chuyển {minutesAfterDeadline:0.##} phút (phần sau deadline) của {droppedBlock.Code} sang {targetRow.RowName}.\n" +
                           $"Giữ nguyên {minutesBeforeDeadline:0.##} phút (phần trước deadline) ở vị trí cũ.";
            MessageBox.Show(message, "Điều phối thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Xử lý kéo thả block TOÀN BỘ (không vượt deadline hoặc kéo từ phần trước deadline)
        /// </summary>
        private void HandleDropFullBlock(ProductBlock droppedBlock, Guid parentId, DayCell targetDay, ShiftRow targetRow)
        {
            // Tính tổng AllocatedMinutes của tất cả split blocks cùng ParentId
            double totalMinutes = 0;
            // Xác định line nguồn (line chứa block trước khi kéo đi) để repack sau
            var sourceRows = new HashSet<ShiftRow>();
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    foreach (var b in day.Blocks)
                    {
                        if (b.ParentId == parentId || b.Id == parentId)
                        {
                            totalMinutes += b.AllocatedMinutes;
                            sourceRows.Add(row);
                        }
                    }
                }
            }

            // Làm tròn để tránh floating point issues
            totalMinutes = Math.Round(totalMinutes, 2);

            // Remove tất cả split blocks khỏi grid
            RemoveBlockFromGrid(parentId);

            // Repack các line nguồn (để block còn lại dồn lên lấp chỗ trống)
            foreach (var srcRow in sourceRows)
            {
                if (srcRow != targetRow) // Không repack line đích vì sắp assign vào đó
                {
                    RepackRowBlocks(srcRow);
                }
            }
            
            // Tạo block mới với tổng phút đầy đủ để gán lại
            var reconstructedBlock = new ProductBlock
            {
                ParentId = parentId,
                SourceId = droppedBlock.SourceId,
                Code = droppedBlock.Code,
                ProductionGroup = droppedBlock.ProductionGroup,
                FunctionName = droppedBlock.FunctionName,
                TotalMinutesRequired = droppedBlock.TotalMinutesRequired,
                AllocatedMinutes = totalMinutes,
                DisplayColor = droppedBlock.DisplayColor
            };
            
            AssignBlockRecursively(reconstructedBlock, targetDay, targetRow);
        }

        /// <summary>
        /// Xóa chỉ các block SAU deadline của một ParentId
        /// </summary>
        private void RemoveBlocksAfterDeadline(Guid parentId)
        {
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    var blocksToRemove = day.Blocks.Where(b => (b.ParentId == parentId || b.Id == parentId) && day.Date > GetDeadlineForBlock(b)).ToList();
                    foreach (var b in blocksToRemove)
                    {
                        day.Blocks.Remove(b);
                    }
                }
            }
        }

        private void RemoveBlockFromGrid(Guid parentId)
        {
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
                {
                    var blocksToRemove = day.Blocks.Where(b => b.ParentId == parentId || b.Id == parentId).ToList();
                    foreach (var b in blocksToRemove)
                    {
                        day.Blocks.Remove(b);
                    }
                }
            }
        }

        private void AssignBlockRecursively(ProductBlock block, DayCell targetDayDrop, ShiftRow targetRow)
        {
            double remainingMin = Math.Round(block.AllocatedMinutes, 2);
            // AUTO-PULL LOGIC: Luôn bắt đầu dò từ ngày đầu tiên thay vì ngày mà người dùng thả chuột
            int dayIndex = 0;

            while (remainingMin > 0.01 && dayIndex < targetRow.Days.Count) // Dùng 0.01 thay vì 0 để tránh floating point issues
            {
                var workDay = targetRow.Days[dayIndex];
                
                if (workDay.IsDayOff)
                {
                    dayIndex++;
                    continue; // Skip ngày nghỉ
                }

                double available = Math.Round(workDay.AvailableMinutes, 2);
                
                if (available > 0.01)
                {
                    double toAssign = Math.Round(Math.Min(available, remainingMin), 2);
                    
                    var splitBlock = block.CloneWithSplit(toAssign);
                    splitBlock.IsExceedingDeadline = workDay.Date > GetDeadlineForBlock(splitBlock);

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
            foreach (var block in day.Blocks)
            {
                block.IsExceedingDeadline = day.Date > GetDeadlineForBlock(block);
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
                sb.AppendLine($"• {info.ProductCode}: {info.ExceedMinutes:0.##} phút ({info.RowName})");
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
            
            foreach (var row in Rows)
            {
                foreach (var day in row.Days)
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
                                    RowName = row.RowName,
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
        public string RowName { get; set; } = string.Empty;
        public double ExceedMinutes { get; set; }
    }
}
