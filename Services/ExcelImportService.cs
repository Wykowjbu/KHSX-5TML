using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExcelDataReader;
using KHSX.Models;

namespace KHSX.Services
{
    public class ExcelImportService
    {
        private const string ModuleMappingsFile = "moduleMappings.json";
        private const string PlanningBlocksFile = "planningBlocks.json";
        private const string OpenMinutesFile = "openMinutes.json";
        private const string BuildGroupSettingsFile = "buildGroupSettings.json";

        public ExcelImportService()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public ImportResult ImportModuleList(string filePath)
        {
            var result = new ImportResult();

            try
            {
                var table = ReadFirstTable(filePath, useHeaderRow: false);
                var existing = JsonStorage.Load<List<ModuleMappingData>>(ModuleMappingsFile);
                var map = existing
                    .Where(m => !string.IsNullOrWhiteSpace(m.FP))
                    .GroupBy(m => NormalizeKey(m.FP))
                    .ToDictionary(g => g.Key, g => g.Last());

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    var functionName = GetCell(row, 0);
                    var buildGroup = GetCell(row, 1);
                    var fp = GetCell(row, 2);

                    if (IsHeaderRow(functionName, buildGroup, fp)) continue;
                    if (string.IsNullOrWhiteSpace(functionName) ||
                        string.IsNullOrWhiteSpace(buildGroup) ||
                        string.IsNullOrWhiteSpace(fp))
                    {
                        continue;
                    }

                    var normalizedFp = NormalizeKey(fp);
                    map[normalizedFp] = new ModuleMappingData
                    {
                        FP = normalizedFp,
                        BuildGroup = NormalizeKey(buildGroup),
                        FunctionName = functionName.Trim(),
                        IsManual = false
                    };
                    result.ImportedCount++;
                }

                var warnings = GetBuildGroupFunctionWarnings(map.Values);
                result.Warnings.AddRange(warnings);

                var mappings = map.Values
                    .OrderBy(m => m.BuildGroup)
                    .ThenBy(m => m.FP)
                    .ToList();

                JsonStorage.Save(ModuleMappingsFile, mappings);
                EnsureBuildGroupSettings(mappings);
                SyncLegacyProductGroups(mappings);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi import file Module List: {ex.Message}");
            }

            return result;
        }

        public ImportResult ImportPlanning(string filePath)
        {
            var result = new ImportResult();

            try
            {
                var table = ReadTable(filePath, "Serienplaning", useHeaderRow: false);
                var mappings = LoadMappingDictionary();
                var grouped = new Dictionary<(string BuildGroup, string ProductionGroup), PlanningBlockData>();
                var missingFps = new HashSet<string>();

                var grColumns = FindPlanningGrColumns(table);
                if (grColumns.Count == 0)
                    throw new Exception("Không tìm thấy header Gr.xxx trong các cột H-L.");

                int startRow = FindPlanningHeaderRow(table, grColumns.Keys) + 1;

                for (int r = startRow; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (!IsU4(GetCell(row, 0))) continue;

                    var productId = GetCell(row, 2);
                    if (string.IsNullOrWhiteSpace(productId)) continue;

                    var total = ParseDouble(GetCell(row, 12));
                    if (total <= 0) continue;

                    var minutesPerProduct = ParseDouble(GetCell(row, 20)) / 1000.0;
                    if (minutesPerProduct <= 0) continue;

                    if (!TryResolveMapping(productId, mappings, out var mapping, out var fp))
                    {
                        if (!string.IsNullOrWhiteSpace(fp)) missingFps.Add(fp);
                        continue;
                    }

                    foreach (var kvp in grColumns)
                    {
                        var qty = ParseDouble(GetCell(row, kvp.Key));
                        if (qty <= 0) continue;

                        var key = (mapping.BuildGroup, kvp.Value);
                        if (!grouped.TryGetValue(key, out var block))
                        {
                            block = new PlanningBlockData
                            {
                                BuildGroup = mapping.BuildGroup,
                                ProductionGroup = kvp.Value,
                                FunctionName = mapping.FunctionName
                            };
                            grouped[key] = block;
                        }

                        block.PlannedMinutes = Math.Round(block.PlannedMinutes + qty * minutesPerProduct, 2);
                    }
                }

                result.MissingFps = missingFps.OrderBy(x => x).ToList();
                if (result.HasMissingFps) return result;

                var blocks = grouped.Values
                    .Where(b => b.PlannedMinutes > 0)
                    .OrderBy(b => b.BuildGroup)
                    .ThenBy(b => b.ProductionGroup)
                    .ToList();

                result.ImportedCount = blocks.Count;
                JsonStorage.Save(PlanningBlocksFile, blocks);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi import file Planning: {ex.Message}");
            }

            return result;
        }

