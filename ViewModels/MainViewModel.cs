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
        private bool _isBatchingCapacityWarnings = false;
        private readonly List<string> _capacityWarnings = new();

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
        private void ImportModuleList()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm;*.xlsb",
                Title = "Chọn file Module List"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var result = _excelService.ImportModuleList(dialog.FileName);
                var message = $"Import Module List thành công: {result.ImportedCount} dòng mapping.";
                if (result.HasWarnings)
                    message += "\n\nCảnh báo:\n" + string.Join("\n", result.Warnings);
                MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi import Module List: {ex.Message}", "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ImportPlanning()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm;*.xlsb",
                Title = "Chọn file Planning"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var result = ImportPlanningWithMissingFpRetry(dialog.FileName);
                if (result == null) return;

                MessageBox.Show($"Import Planning thành công: {result.ImportedCount} block theo BuildGroup + Gr.xxx.",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi import Planning: {ex.Message}", "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ImportResult? ImportPlanningWithMissingFpRetry(string fileName)
        {
            var result = _excelService.ImportPlanning(fileName);
            if (!result.HasMissingFps) return result;

            var manualMappings = RequestMissingFpMappingsDialog?.Invoke(result.MissingFps);
            if (manualMappings == null || manualMappings.Count == 0) return null;

            _excelService.SaveManualMappings(manualMappings);
            var retry = _excelService.ImportPlanning(fileName);
            if (!retry.HasMissingFps) return retry;

            MessageBox.Show("Vẫn còn FP chưa có mapping: " + string.Join(", ", retry.MissingFps),
                "Thiếu Mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        private ImportResult? ImportMesWithMissingFpRetry(string fileName)
        {
            var result = _excelService.ImportMES(fileName);
            if (!result.HasMissingFps) return result;

            var manualMappings = RequestMissingFpMappingsDialog?.Invoke(result.MissingFps);
            if (manualMappings == null || manualMappings.Count == 0) return null;

            _excelService.SaveManualMappings(manualMappings);
            var retry = _excelService.ImportMES(fileName);
            if (!retry.HasMissingFps) return retry;

            MessageBox.Show("Vẫn còn FP chưa có mapping: " + string.Join(", ", retry.MissingFps),
                "Thiếu Mapping", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        [RelayCommand]
        private void ImportMarketing()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm;*.xlsb",
                Title = "Chọn file Planning"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var result = ImportPlanningWithMissingFpRetry(dialog.FileName);
                    if (result == null) return;
                    MessageBox.Show($"Import Planning thành công: {result.ImportedCount} block theo BuildGroup + Gr.xxx.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi import Planning: {ex.Message}", "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ImportMES()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xls;*.xlsx;*.xlsm;*.xlsb",
                Title = "Chọn file MES/OpenMin"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var importResult = ImportMesWithMissingFpRetry(dialog.FileName);
                    if (importResult == null) return;

                    var generation = _excelService.GenerateBlocksFromDataV2();
                    if (generation.MissingDeadlineGroups.Count > 0)
                    {
                        MessageBox.Show(
                            "Thiếu deadline cho Gr.xxx sau, vui lòng cấu hình đủ trước khi auto schedule:\n" +
                            string.Join(", ", generation.MissingDeadlineGroups),
                            "Thiếu Deadline",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        RequestDeadlineDialog?.Invoke();
                        return;
                    }

                    AutoScheduleBlocks(generation.Blocks);

                    var message = $"Đã import MES/OpenMin và auto schedule {generation.Blocks.Count} block.";
                    if (generation.Warnings.Count > 0)
                        message += "\n\nCảnh báo:\n" + string.Join("\n", generation.Warnings);

                    MessageBox.Show(message, "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    SaveConfiguration();

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
                var dl = _customDeadlines.FirstOrDefault(d => 
                    string.Equals(d.GroupNumber, block.ProductionGroup, StringComparison.OrdinalIgnoreCase));
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
        public event Func<List<string>, List<ModuleMappingData>?>? RequestMissingFpMappingsDialog;
        public event Action? RequestProductOrderSettingsDialog;
        public event Func<Dictionary<string, List<string>>?, Dictionary<string, List<string>>?>? RequestExportOrderDialog;

        private void AutoScheduleBlocks(List<ProductBlock> blocks)
        {
            LoadCustomDeadlines();
            EnsureFunctionRows(blocks);

            UnassignedBlocks.Clear();
            foreach (var row in Rows)
                foreach (var day in row.Days)
                    day.Blocks.Clear();

            var settings = JsonStorage.Load<List<BuildGroupShiftSettingData>>("buildGroupSettings.json")
                .Where(s => !string.IsNullOrWhiteSpace(s.BuildGroup))
                .ToDictionary(s => s.BuildGroup, s => s, StringComparer.OrdinalIgnoreCase);
            foreach (var block in blocks
                .OrderBy(b => GetDeadlineForBlock(b))
                .ThenBy(b => b.ProductionGroup)
                .ThenBy(b => b.Code))
            {
                if (!settings.TryGetValue(block.Code, out var setting))
                {
                    setting = new BuildGroupShiftSettingData
                    {
                        BuildGroup = block.Code,
                        FunctionName = block.FunctionName,
                        UseShiftA = true,
                        UseShiftB = false,
                        WorkersA = 1,
                        WorkersB = 1
                    };
                }

                var functionName = string.IsNullOrWhiteSpace(setting.FunctionName) ? block.FunctionName : setting.FunctionName;
                if (string.IsNullOrWhiteSpace(functionName)) functionName = block.Code;

                ScheduleWholeBlockOnDeadline(block, setting);
            }

            NormalizeVisibleRows();

            foreach (var row in Rows)
                foreach (var day in row.Days)
                    CheckBlockExceeding(day);

            UpdateDaySummaries();
            UpdateLineDeadlines();
        }

        private void EnsureFunctionRows(List<ProductBlock> blocks)
        {
            var settings = JsonStorage.Load<List<BuildGroupShiftSettingData>>("buildGroupSettings.json")
                .Where(s => !string.IsNullOrWhiteSpace(s.BuildGroup))
                .ToDictionary(s => s.BuildGroup, s => s, StringComparer.OrdinalIgnoreCase);

            var required = blocks
                .Select(b =>
                {
                    settings.TryGetValue(b.Code, out var setting);
                    var functionName = setting?.FunctionName ?? b.FunctionName;
                    var useA = setting?.UseShiftA ?? true;
                    var useB = setting?.UseShiftB ?? false;
                    if (!useA && !useB) useA = true;

                    return new
                    {
                        FunctionName = string.IsNullOrWhiteSpace(functionName) ? b.Code : functionName,
                        UseShiftA = useA,
                        UseShiftB = useB,
                        WorkersA = setting?.WorkersA ?? 1,
                        WorkersB = setting?.WorkersB ?? 1
                    };
                })
                .GroupBy(x => x.FunctionName, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    FunctionName = g.First().FunctionName,
                    UseShiftA = g.Any(x => x.UseShiftA),
                    UseShiftB = g.Any(x => x.UseShiftB),
                    WorkersA = g.Where(x => x.UseShiftA).Select(x => x.WorkersA).DefaultIfEmpty(1).Max(),
                    WorkersB = g.Where(x => x.UseShiftB).Select(x => x.WorkersB).DefaultIfEmpty(1).Max()
                })
                .OrderBy(x => x.FunctionName)
                .ToList();

            Rows.Clear();
            int displayIndex = 1;
            foreach (var item in required)
            {
                var functionName = item.FunctionName;
                var useA = item.UseShiftA;
                var useB = item.UseShiftB;
                if (!useA && !useB) useA = true;

                if (useA)
                    Rows.Add(CreateFunctionRow(functionName, "A", displayIndex, item.WorkersA));
                if (useB)
                    Rows.Add(CreateFunctionRow(functionName, "B", displayIndex, item.WorkersB));
                displayIndex++;
            }
        }

        private ShiftRow CreateFunctionRow(string functionName, string shiftName, int displayIndex, double workers)
        {
            var row = new ShiftRow(functionName, shiftName) { DisplayIndex = displayIndex.ToString() };
            row.DefaultConfig.Workers = workers > 0 ? workers : 1;
            row.DefaultConfig.Minutes = 480;
            row.DefaultConfig.Efficiency = 1.15;

            for (int d = 0; d < 30; d++)
            {
                var day = new DayCell(StartDate.AddDays(d));
                day.Config.Workers = row.DefaultConfig.Workers;
                day.Config.Minutes = row.DefaultConfig.Minutes;
                day.Config.Efficiency = row.DefaultConfig.Efficiency;
                day.HasCustomConfig = false;
                row.Days.Add(day);
            }

            return row;
        }

        private ShiftRow ChooseTargetRow(ProductBlock block, BuildGroupShiftSettingData setting, DateTime targetDate)
        {
            var functionName = string.IsNullOrWhiteSpace(setting.FunctionName) ? block.FunctionName : setting.FunctionName;
            var rowA = Rows.FirstOrDefault(r =>
                string.Equals(r.ParentLineName, functionName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.ShiftName, "A", StringComparison.OrdinalIgnoreCase));
            var rowB = Rows.FirstOrDefault(r =>
                string.Equals(r.ParentLineName, functionName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.ShiftName, "B", StringComparison.OrdinalIgnoreCase));

            if (setting.UseShiftA && !setting.UseShiftB) return rowA ?? rowB ?? Rows.First();
            if (!setting.UseShiftA && setting.UseShiftB) return rowB ?? rowA ?? Rows.First();

            if (rowA == null) return rowB ?? Rows.First();
            if (rowB == null) return rowA;

            var dayA = EnsureDateExists(rowA, targetDate);
            return dayA.TotalUsed + block.AllocatedMinutes <= dayA.TotalCapacity
                ? rowA
                : rowB;
        }

        private DateTime ScheduleBlockByCapacity(ProductBlock block, BuildGroupShiftSettingData setting, DateTime startDate)
        {
            var rows = GetSchedulableRows(block, setting);
            if (rows.Count == 0) return startDate;

            var remaining = Math.Round(block.AllocatedMinutes, 2);
            var currentDate = startDate.Date;
            var guardDays = 0;

            while (remaining > 0.01 && guardDays < 3650)
            {
                DayCell? lastWorkDay = null;

                foreach (var row in rows)
                {
                    var day = EnsureDateExists(row, currentDate);
                    if (day.IsDayOff) continue;

                    lastWorkDay = day;
                    var available = Math.Round(day.AvailableMinutes, 2);
                    if (available <= 0.01) continue;

                    var minutes = Math.Min(remaining, available);
                    day.Blocks.Add(block.CloneWithSplit(minutes));
                    remaining = Math.Round(remaining - minutes, 2);

                    if (remaining <= 0.01) break;
                }

                if (remaining <= 0.01)
                    return currentDate;

                currentDate = currentDate.AddDays(1);
                guardDays++;
            }

            if (remaining > 0.01)
            {
                UnassignedBlocks.Add(block.CloneWithSplit(remaining));
            }

            return currentDate;
        }

        private DateTime ScheduleWholeBlockOnDeadline(ProductBlock block, BuildGroupShiftSettingData setting)
        {
            var targetDate = GetDeadlineForBlock(block);
            if (targetDate == DateTime.MaxValue)
                targetDate = StartDate.Date;

            var targetRow = ChooseTargetRow(block, setting, targetDate);
            var targetDay = EnsureDateExists(targetRow, targetDate);
            var scheduledBlock = block.CloneWithSplit(block.AllocatedMinutes);
            scheduledBlock.IsCapacityOverflow = targetDay.TotalUsed + scheduledBlock.AllocatedMinutes > targetDay.TotalCapacity + 0.01;
            scheduledBlock.IsExceedingDeadline = targetDay.Date.Date > GetDeadlineForBlock(scheduledBlock);

            targetDay.Blocks.Add(scheduledBlock);
            CheckBlockExceeding(targetDay);
            return targetDate;
        }

        private List<ShiftRow> GetSchedulableRows(ProductBlock block, BuildGroupShiftSettingData setting)
        {
            var functionName = string.IsNullOrWhiteSpace(setting.FunctionName) ? block.FunctionName : setting.FunctionName;
            var functionRows = Rows
                .Where(r => string.Equals(r.ParentLineName, functionName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.ShiftName)
                .ToList();

            if (functionRows.Count > 1)
                return functionRows;

            var useA = setting.UseShiftA;
            var useB = setting.UseShiftB;
            if (!useA && !useB) useA = true;

            return functionRows
                .Where(r => (useA && string.Equals(r.ShiftName, "A", StringComparison.OrdinalIgnoreCase)) ||
                            (useB && string.Equals(r.ShiftName, "B", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(r => r.ShiftName)
                .ToList();
        }

        private void RemoveRowsWithoutBlocks()
        {
            foreach (var row in Rows.Where(r => !r.Days.SelectMany(d => d.Blocks).Any()).ToList())
            {
                Rows.Remove(row);
            }
        }

        private void NormalizeVisibleRows()
        {
            MergeDuplicateRows();
            RemoveRowsWithoutBlocks();
        }

        private void MergeDuplicateRows()
        {
            var duplicateGroups = Rows
                .GroupBy(r => $"{r.ParentLineName}|{r.ShiftName}", StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in duplicateGroups)
            {
                var target = group.First();
                foreach (var duplicate in group.Skip(1).ToList())
                {
                    foreach (var sourceDay in duplicate.Days)
                    {
                        var targetDay = EnsureDateExists(target, sourceDay.Date.Date);
                        if (sourceDay.HasCustomConfig && !targetDay.HasCustomConfig)
                        {
                            targetDay.HasCustomConfig = true;
                            targetDay.IsDayOff = sourceDay.IsDayOff;
                            targetDay.Config.Workers = sourceDay.Config.Workers;
                            targetDay.Config.Minutes = sourceDay.Config.Minutes;
                            targetDay.Config.Efficiency = sourceDay.Config.Efficiency;
                        }

                        foreach (var block in sourceDay.Blocks.ToList())
                        {
                            targetDay.Blocks.Add(block);
                            sourceDay.Blocks.Remove(block);
                        }
                    }

                    Rows.Remove(duplicate);
                }
            }
        }

        private void BeginCapacityWarningBatch()
        {
            _capacityWarnings.Clear();
            _isBatchingCapacityWarnings = true;
        }

        private void FlushCapacityWarningBatch()
        {
            _isBatchingCapacityWarnings = false;
            if (_capacityWarnings.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("Một số sản phẩm chưa xếp hết do không đủ công suất:");
            sb.AppendLine();
            foreach (var warning in _capacityWarnings.Distinct())
                sb.AppendLine("- " + warning);

            MessageBox.Show(sb.ToString(), "Cảnh báo công suất", MessageBoxButton.OK, MessageBoxImage.Warning);
            _capacityWarnings.Clear();
        }

        private void ReportInsufficientCapacity(ProductBlock block, double remainingMinutes)
        {
            var message = $"{block.Code} ({block.ProductionGroup}): còn {remainingMinutes:0.##} phút chưa được xếp lịch";
            if (_isBatchingCapacityWarnings)
            {
                _capacityWarnings.Add(message);
                return;
            }

            MessageBox.Show($"Line này không đủ công suất cho sản phẩm {block.Code}. Còn thừa {remainingMinutes:0.##} phút chưa được xếp lịch.",
                          "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private DayCell EnsureDateExists(ShiftRow row, DateTime date)
        {
            var existing = row.Days.FirstOrDefault(d => d.Date.Date == date.Date);
            if (existing != null) return existing;

            DayCell CreateDay(DateTime dayDate)
            {
                var day = new DayCell(dayDate);
                day.Config.Workers = row.DefaultConfig.Workers;
                day.Config.Minutes = row.DefaultConfig.Minutes;
                day.Config.Efficiency = row.DefaultConfig.Efficiency;
                return day;
            }

            var minDate = row.Days.Count > 0 ? row.Days.Min(d => d.Date.Date) : StartDate.Date.AddDays(1);
            while (minDate > date.Date)
            {
                minDate = minDate.AddDays(-1);
                row.Days.Insert(0, CreateDay(minDate));
            }

            var maxDate = row.Days.Count > 0 ? row.Days.Max(d => d.Date.Date) : StartDate.Date.AddDays(-1);
            while (maxDate < date.Date)
            {
                maxDate = maxDate.AddDays(1);
                row.Days.Add(CreateDay(maxDate));
            }

            return row.Days.First(d => d.Date.Date == date.Date);
        }

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

            var cursorDate = row.Days.First().Date.Date;
            foreach (var block in orderedBlocks)
            {
                cursorDate = AssignBlockKeepingOverflowInDay(block, row, cursorDate);
            }
        }

        public void RepackRowBlocksKeepingOverflowInDay(ShiftRow row, DayCell anchorDay)
        {
            if (row == null || anchorDay == null || row.Days.Count == 0) return;

            var orderedBlocks = CollectAndClearRowBlocks(row);
            var cursorDate = row.Days.First().Date.Date;
            var anchorDate = anchorDay.Date.Date;

            foreach (var block in orderedBlocks)
            {
                cursorDate = AssignBlockWithOverflowAnchor(block, row, cursorDate, anchorDate);
            }
        }

        private List<ProductBlock> CollectAndClearRowBlocks(ShiftRow row)
        {
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

                        if (totalOnThisRow > 0.01)
                        {
                            orderedBlocks.Add(reconstructed);
                        }
                    }
                    day.Blocks.Remove(split);
                }
            }

            return orderedBlocks;
        }

        private DateTime AssignBlockKeepingOverflowInDay(ProductBlock block, ShiftRow row, DateTime startDate)
        {
            var remaining = Math.Round(block.AllocatedMinutes, 2);
            var currentDate = startDate.Date;
            var guardDays = 0;

            while (remaining > 0.01 && guardDays < 3650)
            {
                var day = EnsureDateExists(row, currentDate);
                if (day.IsDayOff)
                {
                    currentDate = currentDate.AddDays(1);
                    guardDays++;
                    continue;
                }

                var available = Math.Round(day.AvailableMinutes, 2);
                if (available > 0.01)
                {
                    var minutes = Math.Round(Math.Min(available, remaining), 2);
                    var splitBlock = block.CloneWithSplit(minutes);
                    splitBlock.IsExceedingDeadline = day.Date > GetDeadlineForBlock(splitBlock);
                    day.Blocks.Add(splitBlock);

                    remaining = Math.Round(remaining - minutes, 2);
                }

                if (remaining <= 0.01)
                {
                    CheckBlockExceeding(day);
                    return currentDate;
                }

                currentDate = currentDate.AddDays(1);
                guardDays++;
            }

            if (remaining > 0.01)
            {
                ReportInsufficientCapacity(block, remaining);
                UnassignedBlocks.Add(block.CloneWithSplit(remaining));
            }

            return currentDate;
        }

        private DateTime AssignBlockWithOverflowAnchor(ProductBlock block, ShiftRow row, DateTime startDate, DateTime anchorDate)
        {
            var remaining = Math.Round(block.AllocatedMinutes, 2);
            var currentDate = startDate.Date;
            var guardDays = 0;

            while (remaining > 0.01 && guardDays < 3650)
            {
                var day = EnsureDateExists(row, currentDate);
                if (day.IsDayOff)
                {
                    currentDate = currentDate.AddDays(1);
                    guardDays++;
                    continue;
                }

                var available = Math.Round(day.AvailableMinutes, 2);

                if (available > 0.01)
                {
                    var minutes = Math.Round(Math.Min(available, remaining), 2);
                    var splitBlock = block.CloneWithSplit(minutes);
                    splitBlock.IsExceedingDeadline = day.Date > GetDeadlineForBlock(splitBlock);
                    day.Blocks.Add(splitBlock);

                    remaining = Math.Round(remaining - minutes, 2);
                }

                if (remaining <= 0.01)
                {
                    CheckBlockExceeding(day);
                    return currentDate;
                }

                if (currentDate == anchorDate)
                {
                    var overflow = block.CloneWithSplit(remaining);
                    overflow.IsCapacityOverflow = true;
                    overflow.IsExceedingDeadline = true;
                    day.Blocks.Add(overflow);
                    CheckBlockExceeding(day);
                    return currentDate.AddDays(1);
                }

                currentDate = currentDate.AddDays(1);
                guardDays++;
            }

            if (remaining > 0.01)
            {
                ReportInsufficientCapacity(block, remaining);
                UnassignedBlocks.Add(block.CloneWithSplit(remaining));
            }

            return currentDate;
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
        private void Load()
        {
            LoadConfiguration();
            SaveConfiguration();
            MessageBox.Show("Đã load lại dữ liệu và tính lại lịch.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    IsCapacityOverflow = b.IsCapacityOverflow,
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
                                    IsCapacityOverflow = block.IsCapacityOverflow,
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
                BeginCapacityWarningBatch();
                RepackAll();
                NormalizeVisibleRows();

                // Cập nhật giao diện và tính toán deadline ngay sau khi nạp xong dữ liệu
                UpdateDaySummaries();
                UpdateLineDeadlines();
                FlushCapacityWarningBatch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phần khôi phục dữ liệu hệ thống: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBatchingCapacityWarnings = false;
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
                        .ToList(); // Giữ thứ tự xuất hiện trên lưới (không sắp theo tên)

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
                        DeadlineDate,
                        _customDeadlines);

                    MessageBox.Show($"Xuất file thành công!\nFile: {saveDialog.FileName}",
                        "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file: {ex.Message}",
                    "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public void HandleDropCellGroup(DayCell sourceDay, DayCell targetDay, ShiftRow targetRow)
        {
            if (sourceDay == null || targetDay == null || targetRow == null || sourceDay == targetDay)
                return;

            var sourceRow = Rows.FirstOrDefault(r => r.Days.Contains(sourceDay));
            var movedBlocks = sourceDay.Blocks.ToList();
            if (movedBlocks.Count == 0) return;

            sourceDay.Blocks.Clear();
            foreach (var block in movedBlocks)
            {
                targetDay.Blocks.Add(block);
            }

            if (sourceRow != null) CheckBlockExceeding(sourceDay);
            CheckBlockExceeding(targetDay);
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
                ReportInsufficientCapacity(block, remainingMin);
                
                var unassignedPortion = block.CloneWithSplit(remainingMin);
                UnassignedBlocks.Add(unassignedPortion);
            }
        }

        private void CheckBlockExceeding(DayCell day)
        {
            bool isOverCapacity = day.TotalUsed > day.TotalCapacity + 0.01;
            day.IsOverCapacity = isOverCapacity;
            foreach (var block in day.Blocks)
            {
                block.IsExceedingDeadline = block.IsCapacityOverflow || day.Date > GetDeadlineForBlock(block);
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
            sb.AppendLine("💡 MẸO: Kéo BuildGroup viền đỏ sang line khác để điều phối.");
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
