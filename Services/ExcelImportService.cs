using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows.Media;
using ExcelDataReader;
using KHSX.Models;

namespace KHSX.Services
{
    public class ExcelImportService
    {
        public ExcelImportService()
        {
            // Cần thiết để ExcelDataReader hoạt động tốt với các encoding của Excel
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public List<ProductBlock> ImportProducts(string filePath, string sheetName = "pivot openmin")
        {
            var result = new List<ProductBlock>();
            
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = true
                            }
                        });

                        if (!dataSet.Tables.Contains(sheetName))
                        {
                            throw new Exception($"Không tìm thấy sheet tên là '{sheetName}' trong file.");
                        }

                        var table = dataSet.Tables[sheetName];
                        
                        // Tìm chỉ mục cột, giả sử header có chứa "Mã" và "phút"
                        int codeColIdx = -1;
                        int minColIdx = -1;

                        for (int i = 0; i < table.Columns.Count; i++)
                        {
                            var colName = table.Columns[i]?.ColumnName?.ToLower() ?? "";
                            if (colName.Contains("build group") || colName.Contains("mã") || colName.Contains("code")) codeColIdx = i;
                            if (colName.Contains("open_min") || colName.Contains("phút") || colName.Contains("min")) minColIdx = i;
                        }

                        if (codeColIdx == -1 || minColIdx == -1)
                        {
                            throw new Exception("Không tìm thấy các cột 'Build group' hoặc 'Sum of OPEN_MIN' trên header.");
                        }

                        Random rng = new Random();
                        int sourceIndex = 1;
                        foreach (DataRow row in table.Rows)
                        {
                            var codeStr = row[codeColIdx]?.ToString() ?? "";
                            
                            double minVal = 0;
                            var minObj = row[minColIdx];
                            if (minObj is double d) minVal = d;
                            else if (minObj is int i) minVal = i;
                            else if (minObj is long l) minVal = l;
                            else if (minObj is float f) minVal = f;
                            else if (minObj is decimal dec) minVal = (double)dec;
                            else
                            {
                                var minStr = minObj?.ToString()?.Replace(",", "") ?? "";
                                double.TryParse(minStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out minVal);
                            }

                            if (string.IsNullOrWhiteSpace(codeStr) && minVal <= 0)
                                continue;

                            if (minVal > 0)
                            {
                                // Generate a random light color for display
                                byte r = (byte)rng.Next(150, 256);
                                byte g = (byte)rng.Next(150, 256);
                                byte b = (byte)rng.Next(150, 256);

                                result.Add(new ProductBlock
                                {
                                    SourceId = $"S{sourceIndex++:0000}",
                                    Code = codeStr,
                                    TotalMinutesRequired = minVal,
                                    AllocatedMinutes = minVal,
                                    DisplayColor = new SolidColorBrush(Color.FromRgb(r, g, b))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi đọc file Excel: {ex.Message}");
            }

            return result;
        }
    }
}
