using KHSX.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace KHSX.Services
{
    public class ConfigurationService
    {
        private const string ConfigFileName = "production_config.json";

        public class ProductionConfig
        {
            public DateTime StartDate { get; set; }
            public DateTime DeadlineDate { get; set; }
            public List<LineConfig> Lines { get; set; } = new();
        }

        public class LineConfig
        {
            public string LineName { get; set; }
            public ShiftData DefaultShiftA { get; set; } = new();
            public ShiftData DefaultShiftB { get; set; } = new();
            public List<DayCellConfig> Days { get; set; } = new();
        }

        public class DayCellConfig
        {
            public DateTime Date { get; set; }
            public bool HasCustomConfig { get; set; }
            public ShiftData ShiftA { get; set; } = new();
            public ShiftData ShiftB { get; set; } = new();
        }

        public class ShiftData
        {
            public double Workers { get; set; }
            public double Minutes { get; set; }
        }

        public void SaveConfiguration(DateTime startDate, DateTime deadlineDate, IEnumerable<ProductionLine> lines)
        {
            var config = new ProductionConfig
            {
                StartDate = startDate,
                DeadlineDate = deadlineDate,
                Lines = lines.Select(line => new LineConfig
                {
                    LineName = line.LineName,
                    DefaultShiftA = new ShiftData
                    {
                        Workers = line.DefaultShiftA.Workers,
                        Minutes = line.DefaultShiftA.Minutes
                    },
                    DefaultShiftB = new ShiftData
                    {
                        Workers = line.DefaultShiftB.Workers,
                        Minutes = line.DefaultShiftB.Minutes
                    },
                    Days = line.Days.Select(day => new DayCellConfig
                    {
                        Date = day.Date,
                        HasCustomConfig = day.HasCustomConfig,
                        ShiftA = new ShiftData
                        {
                            Workers = day.ShiftA.Workers,
                            Minutes = day.ShiftA.Minutes
                        },
                        ShiftB = new ShiftData
                        {
                            Workers = day.ShiftB.Workers,
                            Minutes = day.ShiftB.Minutes
                        }
                    }).ToList()
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigFileName, json);
        }

        public ProductionConfig LoadConfiguration()
        {
            if (!File.Exists(ConfigFileName))
                return null;

            try
            {
                var json = File.ReadAllText(ConfigFileName);
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<ProductionConfig>(json, options);
            }
            catch
            {
                return null;
            }
        }

        public void ApplyConfiguration(ProductionConfig config, ObservableCollection<ProductionLine> lines, Action<DateTime> setStartDate, Action<DateTime> setDeadlineDate)
        {
            if (config == null)
                return;

            setStartDate(config.StartDate);
            setDeadlineDate(config.DeadlineDate);

            // Clear existing lines
            lines.Clear();

            // Recreate lines from config
            foreach (var lineConfig in config.Lines)
            {
                var line = new ProductionLine(lineConfig.LineName);
                line.DefaultShiftA.Workers = lineConfig.DefaultShiftA.Workers;
                line.DefaultShiftA.Minutes = lineConfig.DefaultShiftA.Minutes;
                line.DefaultShiftB.Workers = lineConfig.DefaultShiftB.Workers;
                line.DefaultShiftB.Minutes = lineConfig.DefaultShiftB.Minutes;
                
                foreach (var dayConfig in lineConfig.Days)
                {
                    var day = new DayCell(dayConfig.Date);
                    day.HasCustomConfig = dayConfig.HasCustomConfig;
                    day.ShiftA.Workers = dayConfig.ShiftA.Workers;
                    day.ShiftA.Minutes = dayConfig.ShiftA.Minutes;
                    day.ShiftB.Workers = dayConfig.ShiftB.Workers;
                    day.ShiftB.Minutes = dayConfig.ShiftB.Minutes;
                    line.Days.Add(day);
                }
                
                lines.Add(line);
            }
        }
    }
}
