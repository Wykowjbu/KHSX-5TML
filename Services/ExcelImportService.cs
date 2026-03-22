using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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

        // --- BƯỚC 1: IMPORT MARKETING ---
        // Marketing có chứa các group Gr.xxx
        // Cập nhật settings "currentMESGroup" nếu tìm thấy group mới nhất
        public void ImportMarketing(string filePath, string sheetName = "Sheet1")
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = true }
                });

                var table = dataSet.Tables.Contains(sheetName) ? dataSet.Tables[sheetName] : dataSet.Tables[0];
                
                int codeColIdx = -1;  // Tên sp (A)
                int groupColIdx = -1; // Nhóm sp (K)
                int minColIdx = -1;   // Số phút / sp (L)
                int funcColIdx = -1;  // Function (M)
                var grColIndices = new Dictionary<int, string>(); // Cột chứa Gr.xxx

                // 1. Quét tên cột từ Configured DataTable (dòng đầu tiên có thể làm Header)
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var colName = table.Columns[i]?.ColumnName?.Trim()?.ToLower() ?? "";
                    var originalName = table.Columns[i]?.ColumnName?.Trim() ?? "";

                    if (colName.Contains("mã") || colName.Contains("code") || colName.Contains("tên sp")) codeColIdx = i;
                    else if (colName.Contains("nhóm") || colName.Contains("group")) groupColIdx = i;
                    else if (colName.Contains("phút") || colName.Contains("minute") || colName.Contains("min")) minColIdx = i;
                    else if (colName.Contains("function")) funcColIdx = i;
                    else 
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(originalName, @"^Gr[\.\s]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            grColIndices[i] = $"Gr.{match.Groups[1].Value}";
                        }
                    }
                }

                // 2. Quét thêm từ 2 dòng đầu tiên vì Excel thường có Merge Cell làm sai Header
                for (int r = 0; r < Math.Min(2, table.Rows.Count); r++)
                {
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        var cellVal = table.Rows[r][c]?.ToString()?.Trim() ?? "";
                        var match = System.Text.RegularExpressions.Regex.Match(cellVal, @"^Gr[\.\s]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && !grColIndices.ContainsKey(c))
                        {
                            grColIndices[c] = $"Gr.{match.Groups[1].Value}";
                        }
                    }
                }

                // Fallbacks dựa trên vị trí cột mặc định
                if (codeColIdx == -1) codeColIdx = 0; // A
                if (groupColIdx == -1) groupColIdx = 10; // K
                if (funcColIdx == -1) funcColIdx = 11; // L (Tên Function)
                if (minColIdx == -1) minColIdx = 12; // M (Số phút)

                // Tải dữ liệu cũ để Merge
                var oldProducts = JsonStorage.Load<List<ProductData>>("products.json") ?? new List<ProductData>();
                var oldGroupsMap = JsonStorage.Load<List<ProductGroupData>>("productGroups.json") ?? new List<ProductGroupData>();
                var groupsMap = oldGroupsMap.ToDictionary(g => g.GroupId, g => g);

                var productDict = oldProducts.ToDictionary(p => p.ProductId, p => p);

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row.ItemArray.Length <= Math.Max(codeColIdx, groupColIdx)) continue;

                    var codeStr = row[codeColIdx]?.ToString()?.Trim() ?? "";
                    var groupStr = row[groupColIdx]?.ToString()?.Trim() ?? "";

                    if (string.IsNullOrEmpty(codeStr) || string.IsNullOrEmpty(groupStr)) continue;

                    if (!productDict.TryGetValue(codeStr, out var currentProduct))
                    {
                        currentProduct = new ProductData
                        {
                            ProductId = codeStr,
                            GroupId = groupStr,
                            QuantitiesByGroup = new Dictionary<string, double>()
                        };
                        productDict[codeStr] = currentProduct;
                    }

                    double minVal = 18; 
                    if (minColIdx != -1 && row.ItemArray.Length > minColIdx)
                    {
                        var minObj = row[minColIdx];
                        double.TryParse(minObj?.ToString(), out minVal);
                        currentProduct.MinutesPerProduct = minVal;
                    }

                    string funcStr = "";
                    if (funcColIdx != -1 && row.ItemArray.Length > funcColIdx)
                    {
                        funcStr = row[funcColIdx]?.ToString()?.Trim() ?? "";
                        currentProduct.Function = funcStr;
                    }

                    // Đọc và cập nhật số lượng theo từng Gr.xxx
                    foreach (var kvp in grColIndices)
                    {
                        if (row.ItemArray.Length > kvp.Key)
                        {
                            var cellVal = row[kvp.Key]?.ToString()?.Trim() ?? "";
                            if (double.TryParse(cellVal, out double qty) && qty > 0)
                            {
                                currentProduct.QuantitiesByGroup[kvp.Value] = qty;
                                
                                // Cập nhật tên function vào ProductGroup
                                if (!groupsMap.ContainsKey(groupStr))
                                {
                                    groupsMap[groupStr] = new ProductGroupData { GroupId = groupStr, Name = !string.IsNullOrEmpty(funcStr) ? funcStr : groupStr, ProductionGroup = kvp.Value };
                                }
                                else if (!string.IsNullOrEmpty(funcStr))
                                {
                                    // Luôn cập nhật tên mới nhất từ file Excel
                                    groupsMap[groupStr].Name = funcStr;
                                }
                            }
                        }
                    }

                    // Cập nhật Total
                    currentProduct.TotalQuantity = currentProduct.QuantitiesByGroup.Values.Sum();

                    // Cập nhật lại Gr.xxx lớn nhất cho sản phẩm
                    if (currentProduct.QuantitiesByGroup.Any())
                    {
                        currentProduct.ProductionGroup = currentProduct.QuantitiesByGroup.Keys.OrderByDescending(k => k).First();
                    }
                }

                JsonStorage.Save("products.json", productDict.Values.ToList());
                JsonStorage.Save("productGroups.json", groupsMap.Values.ToList());
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi import file Marketing: {ex.Message}");
            }
        }

        // --- BƯỚC 3: IMPORT MES ---
        // File chứa số phút còn lại của từng mã sản phẩm
        public void ImportMES(string filePath)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    // Tắt tự động lấy dòng đầu làm header vì header ở tít dòng 18
                    ConfigureDataTable = (_) => new ExcelDataTableConfiguration() { UseHeaderRow = false } 
                });

                // Lấy sheet đầu tiên
                var table = dataSet.Tables.Count > 0 ? dataSet.Tables[0] : null;
                if (table == null) throw new Exception("File Excel không có dữ liệu.");

                int codeColIdx = -1;
                int minColIdx = -1;
                int headerRowIdx = -1;

                // Tìm dòng tiêu đề chứa "ORD_PRODUCTNR" và "OPEN_MIN"
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        var cellValue = table.Rows[r][c]?.ToString()?.Trim().ToUpper() ?? "";
                        if (cellValue.Contains("ORD_PRODUCTNR") || cellValue.Contains("PRODUCTNR"))
                            codeColIdx = c;
                        if (cellValue.Contains("OPEN_MIN"))
                            minColIdx = c;
                    }

                    if (codeColIdx != -1 && minColIdx != -1)
                    {
                        headerRowIdx = r;
                        break;
                    }
                }

                // Fallback theo cấu trúc user cung cấp: D (3) cho Sản phẩm, I (8) cho Số phút, Data từ dòng 20 (index 19)
                if (codeColIdx == -1 || minColIdx == -1)
                {
                    codeColIdx = 3; // D
                    minColIdx = 8;  // I
                    headerRowIdx = 18; // Dòng 19 (index 18) là header. Dòng 20 (index 19) là TOTAL.
                }

                var openMins = new List<OpenMinutesData>();

                // Data bắt đầu dòng tiếp theo của Header
                int startRow = headerRowIdx + 1;

                for (int r = startRow; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (row.ItemArray.Length <= Math.Max(codeColIdx, minColIdx)) continue;

                    var codeStr = row[codeColIdx]?.ToString()?.Trim() ?? "";
                    
                    // Bỏ qua dòng trống hoặc dòng có chữ TOTAL
                    if (string.IsNullOrEmpty(codeStr) || codeStr.Equals("TOTAL", StringComparison.OrdinalIgnoreCase)) continue;

                    double minVal = 0;
                    var minObj = row[minColIdx];
                    if (minObj is double d) minVal = d;
                    else double.TryParse(minObj?.ToString()?.Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out minVal);

                    if (minVal > 0)
                    {
                        openMins.Add(new OpenMinutesData
                        {
                            ProductId = codeStr,
                            OpenMinutes = Math.Round(minVal, 2)
                        });
                    }
                }

                JsonStorage.Save("openMinutes.json", openMins);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi import file MES: {ex.Message}");
            }
        }

        // --- BƯỚC 4 & 5: GOM NHÓM & TẠO BLOCK ---
        // Xảy ra sau khi import xong MES
        public List<ProductBlock> GenerateBlocksFromData()
        {
            var result = new List<ProductBlock>();
            var products = JsonStorage.Load<List<ProductData>>("products.json");
            var openMins = JsonStorage.Load<List<OpenMinutesData>>("openMinutes.json");
            var deadlines = JsonStorage.Load<List<DeadlineData>>("deadlines.json");
            var settings = JsonStorage.Load<SettingsData>("settings.json") ?? new SettingsData();

            if (products == null || openMins == null) return result;

            // Dictionary: GroupId -> TotalMinutes
            var groupMinutes = new Dictionary<string, double>();

            foreach (var om in openMins)
            {
                // Tìm GroupId của ProductId này
                var prod = products.FirstOrDefault(p => p.ProductId == om.ProductId);
                if (prod != null)
                {
                    string groupId = prod.GroupId;
                    
                    // CHỈ gom nhóm cho currentMESGroup nếu setting có yêu cầu.
                    // Tuy nhiên tài liệu bảo "không cần quan tâm Gr quá khứ", nên logic linh động là có nhóm nào tạo block nhóm đó.
                    // ViewModel sẽ tuỳ biến hiển thị cảnh báo sau.
                    
                    if (!groupMinutes.ContainsKey(groupId))
                        groupMinutes[groupId] = 0;
                        
                    groupMinutes[groupId] += om.OpenMinutes;
                }
            }

            Random rng = new Random();
            int sourceIndex = 1;

            // Lấy GroupId map cho ProductionGroup
            var productGroups = JsonStorage.Load<List<ProductGroupData>>("productGroups.json") ?? new List<ProductGroupData>();

            foreach (var kvp in groupMinutes)
            {
                string groupId = kvp.Key;
                double totalMin = kvp.Value;

                if (totalMin > 0)
                {
                    byte r = (byte)rng.Next(150, 256);
                    byte g = (byte)rng.Next(150, 256);
                    byte b = (byte)rng.Next(150, 256);

                    string productionGroup = "";
                    var matchingGroup = productGroups.FirstOrDefault(pg => pg.GroupId == groupId);
                    if (matchingGroup != null && !string.IsNullOrEmpty(matchingGroup.ProductionGroup))
                    {
                        // Lấy từ user assignment
                        productionGroup = matchingGroup.ProductionGroup;
                    }
                    else
                    {
                        // Tính toán fallback từ file product
                        var matchingProducts = products.Where(p => p.GroupId == groupId && !string.IsNullOrEmpty(p.ProductionGroup)).ToList();
                        if (matchingProducts.Any())
                        {
                            productionGroup = matchingProducts.OrderByDescending(p => p.ProductionGroup).First().ProductionGroup;
                        }
                    }

                    // Tìm deadline của group này nếu có set (Deadline thiết lập theo ProductionGroup Gr.xxx)
                    DateTime groupDeadline = DateTime.Today.AddDays(7); // default fallback
                    var dl = deadlines?.FirstOrDefault(d => d.GroupNumber == productionGroup);
                    if (dl != null)
                    {
                        groupDeadline = dl.Deadline.Date;
                    }

                    result.Add(new ProductBlock
                    {
                        SourceId = $"S{sourceIndex++:0000}",
                        Code = groupId, // Code hiển thị trên UI chính là GroupId (ProductGroup)
                        ProductionGroup = productionGroup, // Thêm ID sản xuất (Gr.xxx) để kiểm tra deadline
                        FunctionName = matchingGroup?.Name ?? string.Empty, // Tên fuction đã cấu hình trong Groups
                        TotalMinutesRequired = Math.Round(totalMin, 2),
                        AllocatedMinutes = Math.Round(totalMin, 2),
                        DisplayColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b))
                    });
                }
            }

            return result;
        }
    }
}

