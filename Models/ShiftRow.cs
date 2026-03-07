using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ShiftRow : ObservableObject
    {
        [ObservableProperty]
        private string rowName = string.Empty;

        public string ParentLineName { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty; // "A" or "B"

        [ObservableProperty]
        private ShiftConfig defaultConfig = new ShiftConfig { Workers = 1, Minutes = 480 };

        public ObservableCollection<DayCell> Days { get; } = new ObservableCollection<DayCell>();
        
        public ShiftRow(string parentLineName, string shiftName)
        {
            ParentLineName = parentLineName;
            ShiftName = shiftName;
            RowName = $"{parentLineName} - Ca {shiftName}";
        }

        public void ApplyDefaultShiftToAllDays()
        {
            foreach (var day in Days)
            {
                day.Config.Workers = DefaultConfig.Workers;
                day.Config.Minutes = DefaultConfig.Minutes;
                day.HasCustomConfig = false; // Reset tất cả về mặc định
            }
        }
    }
}
