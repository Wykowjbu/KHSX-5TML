using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using KHSX.Models;

namespace KHSX.Services
{
    /// <summary>
    /// Service xuất kế hoạch sản xuất ra file Excel (.xlsx).
    /// Sheet "Tổng Quan" + mỗi Line (ParentLineName) 1 sheet, chia Ca A trên Ca B dưới.
    /// Chế độ duy nhất: Tuần tự (Sequential).
    /// blockOrder: Key = RowName (tên Ca), Value = danh sách BuildGroup code theo thứ tự.
    /// </summary>
    public class ExcelExportService
    {
        public class DailyProductAllocation
        {
            public string ProductId { get; set; } = string.Empty;
            public string GroupId { get; set; } = string.Empty;
            public double MinutesUsed { get; set; }
            public int ProductCount { get; set; }
            public bool IsCompleted { get; set; }
        }

        public class DailyAllocation
        {
            public DateTime Date { get; set; }
            public bool IsDayOff { get; set; }
            public double TotalCapacity { get; set; }
            public double Workers { get; set; }
            public List<DailyProductAllocation> Products { get; set; } = new();
        }

        /// <summary>
        /// Export kế hoạch sản xuất ra file Excel.
        /// blockOrder: Key = RowName (tên Ca thật), Value = thứ tự BuildGroup.
        /// </summary>
        public void Export(
            string filePath,
            IEnumerable<ShiftRow> rows,
            List<ProductData> products,
            List<OpenMinutesData> openMinutes,
            ProductOrderSettings productOrder,
            Dictionary<string, List<string>> blockOrder,
            DateTime startDate,
            DateTime deadlineDate,
            List<DeadlineData>? deadlines = null)
        {
            using var workbook = new XLWorkbook();

            // Nhóm theo ParentLineName → mỗi Line 1 sheet
            var lineGroups = rows
                .GroupBy(r => r.ParentLineName)
                .OrderBy(g => g.Key)
                .ToList();

            // Load productGroups để tra ProductionGroup của BuildGroup
            var productGroups = JsonStorage.Load<List<ProductGroupData>>("productGroups.json") ?? new List<ProductGroupData>();

            // Sheet Tổng Quan
            var summarySheet = workbook.AddWorksheet("Tổng Quan");
            WriteSummarySheet(summarySheet, lineGroups, products, openMinutes, startDate, deadlineDate);

            // Sheet chi tiết từng Line (Ca A trên, Ca B dưới)
            foreach (var lineGroup in lineGroups)
            {
                bool hasBlocks = lineGroup.Any(r => r.Days.Any(d => d.Blocks.Count > 0));
                if (!hasBlocks) continue;

                var sheetName = SanitizeSheetName(lineGroup.Key);
                var sheet = workbook.AddWorksheet(sheetName);
                WriteLineSheet(sheet, lineGroup, products, openMinutes, productOrder, blockOrder, startDate, deadlineDate, deadlines ?? new(), productGroups);
            }

            workbook.SaveAs(filePath);
        }

        /// <summary>Lấy deadline của 1 BuildGroup dựa theo ProductionGroup của nó.</summary>
        private DateTime? GetBuildGroupDeadline(string buildGroupCode, List<DeadlineData> deadlines, List<ProductGroupData> productGroups)
        {
            if (deadlines == null || deadlines.Count == 0) return null;
            var pg = productGroups.FirstOrDefault(g => g.GroupId == buildGroupCode);
            if (pg == null || string.IsNullOrEmpty(pg.ProductionGroup)) return null;
            var dl = deadlines.FirstOrDefault(d => d.GroupNumber == pg.ProductionGroup);
            return dl?.Deadline.Date;
        }

        private string SanitizeSheetName(string name)
        {
            if (name.Length > 31) name = name[..31];
            foreach (var c in new[] { '\\', '/', '*', '[', ']', ':', '?' })
                name = name.Replace(c, '_');
            return name;
        }

