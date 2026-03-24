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
using Microsoft.Win32;

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
                    RefreshBlocksMetadata();
                    SaveConfiguration();
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

                    // --- BẮT ĐẦU FIX: DỌN DẸP CÁC SẢN PHẨM ĐÃ LÀM XONG (SỐ PHÚT = 0) ---
                    var activeCodes = new HashSet<string>(newBlocks.Select(b => b.Code));

                    // 1. Quét phần Chưa gán (Unassigned)
                    var obsoleteUnassigned = UnassignedBlocks.Where(b => !activeCodes.Contains(b.Code)).ToList();
                    foreach (var obsolete in obsoleteUnassigned)
                    {
                        UnassignedBlocks.Remove(obsolete);
                    }

                    // 2. Quét trên Lịch (Grid/Rows)
                    var rowsToRepackForRemoval = new HashSet<ShiftRow>();
                    foreach (var row in Rows)
                    {
                        bool rowModified = false;
                        foreach (var day in row.Days)
                        {
                            var obsoleteSplits = day.Blocks.Where(b => !activeCodes.Contains(b.Code)).ToList();
                            foreach (var split in obsoleteSplits)
                            {
                                day.Blocks.Remove(split);
                                rowModified = true;
                            }
                        }
                        if (rowModified) 
                        {
                            rowsToRepackForRemoval.Add(row);
                        }
                    }

                    // 3. Đóng gói lại (dồn về bên trái) những hàng vừa bị xoá mất mảnh ghép
                    foreach (var row in rowsToRepackForRemoval)
                    {
                        RepackRowBlocks(row);
                    }
                    // --- KẾT THÚC FIX DỌN DẸP ---

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

                                    var splitInfos = splitsOnGrid
                                        .SelectMany(b => Rows.SelectMany(row => row.Days.Where(day => day.Blocks.Contains(b)).Select(day => new { Block = b, Day = day, Row = row })))
                                        .OrderBy(x => x.Day.Date)
                                        .ToList();

                                    if (splitInfos.Any())
                                    {
                                        // Cập nhật TotalMinutesRequired cho tất cả các mảnh hiện có
                                        foreach (var info in splitInfos)
                                        {
                                            info.Block.TotalMinutesRequired = newBlock.TotalMinutesRequired;
                                        }

                                        if (minuteDiff > 0)
                                        {
                                            // Tăng sản lượng (Hướng 2): Cộng dồn toàn bộ vào mảnh cuối cùng (trễ nhất)
                                            var lastSplit = splitInfos.Last();
                                            lastSplit.Block.AllocatedMinutes += minuteDiff;
                                        }
                                        else
                                        {
                                            // Giảm sản lượng: Trừ lùi từ mảnh cuối cùng trở về trước
                                            double remainingToSubtract = Math.Abs(minuteDiff);
                                            for (int i = splitInfos.Count - 1; i >= 0; i--)
                                            {
                                                if (remainingToSubtract <= 0.01) break;
                                                
                                                var info = splitInfos[i];
                                                if (info.Block.AllocatedMinutes > remainingToSubtract)
                                                {
                                                    info.Block.AllocatedMinutes -= remainingToSubtract;
                                                    remainingToSubtract = 0;
                                                }
                                                else
                                                {
                                                    remainingToSubtract -= info.Block.AllocatedMinutes;
                                                    info.Day.Blocks.Remove(info.Block); // Xoá hẳn mảnh này khỏi lưới
                                                    info.Block.AllocatedMinutes = 0;
                                                }
                                            }
                                        }

                                        // Gọi dồn dòng cho các line bị ảnh hưởng.
                                        // RepackRowBlocks sẽ đọc các Block CÒN LẠI mượt mà từ trái qua phải, nhờ đó giữ nguyên trật tự của Sp1 với Sp2.
                                        foreach (var affectedRow in affectedRows)
                                        {
                                            RepackRowBlocks(affectedRow);
                                        }
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

        public event Action? RequestDeadlineDialog;
        public event Action? RequestConfigGroupsDialog;
        public event Action? RequestProductOrderSettingsDialog;
        public event Func<Dictionary<string, List<string>>?, Dictionary<string, List<string>>?>? RequestExportOrderDialog;

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
                            TotalMinutesRequired = split.TotalMinutesRequired,
                            AllocatedMinutes = totalOnThisRow,
                            DisplayColor = split.DisplayColor
                        };
                        
                        // Bỏ qua các khối có số phút <= 0 sau khi trừ
                        if (totalOnThisRow > 0.01)
                        {
                            orderedBlocks.Add(reconstructed);
                        }
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

                // Tái phân bổ blocks theo trạng thái IsDayOff hiện tại
                // (ví dụ: ngày nghỉ đã được bỏ → blocks cần dịch về ngày sớm hơn)
                RepackAll();

                // Cập nhật giao diện và tính toán deadline ngay sau khi nạp xong dữ liệu
                UpdateDaySummaries();
                UpdateLineDeadlines();
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

        [RelayCommand]
        private void ConfigProductOrder()
        {
            RequestProductOrderSettingsDialog?.Invoke();
        }

        [RelayCommand]
        private void ShowBuildGroupInfo()
        {
            var openMinutes = JsonStorage.Load<List<OpenMinutesData>>("openMinutes.json") ?? new();
            var products = JsonStorage.Load<List<ProductData>>("products.json") ?? new();

            var items = new List<Models.BuildGroupInfoItem>();
            foreach (var om in openMinutes)
            {
                if (om.OpenMinutes > 0)
                {
                    var prod = products.FirstOrDefault(p => p.ProductId == om.ProductId);
                    items.Add(new Models.BuildGroupInfoItem
                    {
                        GroupId = prod?.GroupId ?? "(không rõ)",
                        ProductId = om.ProductId,
                        OpenMinutes = om.OpenMinutes
                    });
                }
            }

            if (items.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu sản phẩm hoặc không có sản phẩm nào còn phút cần làm. Vui lòng Import MES trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.BuildGroupInfoDialog(items);
            dialog.ShowDialog();
        }

        [RelayCommand]
        private void ExportPlan()
        {
            try
            {
                // 1. Load dữ liệu cần thiết
                var products = JsonStorage.Load<List<ProductData>>("products.json") ?? new();
                var openMinutes = JsonStorage.Load<List<OpenMinutesData>>("openMinutes.json") ?? new();
                var productOrder = JsonStorage.Load<ProductOrderSettings>("productOrderSettings.json") ?? new();

                // 2. Kiểm tra có dữ liệu trên grid không
                bool hasAnyBlocks = Rows.Any(r => r.Days.Any(d => d.Blocks.Count > 0));
                if (!hasAnyBlocks)
                {
                    MessageBox.Show("Không có BuildGroup nào trên lưới. Vui lòng import dữ liệu và gán block trước khi export.",
                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2b. Cảnh báo nếu có SP vượt deadline
                var exceedingInfo = GetDeadlineExceedingInfo();
                if (exceedingInfo.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("⚠️ CÓ SẢN PHẨM ĐANG VƯỢT DEADLINE:");
                    sb.AppendLine();
                    foreach (var info in exceedingInfo)
                        sb.AppendLine($"• {info.ProductCode}: {info.ExceedMinutes:0.##} phút ({info.RowName})");
                    sb.AppendLine();
                    sb.AppendLine("Bạn có muốn tiếp tục export không?");

                    var warningResult = MessageBox.Show(sb.ToString(), "Cảnh báo Vượt Deadline",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (warningResult == MessageBoxResult.No) return;
                }

                // 3. Lấy danh sách BuildGroup per ca (mỗi Ca riêng - dùng tên Ca thật)
                var lineBlockData = new Dictionary<string, List<string>>();
                foreach (var row in Rows)
                {
                    var blockCodes = row.Days
                        .SelectMany(d => d.Blocks.Select(b => b.Code))
                        .Distinct()
                        .OrderBy(c => c)
                        .ToList();

                    if (blockCodes.Count > 0)
                        lineBlockData[row.RowName] = blockCodes;
                }

                // 4. Nếu có line nào >1 block → mở popup sắp xếp block
                Dictionary<string, List<string>>? blockOrder = null;
                bool needsBlockOrderPopup = lineBlockData.Any(kvp => kvp.Value.Count > 1);

                if (needsBlockOrderPopup)
                {
                    blockOrder = RequestExportOrderDialog?.Invoke(lineBlockData);
                    if (blockOrder == null)
                        return; // User đã hủy
                }
                else
                {
                    blockOrder = lineBlockData;
                }

                // 5. Mở Save dialog
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"KHSX_{DateTime.Now:dd-MM-yyyy}.xlsx",
                    Title = "Lưu file Kế Hoạch Sản Xuất"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var exportService = new ExcelExportService();
                    exportService.Export(
                        saveDialog.FileName,
                        Rows,
                        products,
                        openMinutes,
                        productOrder,
                        blockOrder ?? new(),
                        StartDate,
                        DeadlineDate);

                    MessageBox.Show($"Export thành công!\nFile: {saveDialog.FileName}",
                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi export: {ex.Message}",
                    "Lỗi Export", MessageBoxButton.OK, MessageBoxImage.Error);
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
        /// Xử lý kéo thả block VƯỢT deadline - chỉ di chuyển phần sau deadline.
        /// Nếu target line đã có block cùng ParentId → gộp toàn bộ để tránh duplicate.
        /// </summary>
        private void HandleDropExceedingBlock(ProductBlock droppedBlock, Guid parentId, DayCell targetDay, ShiftRow targetRow)
        {
            // ── Kiểm tra target line có block cùng ParentId không ──
            bool targetAlreadyHasSameBlock = targetRow.Days
                .Any(d => d.Blocks.Any(b => b.ParentId == parentId || b.Id == parentId));

            if (targetAlreadyHasSameBlock)
            {
                // GỘP SPLITS: block cùng ParentId đang nằm ở cả source lẫn target.
                // Mục tiêu: gộp thành 1 block duy nhất trên target, KHÔNG xáo trộn thứ tự SP khác.
                //
                // Cách: xóa tất cả splits của SP1 khỏi mọi row (source và target),
                // repack mọi row bị ảnh hưởng, rồi dùng HandleDropFullBlock-style
                // re-assign SP1 vào target với tổng phút đầy đủ.
                // → SP1 sẽ điền vào capacity còn trống của target theo thứ tự ngày tự nhiên,
                //   sau SP3-SP4 (vì SP3-SP4 đã chiếm các ngày đầu sau repack).

                // 1. Tính tổng phút SP1 từ MỌI row
                double totalMinutes = 0;
                var allSourceRows = new HashSet<ShiftRow>();
                foreach (var row in Rows)
                    foreach (var day in row.Days)
                        foreach (var b in day.Blocks)
                            if (b.ParentId == parentId || b.Id == parentId)
                            {
                                totalMinutes += b.AllocatedMinutes;
                                allSourceRows.Add(row);
                            }
                totalMinutes = Math.Round(totalMinutes, 2);

                // 2. Xóa toàn bộ SP1 khỏi MỌI row
                RemoveBlockFromGrid(parentId);

                // 3. Repack tất cả row bị ảnh hưởng (giữ đúng thứ tự SP3-SP4-SP2 trên source,
                //    và đúng thứ tự SP3-SP4-SP2 trên target nếu có)
                foreach (var srcRow in allSourceRows)
                    RepackRowBlocks(srcRow);

                // 4. Re-assign SP1 tổng vào target → điền capacity còn trống theo thứ tự ngày
                //    (sẽ đứng sau các block đã chiếm trước nếu không còn capacity trước đó)
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
                AssignBlockRecursively(reconstructedBlock, targetRow.Days[0], targetRow);

                MessageBox.Show($"Đã gộp toàn bộ {droppedBlock.Code} ({totalMinutes:0.##} phút) về {targetRow.RowName}.",
                    "Gộp block thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // ── KHÔNG có block cùng ParentId trên target: logic cũ (chỉ move phần sau-deadline) ──
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
                                minutesBeforeDeadline += b.AllocatedMinutes;
                            else
                                minutesAfterDeadline += b.AllocatedMinutes;
                        }
                    }
                }
            }

            minutesBeforeDeadline = Math.Round(minutesBeforeDeadline, 2);
            minutesAfterDeadline = Math.Round(minutesAfterDeadline, 2);

            if (minutesAfterDeadline <= 0.01)
            {
                MessageBox.Show("Không có phút nào sau deadline để di chuyển.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Xóa CHỈ các block SAU deadline (giữ nguyên phần trước deadline)
            RemoveBlocksAfterDeadline(parentId);

            // Repack line nguồn để các block phía sau dồn lên lấp chỗ trống
            var sourceRows = Rows.Where(r =>
                r.Days.Any(d => d.Blocks.Any(b => b.ParentId == parentId || b.Id == parentId))).ToList();
            foreach (var srcRow in sourceRows)
            {
                if (srcRow != targetRow)
                    RepackRowBlocks(srcRow);
            }

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

            bool isSameLine = sourceRows.Count == 1 && sourceRows.Contains(targetRow);

            if (isSameLine)
            {
                // === KÉO TRONG CÙNG LINE = ĐỔI THỨ TỰ ===
                // Thu thập tất cả block khác (không phải block đang kéo) theo thứ tự hiện có
                var otherBlocks = new List<ProductBlock>();
                var processedOtherIds = new HashSet<Guid>();

                foreach (var day in targetRow.Days)
                {
                    foreach (var b in day.Blocks.ToList())
                    {
                        var bParent = b.ParentId ?? b.Id;
                        if (bParent == parentId || b.Id == parentId) continue; // Bỏ qua block đang kéo
                        if (processedOtherIds.Contains(bParent)) continue;
                        processedOtherIds.Add(bParent);

                        // Tính tổng allocated cho block này
                        double otherTotal = 0;
                        foreach (var d in targetRow.Days)
                        {
                            foreach (var ob in d.Blocks)
                            {
                                if ((ob.ParentId ?? ob.Id) == bParent)
                                    otherTotal += ob.AllocatedMinutes;
                            }
                        }

                        var clone = new ProductBlock
                        {
                            Id = Guid.NewGuid(),
                            ParentId = bParent,
                            SourceId = b.SourceId,
                            Code = b.Code,
                            ProductionGroup = b.ProductionGroup,
                            FunctionName = b.FunctionName,
                            TotalMinutesRequired = b.TotalMinutesRequired,
                            AllocatedMinutes = Math.Round(otherTotal, 2),
                            DisplayColor = b.DisplayColor
                        };
                        otherBlocks.Add(clone);
                    }
                }

                // Xoá toàn bộ block trên line
                foreach (var day in targetRow.Days)
                    day.Blocks.Clear();

                // Assign block ĐƯỢC KÉO trước (lên đầu line)
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
                AssignBlockRecursively(reconstructedBlock, targetRow.Days[0], targetRow);

                // Assign các block còn lại SAU (giữ thứ tự cũ giữa chúng)
                foreach (var other in otherBlocks)
                {
                    AssignBlockRecursively(other, targetRow.Days[0], targetRow);
                }
            }
            else
            {
                // === KÉO KHÁC LINE (logic cũ) ===
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