        public ImportResult ImportMES(string filePath)
        {
            var result = new ImportResult();

            try
            {
                var table = ReadFirstTable(filePath, useHeaderRow: false);
                var mappings = LoadMappingDictionary();
                var grouped = new Dictionary<(string BuildGroup, string ProductionGroup), OpenMinutesBlockData>();
                var missingFps = new HashSet<string>();

                for (int r = 0; r < table.Rows.Count; r++)
                {
                    var row = table.Rows[r];
                    if (!IsU4(GetCell(row, 1))) continue;

                    var productionGroup = NormalizeProductionGroup(GetCell(row, 2));
                    var productId = GetCell(row, 3);
                    var openMinutes = ParseDouble(GetCell(row, 8));

                    if (string.IsNullOrWhiteSpace(productionGroup) ||
                        string.IsNullOrWhiteSpace(productId) ||
                        openMinutes <= 0)
                    {
                        continue;
                    }

                    if (!TryResolveMapping(productId, mappings, out var mapping, out var fp))
                    {
                        if (!string.IsNullOrWhiteSpace(fp)) missingFps.Add(fp);
                        continue;
                    }

                    var key = (mapping.BuildGroup, productionGroup);
                    if (!grouped.TryGetValue(key, out var block))
                    {
                        block = new OpenMinutesBlockData
                        {
                            BuildGroup = mapping.BuildGroup,
                            ProductionGroup = productionGroup,
                            FunctionName = mapping.FunctionName
                        };
                        grouped[key] = block;
                    }

                    block.OpenMinutes = Math.Round(block.OpenMinutes + openMinutes, 2);
                }

                result.MissingFps = missingFps.OrderBy(x => x).ToList();
                if (result.HasMissingFps) return result;

                var blocks = grouped.Values
                    .Where(b => b.OpenMinutes > 0)
                    .OrderBy(b => b.BuildGroup)
                    .ThenBy(b => b.ProductionGroup)
                    .ToList();

                result.ImportedCount = blocks.Count;
                JsonStorage.Save(OpenMinutesFile, blocks);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi import file MES/OpenMin: {ex.Message}");
            }

            return result;
        }

