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
            
            // Tổng số phút phân bổ trên lưới cho TỪNG BuildGroup trong ngày (Không phụ thuộc SP)
            public Dictionary<string, double> BgCapacities { get; set; } = new Dictionary<string, double>();

            public List<DailyProductAllocation> Products { get; set; } = new List<DailyProductAllocation>();
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
            DateTime deadlineDate)
        {
            using var workbook = new XLWorkbook();

            // Nhóm theo ParentLineName → mỗi Line 1 sheet
            var lineGroups = rows
                .GroupBy(r => r.ParentLineName)
                .OrderBy(g => g.Key)
                .ToList();

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
                WriteLineSheet(sheet, lineGroup, products, openMinutes, productOrder, blockOrder, startDate, deadlineDate);
            }

            workbook.SaveAs(filePath);
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
            DateTime deadlineDate)
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

            // Tính phạm vi ngày
            int totalDays = (int)(deadlineDate.Date - startDate.Date).TotalDays + 1;

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

                // Dữ liệu từng BuildGroup
                foreach (var buildGroupCode in orderedBuildGroups)
                {
                    // Header BuildGroup
                    sheet.Cell(row, 1).Value = $"BuildGroup: {buildGroupCode}";
                    sheet.Cell(row, 1).Style.Font.Bold = true;
                    sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    for (int d = 0; d < allocations.Count; d++)
                        sheet.Cell(row, colStart + d).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    row++;

                    // Phút phân bổ
                    sheet.Cell(row, 1).Value = "Phút phân bổ";
                    sheet.Cell(row, 1).Style.Font.Italic = true;
                    for (int d = 0; d < allocations.Count; d++)
                    {
                        var alloc = allocations[d];
                        double bgMin = alloc.IsDayOff ? 0 :
                            (alloc.BgCapacities.TryGetValue(buildGroupCode, out double cap) ? cap : 0);
                        sheet.Cell(row, colStart + d).Value = Math.Round(bgMin, 1);
                        sheet.Cell(row, colStart + d).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
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
                                if (alloc.IsDayOff)
                                {
                                    sheet.Cell(row, colStart + d).Value = 0;
                                    sheet.Cell(row, colStart + d).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    continue;
                                }

                                var pa = alloc.Products.FirstOrDefault(p => p.ProductId == sp);
                                if (pa != null && pa.ProductCount > 0)
                                {
                                    sheet.Cell(row, colStart + d).Value = pa.IsCompleted ? $"{pa.ProductCount} sp ✅" : $"{pa.ProductCount} sp";
                                }
                                else
                                {
                                    sheet.Cell(row, colStart + d).Value = "—";
                                }
                                sheet.Cell(row, colStart + d).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
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

            var orderedBuildGroups = GetOrderedBuildGroupsForRow(shiftRow, blockOrder);
            var spByGroup = GetProductsByBuildGroup(orderedBuildGroups, products, openMinutes, productOrder);

            // Khởi tạo state cho TỪNG sản phẩm (cần biết SP nào còn dư bao nhiêu phút hở)
            // Thay vì dùng list phẳng, dùng dictionary GroupId -> List<SPState>
            var stateByGroup = new Dictionary<string, List<ProductState>>();
            foreach (var bgCode in orderedBuildGroups)
            {
                var spListForGroup = new List<ProductState>();
                if (spByGroup.TryGetValue(bgCode, out var spIds))
                {
                    foreach (var spId in spIds)
                    {
                        var prod = products.FirstOrDefault(p => p.ProductId == spId);
                        var om = openMinutes.FirstOrDefault(o => o.ProductId == spId);
                        if (om == null || om.OpenMinutes <= 0) continue;
                        
                        double minPerSP = prod?.MinutesPerProduct ?? 0;
                        if (minPerSP <= 0) continue;

                        spListForGroup.Add(new ProductState
                        {
                            ProductId = spId,
                            GroupId = bgCode,
                            OriginalMinutes = om.OpenMinutes,
                            RemainingMinutes = om.OpenMinutes,
                            MinPerSP = minPerSP
                        });
                    }
                }
                stateByGroup[bgCode] = spListForGroup;
            }

            for (int d = 0; d < totalDays; d++)
            {
                var date = startDate.AddDays(d);
                var dayCell = shiftRow.Days.FirstOrDefault(dc => dc.Date.Date == date.Date);

                bool isDayOff = dayCell == null || dayCell.IsDayOff;
                double capacity = isDayOff ? 0 : dayCell!.TotalCapacity;
                double workers = isDayOff ? 0 : dayCell!.Config.Workers;

                var dailyAlloc = new DailyAllocation
                {
                    Date = date,
                    IsDayOff = isDayOff,
                    TotalCapacity = capacity,
                    Workers = workers
                };

                if (!isDayOff && dayCell != null && dayCell.Blocks.Count > 0)
                {
                    // Lăn qua từng BuildGroup CÓ CHỨA BLOCK trong ngày hôm nay
                    // Capacity của Bg trong ngày = tổng AllocatedMinutes của các khối block thuộc Bg này
                    foreach (var bgCode in orderedBuildGroups)
                    {
                        double bgCapacityInDay = dayCell.Blocks
                            .Where(b => b.Code == bgCode)
                            .Sum(b => b.AllocatedMinutes);

                        if (bgCapacityInDay <= 0.01) continue;
                        
                        // LƯU LẠI NĂNG LỰC GỐC CỦA GROUP TRONG NGÀY ĐỂ RENDER EXCEL CHUẨN XÁC
                        dailyAlloc.BgCapacities[bgCode] = bgCapacityInDay;

                        if (!stateByGroup.TryGetValue(bgCode, out var spList) || spList.Count == 0) continue;

                        double remainingBgCapacity = bgCapacityInDay;

                        // Rút ruột capacity này cho các sản phẩm trong group
                        for (int i = 0; i < spList.Count && remainingBgCapacity > 0.01; i++)
                        {
                            var sp = spList[i];
                            if (sp.RemainingMinutes <= 0.01) continue;

                            double minutesToUse = Math.Min(remainingBgCapacity, sp.RemainingMinutes);
                            
                            // Tổng số lượng SP có thể làm tính đến thời điểm hết 'minutesToUse' này
                            // (Bao gồm cả phần lẻ đã làm từ ngày hôm trước mang lọt sang)
                            double totalMinutesWorkedSoFarOnThisSP = (sp.OriginalMinutes - sp.RemainingMinutes) + minutesToUse;
                            double previousMinutesWorkedOnThisSP = sp.OriginalMinutes - sp.RemainingMinutes;

                            int totalProductCountSoFar = (int)Math.Floor(totalMinutesWorkedSoFarOnThisSP / sp.MinPerSP);
                            int previousProductCount = (int)Math.Floor(previousMinutesWorkedOnThisSP / sp.MinPerSP);

                            // Số lượng SP hoàn thành TRONG ngày hôm nay
                            int productCountToday = totalProductCountSoFar - previousProductCount;
                            
                            // Nếu đây là lần cuối cùng (làm nốt mẩu cuối cùng) thì vét nốt SP nếu lẻ
                            bool isCompleted = sp.RemainingMinutes <= minutesToUse + 0.01;
                            if (isCompleted)
                            {
                                // Khi vớt nốt mẩu cuối, kiểm tra tổng số lượng đã allocate so với tổng SP cần thiết
                                int expectedTotalItems = (int)Math.Ceiling(sp.OriginalMinutes / sp.MinPerSP);
                                if (totalProductCountSoFar < expectedTotalItems)
                                {
                                    int shortFall = expectedTotalItems - totalProductCountSoFar;
                                    productCountToday += shortFall; 
                                }
                            }

                            dailyAlloc.Products.Add(new DailyProductAllocation
                            {
                                ProductId = sp.ProductId,
                                GroupId = sp.GroupId,
                                MinutesUsed = minutesToUse, // Ghi nhận đúng số phút đã cống hiến hôm nay (dù có ra SP hay không)
                                ProductCount = productCountToday, // Đủ 1 SP mới nảy số, rơi vào ngày nào thì hiện số ở ngày đó
                                IsCompleted = isCompleted
                            });

                            remainingBgCapacity -= minutesToUse;
                            sp.RemainingMinutes -= minutesToUse;
                        }
                    }
                }

                result.Add(dailyAlloc);
            }

            return result;
        }

        private class ProductState
        {
            public string ProductId { get; set; } = string.Empty;
            public string GroupId { get; set; } = string.Empty;
            public double OriginalMinutes { get; set; }
            public double RemainingMinutes { get; set; }
            public double MinPerSP { get; set; }
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
            var buildGroupCodes = shiftRow.Days
                .SelectMany(d => d.Blocks.Select(b => b.Code))
                .Distinct().ToList();

            int totalSP = 0;
            foreach (var code in buildGroupCodes)
            {
                foreach (var sp in products.Where(p => p.GroupId == code))
                {
                    var om = openMinutes.FirstOrDefault(o => o.ProductId == sp.ProductId);
                    if (om != null && om.OpenMinutes > 0 && sp.MinutesPerProduct > 0)
                        totalSP += (int)Math.Ceiling(om.OpenMinutes / sp.MinutesPerProduct);
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
