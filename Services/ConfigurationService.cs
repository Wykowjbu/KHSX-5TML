using KHSX.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            public List<DayCellConfig> Days { get; set; } = new();
        }

        public class DayCellConfig
        {
            public DateTime Date { get; set; }
            public ShiftData ShiftA { get; set; } = new();
            public ShiftData ShiftB { get; set; } = new();
        }

        public class ShiftData
        {
            public int Workers { get; set; }
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
                    Days = line.Days.Select(day => new DayCellConfig
                    {
                        Date = day.Date,
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

        public void ApplyConfiguration(ProductionConfig config, IList<ProductionLine> lines, Action<DateTime> setStartDate, Action<DateTime> setDeadlineDate)
        {
            if (config == null)
                return;

            setStartDate(config.StartDate);
            setDeadlineDate(config.DeadlineDate);

            for (int i = 0; i < Math.Min(lines.Count, config.Lines.Count); i++)
            {
                var line = lines[i];
                var lineConfig = config.Lines[i];

                for (int d = 0; d < Math.Min(line.Days.Count, lineConfig.Days.Count); d++)
                {
                    var day = line.Days[d];
                    var dayConfig = lineConfig.Days[d];

                    if (day.Date.Date == dayConfig.Date.Date)
                    {
                        day.ShiftA.Workers = dayConfig.ShiftA.Workers;
                        day.ShiftA.Minutes = dayConfig.ShiftA.Minutes;
                        day.ShiftB.Workers = dayConfig.ShiftB.Workers;
                        day.ShiftB.Minutes = dayConfig.ShiftB.Minutes;
                    }
                }
            }
        }
    }
}