        public BlockGenerationResult GenerateBlocksFromDataV2()
        {
            var result = new BlockGenerationResult();
            var planning = JsonStorage.Load<List<PlanningBlockData>>(PlanningBlocksFile);
            var openMinutes = JsonStorage.Load<List<OpenMinutesBlockData>>(OpenMinutesFile);
            var deadlines = JsonStorage.Load<List<DeadlineData>>("deadlines.json");

            var planningMap = planning
                .Where(b => IsValidProductionGroup(b.ProductionGroup))
                .ToDictionary(
                b => (NormalizeKey(b.BuildGroup), NormalizeProductionGroup(b.ProductionGroup)),
                b => b);
            var openMap = openMinutes
                .Where(b => IsValidProductionGroup(b.ProductionGroup))
                .ToDictionary(
                b => (NormalizeKey(b.BuildGroup), NormalizeProductionGroup(b.ProductionGroup)),
                b => b);

            var allKeys = planningMap.Keys.Concat(openMap.Keys)
                .Distinct()
                .OrderBy(k => k.Item1)
                .ThenBy(k => k.Item2)
                .ToList();

            int sourceIndex = 1;
            foreach (var key in allKeys)
            {
                var hasOpen = openMap.TryGetValue(key, out var open);
                var hasPlanning = planningMap.TryGetValue(key, out var planned);

                var minutes = hasOpen ? open!.OpenMinutes : planned?.PlannedMinutes ?? 0;
                if (minutes <= 0) continue;

                if (hasOpen && !hasPlanning)
                {
                    result.Warnings.Add($"MES/OpenMin có {key.Item1} - {key.Item2} nhưng Planning không có. Vẫn tạo block theo MES.");
                }

                var functionName = hasOpen
                    ? open!.FunctionName
                    : planned?.FunctionName ?? string.Empty;

                result.Blocks.Add(new ProductBlock
                {
                    SourceId = $"S{sourceIndex++:0000}",
                    Code = key.Item1,
                    ProductionGroup = key.Item2,
                    FunctionName = functionName,
                    TotalMinutesRequired = Math.Round(minutes, 2),
                    AllocatedMinutes = Math.Round(minutes, 2),
                    DisplayColor = CreateColorForKey(key.Item1, key.Item2)
                });
            }

            var configuredDeadlines = deadlines
                .Select(d => NormalizeProductionGroup(d.GroupNumber))
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            result.MissingDeadlineGroups = result.Blocks
                .Select(b => NormalizeProductionGroup(b.ProductionGroup))
                .Where(g => !configuredDeadlines.Contains(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();

            return result;
        }

        public List<string> GetRequiredProductionGroups()
        {
            var planning = JsonStorage.Load<List<PlanningBlockData>>(PlanningBlocksFile)
                .Select(b => b.ProductionGroup);
            var openMinutes = JsonStorage.Load<List<OpenMinutesBlockData>>(OpenMinutesFile)
                .Select(b => b.ProductionGroup);

            return planning.Concat(openMinutes)
                .Select(NormalizeProductionGroup)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();
        }

        public List<ModuleMappingData> SaveManualMappings(IEnumerable<ModuleMappingData> mappings)
        {
            var existing = JsonStorage.Load<List<ModuleMappingData>>(ModuleMappingsFile);
            var map = existing
                .Where(m => !string.IsNullOrWhiteSpace(m.FP))
                .GroupBy(m => NormalizeKey(m.FP))
                .ToDictionary(g => g.Key, g => g.Last());

            foreach (var mapping in mappings)
            {
                var fp = NormalizeKey(mapping.FP);
                if (string.IsNullOrWhiteSpace(fp)) continue;

                map[fp] = new ModuleMappingData
                {
                    FP = fp,
                    BuildGroup = NormalizeKey(mapping.BuildGroup),
                    FunctionName = mapping.FunctionName.Trim(),
                    IsManual = true
                };
            }

            var saved = map.Values
                .OrderBy(m => m.BuildGroup)
                .ThenBy(m => m.FP)
                .ToList();
            JsonStorage.Save(ModuleMappingsFile, saved);
            EnsureBuildGroupSettings(saved);
            SyncLegacyProductGroups(saved);
            return saved;
        }

        // Backward-compatible wrapper for old command names while the UI is migrated.
        public void ImportMarketing(string filePath, string sheetName = "Sheet1")
        {
            ImportPlanning(filePath);
        }

        public List<ProductBlock> GenerateBlocksFromData()
        {
            return GenerateBlocksFromDataV2().Blocks;
        }

        private static DataTable ReadFirstTable(string filePath, bool useHeaderRow)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = (_) => new ExcelDataTableConfiguration { UseHeaderRow = useHeaderRow }
            });

