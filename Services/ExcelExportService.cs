using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using KHSX.Models;

namespace KHSX.Services
{
    public class ExcelExportService
    {
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
            WriteScheduleSheet(workbook.Worksheets.Add("KeHoach"), rows.ToList(), deadlines ?? new List<DeadlineData>());
            WriteBlockSummarySheet(workbook.Worksheets.Add("Blocks"), rows.ToList(), deadlines ?? new List<DeadlineData>());
            workbook.SaveAs(filePath);
        }

        private static void WriteScheduleSheet(IXLWorksheet sheet, List<ShiftRow> rows, List<DeadlineData> deadlines)
        {
            sheet.Cell(1, 1).Value = "Function";
            sheet.Cell(1, 2).Value = "Ca";

            var dates = rows
                .SelectMany(r => r.Days)
                .Select(d => d.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            for (int i = 0; i < dates.Count; i++)
            {
                sheet.Cell(1, i + 3).Value = dates[i].ToString("dd/MM/yyyy");
            }

            int rowIndex = 2;
            foreach (var row in rows.OrderBy(r => r.ParentLineName).ThenBy(r => r.ShiftName))
            {
                sheet.Cell(rowIndex, 1).Value = row.ParentLineName;
                sheet.Cell(rowIndex, 2).Value = row.ShiftName;

                for (int i = 0; i < dates.Count; i++)
                {
                    var date = dates[i];
                    var day = row.Days.FirstOrDefault(d => d.Date.Date == date);
                    var cell = sheet.Cell(rowIndex, i + 3);

                    if (day == null || day.IsDayOff)
                    {
                        cell.Value = "Nghỉ";
                        cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                        continue;
                    }

                    if (day.Blocks.Count == 0)
                    {
                        cell.Value = string.Empty;
                    }
                    else
                    {
                        cell.Value = string.Join(Environment.NewLine, day.Blocks.Select(b =>
                            $"{b.Code} ({b.ProductionGroup}) - {b.AllocatedMinutes:0.##}m"));
                        cell.Style.Alignment.WrapText = true;
                    }

                    if (day.IsOverCapacity || day.Blocks.Any(b => IsAfterDeadline(date, b.ProductionGroup, deadlines)))
                    {
                        cell.Style.Fill.BackgroundColor = XLColor.LightPink;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
                        cell.Style.Border.OutsideBorderColor = XLColor.Red;
                    }
                }

                rowIndex++;
            }

            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
            sheet.SheetView.FreezeColumns(2);
        }

        private static void WriteBlockSummarySheet(IXLWorksheet sheet, List<ShiftRow> rows, List<DeadlineData> deadlines)
        {
            sheet.Cell(1, 1).Value = "Function";
            sheet.Cell(1, 2).Value = "Ca";
            sheet.Cell(1, 3).Value = "Ngày";
            sheet.Cell(1, 4).Value = "BuildGroup";
            sheet.Cell(1, 5).Value = "Gr.xxx";
            sheet.Cell(1, 6).Value = "Minutes";
            sheet.Cell(1, 7).Value = "Deadline";
            sheet.Cell(1, 8).Value = "Cảnh báo";

            int rowIndex = 2;
            foreach (var row in rows.OrderBy(r => r.ParentLineName).ThenBy(r => r.ShiftName))
            {
                foreach (var day in row.Days.OrderBy(d => d.Date))
                {
                    foreach (var block in day.Blocks)
                    {
                        var deadline = deadlines.FirstOrDefault(d =>
                            string.Equals(d.GroupNumber, block.ProductionGroup, StringComparison.OrdinalIgnoreCase))?.Deadline.Date;

                        sheet.Cell(rowIndex, 1).Value = row.ParentLineName;
                        sheet.Cell(rowIndex, 2).Value = row.ShiftName;
                        sheet.Cell(rowIndex, 3).Value = day.Date;
                        sheet.Cell(rowIndex, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                        sheet.Cell(rowIndex, 4).Value = block.Code;
                        sheet.Cell(rowIndex, 5).Value = block.ProductionGroup;
                        sheet.Cell(rowIndex, 6).Value = block.AllocatedMinutes;
                        if (deadline.HasValue)
                        {
                            sheet.Cell(rowIndex, 7).Value = deadline.Value;
                            sheet.Cell(rowIndex, 7).Style.DateFormat.Format = "dd/MM/yyyy";
                        }

                        var warnings = new List<string>();
                        if (day.IsOverCapacity) warnings.Add("Vượt capacity");
                        if (deadline.HasValue && day.Date.Date > deadline.Value) warnings.Add("Vượt deadline");
                        sheet.Cell(rowIndex, 8).Value = string.Join(", ", warnings);

                        if (warnings.Count > 0)
                            sheet.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.LightPink;

                        rowIndex++;
                    }
                }
            }

            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }

        private static bool IsAfterDeadline(DateTime date, string productionGroup, List<DeadlineData> deadlines)
        {
            var deadline = deadlines.FirstOrDefault(d =>
                string.Equals(d.GroupNumber, productionGroup, StringComparison.OrdinalIgnoreCase));
            return deadline != null && date.Date > deadline.Deadline.Date;
        }
    }
}