        // ─────────────────────────────────────────
        // SHEET: TỔNG QUAN
        // ─────────────────────────────────────────
        private void WriteSummarySheet(
            IXLWorksheet sheet,
            List<IGrouping<string, ShiftRow>> lineGroups,
            List<ProductData> products,
            List<OpenMinutesData> openMinutes,
            DateTime startDate,
            DateTime deadlineDate)
        {
            sheet.Cell(1, 1).Value = "KẾ HOẠCH SẢN XUẤT";
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 16;
            sheet.Range(1, 1, 1, 5).Merge();

            sheet.Cell(2, 1).Value = $"Từ {startDate:dd/MM/yyyy} đến {deadlineDate:dd/MM/yyyy}";
            sheet.Cell(2, 1).Style.Font.Italic = true;
            sheet.Range(2, 1, 2, 5).Merge();

            int headerRow = 4;
            var headers = new[] { "Line", "Ca", "BuildGroup(s)", "Tổng SP", "Chi tiết" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int dataRow = headerRow + 1;
            foreach (var lineGroup in lineGroups)
            {
                bool hasBlocks = lineGroup.Any(r => r.Days.Any(d => d.Blocks.Count > 0));
                if (!hasBlocks) continue;

                string lineName = lineGroup.Key;
                var sanitizedName = SanitizeSheetName(lineName);

                bool firstRow = true;
                foreach (var shiftRow in lineGroup.OrderBy(r => r.RowName))
                {
                    var buildGroups = shiftRow.Days
                        .SelectMany(d => d.Blocks.Select(b => b.Code))
                        .Distinct().OrderBy(c => c).ToList();
                    if (buildGroups.Count == 0) continue;

                    int totalSP = CalculateTotalSPForRow(shiftRow, products, openMinutes);

                    sheet.Cell(dataRow, 1).Value = firstRow ? lineName : "";
                    sheet.Cell(dataRow, 2).Value = shiftRow.RowName;
                    sheet.Cell(dataRow, 3).Value = string.Join(", ", buildGroups);
                    sheet.Cell(dataRow, 4).Value = totalSP;
                    sheet.Cell(dataRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    sheet.Cell(dataRow, 5).Value = "👉 Chi tiết";
                    sheet.Cell(dataRow, 5).SetHyperlink(new XLHyperlink($"'{sanitizedName}'!A1"));
                    sheet.Cell(dataRow, 5).Style.Font.FontColor = XLColor.Blue;
                    sheet.Cell(dataRow, 5).Style.Font.Underline = XLFontUnderlineValues.Single;

                    firstRow = false;
                    dataRow++;
                }
            }

            sheet.Columns().AdjustToContents();
        }

        // ─────────────────────────────────────────
        // SHEET: CHI TIẾT 1 LINE (Ca A trên / Ca B dưới)
        // ─────────────────────────────────────────
        private void WriteLineSheet(
            IXLWorksheet sheet,
            IGrouping<string, ShiftRow> lineGroup,
            List<ProductData> products,
            List<OpenMinutesData> openMinutes,
            ProductOrderSettings productOrder,
            Dictionary<string, List<string>> blockOrder,
            DateTime startDate,
            DateTime deadlineDate,
            List<DeadlineData> deadlines,
            List<ProductGroupData> productGroups)
        {
            int colStart = 2; // Cột A = label, Cột B+ = ngày
            int row = 1;

            // Title
            sheet.Cell(row, 1).Value = lineGroup.Key;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 14;
            row++;

            // Link về Tổng Quan
            sheet.Cell(row, 1).Value = "← Về Tổng Quan";
            sheet.Cell(row, 1).SetHyperlink(new XLHyperlink("'Tổng Quan'!A1"));
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Blue;
            sheet.Cell(row, 1).Style.Font.Underline = XLFontUnderlineValues.Single;
            row += 2;

            // Tính phạm vi ngày: mở rộng đến ngày có block cuối cùng nếu vượt deadline
            var lastBlockDate = lineGroup
                .SelectMany(r => r.Days)
                .Where(d => d.Blocks.Count > 0)
                .Select(d => d.Date.Date)
                .DefaultIfEmpty(deadlineDate.Date)
                .Max();
            var effectiveEndDate = lastBlockDate > deadlineDate.Date ? lastBlockDate : deadlineDate.Date;
            int totalDays = (int)(effectiveEndDate - startDate.Date).TotalDays + 1;

            // Ghi từng Ca trong Line
            bool firstShift = true;
            foreach (var shiftRow in lineGroup.OrderBy(r => r.RowName))
            {
                bool hasBlocks = shiftRow.Days.Any(d => d.Blocks.Count > 0);
                if (!hasBlocks) continue;

                if (!firstShift)
                {
                    // Dòng trống ngăn cách giữa các Ca
                    row++;
                }
                firstShift = false;

                // Tính phân bổ cho Ca này
                var allocations = CalculateSequentialAllocationForRow(
                    shiftRow, products, openMinutes, productOrder, blockOrder, startDate, deadlineDate, totalDays);

                // Lấy thứ tự BuildGroup
                var orderedBuildGroups = GetOrderedBuildGroupsForRow(shiftRow, blockOrder);
                var spByGroup = GetProductsByBuildGroup(orderedBuildGroups, products, openMinutes, productOrder);

                // === Header Ca ===
                var caHeader = sheet.Cell(row, 1);
                caHeader.Value = $"🔵 {shiftRow.RowName}";
                caHeader.Style.Font.Bold = true;
                caHeader.Style.Font.FontSize = 12;
                caHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                caHeader.Style.Font.FontColor = XLColor.White;
                for (int d = 0; d < allocations.Count; d++)
                    sheet.Cell(row, colStart + d).Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                row++;

                // Header ngày
                for (int d = 0; d < allocations.Count; d++)
                {
                    var alloc = allocations[d];
                    var cell = sheet.Cell(row, colStart + d);
                    var dayName = GetVietnameseDayName(alloc.Date.DayOfWeek);
                    cell.Value = $"{dayName} ({alloc.Date:dd/MM})";
                    cell.Style.Font.Bold = true;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Fill.BackgroundColor = alloc.IsDayOff ? XLColor.LightGray : XLColor.LightSteelBlue;
                }
                row++;

                // Tính deadline dict: BuildGroupCode → ngày deadline
                var bgDeadlineMap = new Dictionary<string, DateTime?>();
                foreach (var bgCode in orderedBuildGroups)
                    bgDeadlineMap[bgCode] = GetBuildGroupDeadline(bgCode, deadlines, productGroups);

                // Dữ liệu từng BuildGroup
                foreach (var buildGroupCode in orderedBuildGroups)
                {
                    // Deadline của BuildGroup này
                    DateTime? bgDeadline = bgDeadlineMap.TryGetValue(buildGroupCode, out var dl) ? dl : null;

                    // Màu nền: vàng = trong deadline, cam nhạt = sau deadline
                    var headerBgNormal = XLColor.LightYellow;
                    var headerBgOver = XLColor.FromHtml("#FFE0B2"); // cam nhạt

                    // Header BuildGroup
                    sheet.Cell(row, 1).Value = $"BuildGroup: {buildGroupCode}";
                    sheet.Cell(row, 1).Style.Font.Bold = true;
                    sheet.Cell(row, 1).Style.Fill.BackgroundColor = headerBgNormal;
                    for (int d = 0; d < allocations.Count; d++)
                    {
                        bool isOver = bgDeadline.HasValue && allocations[d].Date.Date > bgDeadline.Value;
                        sheet.Cell(row, colStart + d).Style.Fill.BackgroundColor = isOver ? headerBgOver : headerBgNormal;
                    }
                    row++;

                    // Phút phân bổ
                    sheet.Cell(row, 1).Value = "Phút phân bổ";
                    sheet.Cell(row, 1).Style.Font.Italic = true;
                    for (int d = 0; d < allocations.Count; d++)
                    {
                        var alloc = allocations[d];
                        bool isOver = bgDeadline.HasValue && alloc.Date.Date > bgDeadline.Value;
                        double bgMin = alloc.IsDayOff ? 0 :
                            alloc.Products.Where(p => p.GroupId == buildGroupCode).Sum(p => p.MinutesUsed);
                        var cell = sheet.Cell(row, colStart + d);
                        cell.Value = Math.Round(bgMin, 1);
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        if (isOver) cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE0B2");
                    }
                    row++;

                    // Từng SP
                    if (spByGroup.TryGetValue(buildGroupCode, out var spList))
                    {
                        foreach (var sp in spList)
                        {
                            var prod = products.FirstOrDefault(p => p.ProductId == sp);
                            double minPerSP = prod?.MinutesPerProduct ?? 0;
                            sheet.Cell(row, 1).Value = minPerSP > 0 ? $"─ {sp} ({minPerSP}'/sp)" : $"─ {sp} (—)";

                            for (int d = 0; d < allocations.Count; d++)
                            {
                                var alloc = allocations[d];
                                bool isOver = bgDeadline.HasValue && alloc.Date.Date > bgDeadline.Value;
                                var cell = sheet.Cell(row, colStart + d);

                                if (alloc.IsDayOff)
                                {
                                    cell.Value = 0;
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                }
                                else
                                {
                                    var pa = alloc.Products.FirstOrDefault(p => p.ProductId == sp);
                                    cell.Value = (pa != null && pa.ProductCount > 0) ? $"{pa.ProductCount} sp" : "—";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                }

                                if (isOver) cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE0B2");
                            }
                            row++;
                        }
                    }

                    row++; // Dòng trống giữa các BuildGroup
                }

                // Số người Ca
                sheet.Cell(row, 1).Value = "Số người";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                for (int d = 0; d < allocations.Count; d++)
                {
                    var alloc = allocations[d];
                    sheet.Cell(row, colStart + d).Value = alloc.IsDayOff ? 0 : alloc.Workers;
                    sheet.Cell(row, colStart + d).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                row++;

                // Tổng SP Ca
                sheet.Cell(row, 1).Value = $"Tổng SP - {shiftRow.RowName}";
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
                for (int d = 0; d < allocations.Count; d++)
                {
                    var alloc = allocations[d];
                    int totalSP = alloc.Products.Sum(p => p.ProductCount);
                    var cell = sheet.Cell(row, colStart + d);
                    cell.Value = totalSP;
                    cell.Style.Font.Bold = true;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                }
                row++;
            }

            // Auto-fit
            sheet.Column(1).AdjustToContents();
            for (int d = 0; d < totalDays; d++)
                sheet.Column(colStart + d).Width = 16;
        }

        // ─────────────────────────────────────────
        // TÍNH PHÂN BỔ TUẦN TỰ CHO 1 CA (ShiftRow)
        // Dựa trên blocks THỰC TẾ trên grid, không phải openMinutes toàn cục.
        // ─────────────────────────────────────────
        private List<DailyAllocation> CalculateSequentialAllocationForRow(
            ShiftRow shiftRow,
            List<ProductData> products,
            List<OpenMinutesData> openMinutes,
            ProductOrderSettings productOrder,
            Dictionary<string, List<string>> blockOrder,
            DateTime startDate,
            DateTime deadlineDate,
            int totalDays)
        {
            var result = new List<DailyAllocation>();

            // Lấy thứ tự BuildGroup và danh sách SP
            var orderedBuildGroups = GetOrderedBuildGroupsForRow(shiftRow, blockOrder);
            var spByGroup = GetProductsByBuildGroup(orderedBuildGroups, products, openMinutes, productOrder);

            // Tính tổng allocated minutes cho mỗi BuildGroup trên TOÀN BỘ row
            var totalAllocatedByGroup = new Dictionary<string, double>();
            foreach (var day in shiftRow.Days)
            {
                foreach (var block in day.Blocks)
                {
                    if (!totalAllocatedByGroup.ContainsKey(block.Code))
                        totalAllocatedByGroup[block.Code] = 0;
                    totalAllocatedByGroup[block.Code] += block.AllocatedMinutes;
                }
            }

            // Tạo danh sách SP tuần tự — dùng allocated minutes từ grid, không phải openMinutes
            var sequentialSPs = new List<(string ProductId, string GroupId, double RemainingMinutes, double MinPerSP)>();
            foreach (var bgCode in orderedBuildGroups)
            {
                if (!spByGroup.TryGetValue(bgCode, out var spList)) continue;
                if (!totalAllocatedByGroup.TryGetValue(bgCode, out var totalGroupMinutes)) continue;
                if (totalGroupMinutes <= 0) continue;

                // Tính tổng openMinutes cho SP trong group này (dùng để phân tỷ lệ)
                double totalOpenInGroup = 0;
                var spOpenList = new List<(string Id, double Open, double MinPerSP)>();
                foreach (var spId in spList)
                {
                    var om = openMinutes.FirstOrDefault(o => o.ProductId == spId);
                    var prod = products.FirstOrDefault(p => p.ProductId == spId);
                    double open = om?.OpenMinutes ?? 0;
                    double minPerSP = prod?.MinutesPerProduct ?? 0;
                    if (open > 0)
                    {
                        spOpenList.Add((spId, open, minPerSP));
                        totalOpenInGroup += open;
                    }
                }

                if (totalOpenInGroup <= 0) continue;

                // Phân bổ allocated minutes theo tỷ lệ openMinutes cho từng SP
                double distributed = 0;
                for (int i = 0; i < spOpenList.Count; i++)
                {
                    var sp = spOpenList[i];
                    double spShare;
                    if (i == spOpenList.Count - 1)
                    {
                        // SP cuối nhận phần còn lại (tránh sai số floating point)
                        spShare = totalGroupMinutes - distributed;
                    }
                    else
                    {
                        spShare = Math.Round(totalGroupMinutes * (sp.Open / totalOpenInGroup), 2);
                    }
                    distributed += spShare;

                    if (spShare > 0)
                        sequentialSPs.Add((sp.Id, bgCode, spShare, sp.MinPerSP));
                }
            }

            // Phân bổ SP tuần tự theo capacity thực tế từ blocks trên grid mỗi ngày
            int currentSPIndex = 0;
            double currentSPRemaining = sequentialSPs.Count > 0 ? sequentialSPs[0].RemainingMinutes : 0;

            for (int d = 0; d < totalDays; d++)
            {
                var date = startDate.AddDays(d);
                var dayCell = shiftRow.Days.FirstOrDefault(dc => dc.Date.Date == date.Date);

                bool isDayOff = dayCell == null || dayCell.IsDayOff;
                double workers = (!isDayOff && dayCell != null) ? dayCell.Config.Workers : 0;

                // Capacity = tổng AllocatedMinutes của blocks trên grid ngày này (không phải TotalCapacity)
                double dayBlockMinutes = 0;
                if (dayCell != null)
                {
                    dayBlockMinutes = dayCell.Blocks.Sum(b => b.AllocatedMinutes);
                }

                var dailyAlloc = new DailyAllocation
                {
                    Date = date,
                    IsDayOff = isDayOff,
                    TotalCapacity = dayBlockMinutes,
                    Workers = workers
                };

                if (!isDayOff && dayBlockMinutes > 0.01 && currentSPIndex < sequentialSPs.Count)
                {
                    double remainingCapacity = dayBlockMinutes;

                    while (remainingCapacity > 0.01 && currentSPIndex < sequentialSPs.Count)
                    {
                        var sp = sequentialSPs[currentSPIndex];
                        double minPerSP = sp.MinPerSP;

                        if (minPerSP <= 0)
                        {
                            currentSPIndex++;
                            if (currentSPIndex < sequentialSPs.Count)
                                currentSPRemaining = sequentialSPs[currentSPIndex].RemainingMinutes;
                            continue;
                        }

                        double minutesToUse = Math.Min(remainingCapacity, currentSPRemaining);
                        int productCount = (int)Math.Ceiling(minutesToUse / minPerSP);
                        double actualMinutesUsed = productCount * minPerSP;

                        if (actualMinutesUsed > currentSPRemaining)
                        {
                            productCount = (int)Math.Ceiling(currentSPRemaining / minPerSP);
                            actualMinutesUsed = Math.Min(currentSPRemaining, productCount * minPerSP);
                        }

                        bool isCompleted = currentSPRemaining <= actualMinutesUsed + 0.01;
                        double used = Math.Min(minutesToUse, actualMinutesUsed);

                        var existing = dailyAlloc.Products.FirstOrDefault(p => p.ProductId == sp.ProductId);
                        if (existing != null)
                        {
                            existing.MinutesUsed += used;
                            existing.ProductCount += productCount;
                            existing.IsCompleted = isCompleted;
                        }
                        else
                        {
                            dailyAlloc.Products.Add(new DailyProductAllocation
                            {
                                ProductId = sp.ProductId,
                                GroupId = sp.GroupId,
                                MinutesUsed = used,
                                ProductCount = productCount,
                                IsCompleted = isCompleted
                            });
                        }

                        remainingCapacity -= used;
                        currentSPRemaining -= used;

                        if (currentSPRemaining <= 0.01)
                        {
                            currentSPIndex++;
                            if (currentSPIndex < sequentialSPs.Count)
                                currentSPRemaining = sequentialSPs[currentSPIndex].RemainingMinutes;
                        }
                    }
                }

                result.Add(dailyAlloc);
            }

            return result;
        }

        // ─────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────

        /// <summary>
        /// Lấy thứ tự BuildGroup cho 1 Ca, dùng blockOrder[RowName].
        /// </summary>
        private List<string> GetOrderedBuildGroupsForRow(
            ShiftRow shiftRow,
            Dictionary<string, List<string>> blockOrder)
        {
            var allCodes = shiftRow.Days
                .SelectMany(d => d.Blocks.Select(b => b.Code))
                .Distinct()
                .ToList();

            if (blockOrder != null && blockOrder.TryGetValue(shiftRow.RowName, out var ordered))
            {
                var result = ordered.Where(c => allCodes.Contains(c)).ToList();
                var remaining = allCodes.Where(c => !result.Contains(c)).OrderBy(c => c);
                result.AddRange(remaining);
                return result;
            }

            return allCodes.OrderBy(c => c).ToList();
        }

        private Dictionary<string, List<string>> GetProductsByBuildGroup(
            List<string> buildGroupCodes,
            List<ProductData> products,
            List<OpenMinutesData> openMinutes,
            ProductOrderSettings productOrder)
        {
            var result = new Dictionary<string, List<string>>();
            var activeIds = new HashSet<string>(openMinutes.Where(o => o.OpenMinutes > 0).Select(o => o.ProductId));

            foreach (var code in buildGroupCodes)
            {
                var spInGroup = products
                    .Where(p => p.GroupId == code && activeIds.Contains(p.ProductId))
                    .Select(p => p.ProductId)
                    .ToList();

                if (productOrder?.BlockProductOrder != null &&
                    productOrder.BlockProductOrder.TryGetValue(code, out var orderedSPs))
                {
                    var ordered = orderedSPs.Where(id => spInGroup.Contains(id)).ToList();
                    ordered.AddRange(spInGroup.Where(id => !ordered.Contains(id)).OrderBy(id => id));
                    result[code] = ordered;
                }
                else
                {
                    result[code] = spInGroup.OrderBy(id => id).ToList();
                }
            }

            return result;
        }

        private int CalculateTotalSPForRow(
            ShiftRow shiftRow,
            List<ProductData> products,
            List<OpenMinutesData> openMinutes)
        {
            // Tính tổng allocated minutes trên grid cho mỗi BuildGroup
            var totalAllocatedByGroup = new Dictionary<string, double>();
            foreach (var day in shiftRow.Days)
            {
                foreach (var block in day.Blocks)
                {
                    if (!totalAllocatedByGroup.ContainsKey(block.Code))
                        totalAllocatedByGroup[block.Code] = 0;
                    totalAllocatedByGroup[block.Code] += block.AllocatedMinutes;
                }
            }

            int totalSP = 0;
            foreach (var kvp in totalAllocatedByGroup)
            {
                var code = kvp.Key;
                var groupAllocated = kvp.Value;
                if (groupAllocated <= 0) continue;

                // Tính tổng openMinutes cho SP trong group
                double totalOpenInGroup = 0;
                var spList = new List<(string Id, double Open, double MinPerSP)>();
                foreach (var sp in products.Where(p => p.GroupId == code))
                {
                    var om = openMinutes.FirstOrDefault(o => o.ProductId == sp.ProductId);
                    double open = om?.OpenMinutes ?? 0;
                    if (open > 0 && sp.MinutesPerProduct > 0)
                    {
                        spList.Add((sp.ProductId, open, sp.MinutesPerProduct));
                        totalOpenInGroup += open;
                    }
                }

                if (totalOpenInGroup <= 0) continue;

                // Phân bổ allocated minutes theo tỷ lệ và tính SP
                foreach (var sp in spList)
                {
                    double spShare = groupAllocated * (sp.Open / totalOpenInGroup);
                    totalSP += (int)Math.Ceiling(spShare / sp.MinPerSP);
                }
            }
            return totalSP;
        }

        private string GetVietnameseDayName(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            DayOfWeek.Sunday => "CN",
            _ => day.ToString()
        };
    }
}