            return dataSet.Tables.Count > 0
                ? dataSet.Tables[0]
                : throw new Exception("File Excel không có dữ liệu.");
        }

        private static DataTable ReadTable(string filePath, string sheetName, bool useHeaderRow)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = (_) => new ExcelDataTableConfiguration { UseHeaderRow = useHeaderRow }
            });

            if (!dataSet.Tables.Contains(sheetName))
                throw new Exception($"Không tìm thấy sheet '{sheetName}'.");

            return dataSet.Tables[sheetName]!;
        }

        private Dictionary<string, ModuleMappingData> LoadMappingDictionary()
        {
            var mappings = JsonStorage.Load<List<ModuleMappingData>>(ModuleMappingsFile);
            if (mappings.Count == 0)
                throw new Exception("Chưa import Module List. Hãy import module_list.xlsx trước.");

            return mappings
                .Where(m => !string.IsNullOrWhiteSpace(m.FP))
                .GroupBy(m => NormalizeKey(m.FP))
                .ToDictionary(g => g.Key, g => g.Last());
        }

        private static bool TryResolveMapping(
            string productId,
            Dictionary<string, ModuleMappingData> mappings,
            out ModuleMappingData mapping,
            out string fp)
        {
            mapping = new ModuleMappingData();
            fp = ExtractFp(productId);
            if (string.IsNullOrWhiteSpace(fp)) return false;
            return mappings.TryGetValue(fp, out mapping!);
        }

        private static string ExtractFp(string productId)
        {
            var normalized = NormalizeKey(productId);
            return normalized.Length >= 7 ? normalized[..7] : string.Empty;
        }

        private static Dictionary<int, string> FindPlanningGrColumns(DataTable table)
        {
            var result = new Dictionary<int, string>();
            for (int r = 0; r < Math.Min(10, table.Rows.Count); r++)
            {
                var foundGroupRange = false;
                for (int c = 7; c < table.Columns.Count; c++)
                {
                    if (!TryNormalizeStrictProductionGroup(GetCell(table.Rows[r], c), out var group))
                    {
                        if (foundGroupRange) break;
                        continue;
                    }

                    foundGroupRange = true;
                    result[c] = group;
                }

                if (result.Count > 0) return result;
            }

            return result;
        }

        private static int FindPlanningHeaderRow(DataTable table, IEnumerable<int> grColumns)
        {
            var cols = grColumns.ToList();
            for (int r = 0; r < Math.Min(10, table.Rows.Count); r++)
            {
                if (cols.Any(c => TryNormalizeStrictProductionGroup(GetCell(table.Rows[r], c), out _)))
                    return r;
            }

            return 0;
        }

        private static List<string> GetBuildGroupFunctionWarnings(IEnumerable<ModuleMappingData> mappings)
        {
            return mappings
                .GroupBy(m => m.BuildGroup)
                .Where(g => g.Select(x => x.FunctionName).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                .Select(g => $"BuildGroup {g.Key} có nhiều Function khác nhau trong module list.")
                .ToList();
        }

        private static void EnsureBuildGroupSettings(List<ModuleMappingData> mappings)
        {
            var existing = JsonStorage.Load<List<BuildGroupShiftSettingData>>(BuildGroupSettingsFile);
            var settings = existing
                .Where(s => !string.IsNullOrWhiteSpace(s.BuildGroup))
                .GroupBy(s => NormalizeKey(s.BuildGroup))
                .ToDictionary(g => g.Key, g => g.Last());

            foreach (var group in mappings.GroupBy(m => m.BuildGroup))
            {
                if (settings.TryGetValue(group.Key, out var current))
                {
                    current.FunctionName = group.First().FunctionName;
                    continue;
                }

                settings[group.Key] = new BuildGroupShiftSettingData
                {
                    BuildGroup = group.Key,
                    FunctionName = group.First().FunctionName,
                    UseShiftA = true,
                    UseShiftB = false,
                    WorkersA = 1,
                    WorkersB = 1
                };
            }

            JsonStorage.Save(BuildGroupSettingsFile, settings.Values.OrderBy(s => s.BuildGroup).ToList());
        }

        private static void SyncLegacyProductGroups(List<ModuleMappingData> mappings)
        {
            var groups = mappings
                .GroupBy(m => m.BuildGroup)
                .Select(g => new ProductGroupData
                {
                    GroupId = g.Key,
                    Name = g.First().FunctionName,
                    ProductionGroup = string.Empty
                })
                .OrderBy(g => g.GroupId)
                .ToList();

            JsonStorage.Save("productGroups.json", groups);
        }

        private static string GetCell(DataRow row, int index)
        {
            if (index < 0 || index >= row.ItemArray.Length) return string.Empty;
            return row[index]?.ToString()?.Trim() ?? string.Empty;
        }

        private static double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            var normalized = value.Trim().Replace(",", "");
            return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static bool IsU4(string value)
        {
            return string.Equals(value?.Trim(), "U4", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHeaderRow(string functionName, string buildGroup, string fp)
        {
            return functionName.Contains("part", StringComparison.OrdinalIgnoreCase) ||
                   buildGroup.Contains("build", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fp, "FP", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeKey(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeProductionGroup(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var match = Regex.Match(text, @"Gr[\.\s]*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success) return $"Gr.{match.Groups[1].Value}";

            var numericMatch = Regex.Match(text, @"^\d{3,}$");
            if (numericMatch.Success) return $"Gr.{numericMatch.Value}";

            return text;
        }

        private static bool TryNormalizeStrictProductionGroup(string value, out string group)
        {
            group = string.Empty;
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return false;

            var match = Regex.Match(text, @"^Gr[\.\s]*(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                group = $"Gr.{match.Groups[1].Value}";
                return true;
            }

            var numericMatch = Regex.Match(text, @"^\d{3,}$");
            if (numericMatch.Success)
            {
                group = $"Gr.{numericMatch.Value}";
                return true;
            }

            return false;
        }

        private static bool IsValidProductionGroup(string value)
        {
            return TryNormalizeStrictProductionGroup(value, out _);
        }

        private static System.Windows.Media.SolidColorBrush CreateColorForKey(string buildGroup, string productionGroup)
        {
            var hash = Math.Abs($"{buildGroup}|{productionGroup}".GetHashCode());
            byte r = (byte)(150 + hash % 90);
            byte g = (byte)(150 + (hash / 7) % 90);
            byte b = (byte)(150 + (hash / 13) % 90);
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }
    }
}
