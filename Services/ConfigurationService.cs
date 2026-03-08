using KHSX.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KHSX.Services
{
    public class ConfigurationService
    {
        public class FactoryConfig
        {
            public DateTime StartDate { get; set; } = DateTime.Today;
            public DateTime DeadlineDate { get; set; } = DateTime.Today.AddDays(7);
            public List<RowConfig> Rows { get; set; } = new();
        }

        public class RowConfig
        {
            public string RowName { get; set; } = string.Empty;
            public string DisplayIndex { get; set; } = string.Empty;
            public string ParentLineName { get; set; } = string.Empty;
            public string ShiftName { get; set; } = string.Empty;
            public ShiftData DefaultConfig { get; set; } = new();
            public List<DayCellConfig> Days { get; set; } = new();
        }

        public class DayCellConfig
        {
            public DateTime Date { get; set; }
            public bool HasCustomConfig { get; set; }
            public bool IsDayOff { get; set; }
            public ShiftData Config { get; set; } = new();
        }

        public class ShiftData
        {
            public double Workers { get; set; } = 1;
            public double Minutes { get; set; } = 480;
            public double Efficiency { get; set; } = 1.15;
        }

        public void SaveConfiguration(DateTime startDate, DateTime deadlineDate, IEnumerable<ShiftRow> rows)
        {
            var config = new FactoryConfig
            {
                StartDate = startDate,
                DeadlineDate = deadlineDate,
                Rows = rows.Select(row => new RowConfig
                {
                    RowName = row.RowName,
                    DisplayIndex = row.DisplayIndex,
                    ParentLineName = row.ParentLineName,
                    ShiftName = row.ShiftName,
                    DefaultConfig = new ShiftData
                    {
                        Workers = row.DefaultConfig.Workers,
                        Minutes = row.DefaultConfig.Minutes,
                        Efficiency = row.DefaultConfig.Efficiency
                    },
                    Days = row.Days.Select(day => new DayCellConfig
                    {
                        Date = day.Date,
                        HasCustomConfig = day.HasCustomConfig,
                        IsDayOff = day.IsDayOff,
                        Config = new ShiftData
                        {
                            Workers = day.Config.Workers,
                            Minutes = day.Config.Minutes,
                            Efficiency = day.Config.Efficiency
                        }
                    }).ToList()
                }).ToList()
            };

            JsonStorage.Save("factory/lines.json", config);
        }

        public FactoryConfig LoadConfiguration()
        {
            var config = JsonStorage.Load<FactoryConfig>("factory/lines.json");
            if (config?.Rows == null || config.Rows.Count == 0) return null;
            return config;
        }

        public void ApplyConfiguration(FactoryConfig config, ObservableCollection<ShiftRow> rows, Action<DateTime> setStartDate, Action<DateTime> setDeadlineDate)
        {
            if (config == null)
                return;

            setStartDate(config.StartDate);
            setDeadlineDate(config.DeadlineDate);

            // Clear existing rows
            rows.Clear();

            // Recreate rows from config
            foreach (var rowConfig in config.Rows)
            {
                var row = new ShiftRow(rowConfig.ParentLineName, rowConfig.ShiftName);
                if (!string.IsNullOrEmpty(rowConfig.RowName))
                {
                    row.RowName = rowConfig.RowName;
                }
                if (!string.IsNullOrEmpty(rowConfig.DisplayIndex))
                {
                    row.DisplayIndex = rowConfig.DisplayIndex;
                }
                
                row.DefaultConfig.Workers = rowConfig.DefaultConfig.Workers;
                row.DefaultConfig.Minutes = rowConfig.DefaultConfig.Minutes;
                row.DefaultConfig.Efficiency = rowConfig.DefaultConfig.Efficiency;
                
                foreach (var dayConfig in rowConfig.Days)
                {
                    var day = new DayCell(dayConfig.Date);
                    day.HasCustomConfig = dayConfig.HasCustomConfig;
                    // Nếu dữ liệu cũ chưa có IsDayOff (default false) và chưa custom → dùng mặc định theo lịch
                    if (!dayConfig.IsDayOff && !dayConfig.HasCustomConfig && day.IsWeekend)
                        day.IsDayOff = true;
                    else
                        day.IsDayOff = dayConfig.IsDayOff;
                    day.Config.Workers = dayConfig.Config.Workers;
                    day.Config.Minutes = dayConfig.Config.Minutes;
                    day.Config.Efficiency = dayConfig.Config.Efficiency > 0 ? dayConfig.Config.Efficiency : 1.15;
                    row.Days.Add(day);
                }
                
                rows.Add(row);
            }
        }
    }
}
