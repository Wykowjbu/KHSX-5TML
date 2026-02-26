using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ProductionLine : ObservableObject
    {
        [ObservableProperty]
        private string lineName = string.Empty;

        [ObservableProperty]
        private ShiftConfig defaultShiftA = new ShiftConfig { Workers = 1, Minutes = 480 };

        [ObservableProperty]
        private ShiftConfig defaultShiftB = new ShiftConfig { Workers = 1, Minutes = 480 };

        public ObservableCollection<DayCell> Days { get; } = new ObservableCollection<DayCell>();
        
        public ProductionLine(string name)
        {
            LineName = name;
            
            // Không tự động apply khi thay đổi default shift
            // User phải bấm nút "Áp dụng cho tất cả" để apply
        }

        public void ApplyDefaultShiftToAllDays()
        {
            foreach (var day in Days)
            {
                day.ShiftA.Workers = DefaultShiftA.Workers;
                day.ShiftA.Minutes = DefaultShiftA.Minutes;
                day.ShiftB.Workers = DefaultShiftB.Workers;
                day.ShiftB.Minutes = DefaultShiftB.Minutes;
                day.HasCustomConfig = false; // Reset tất cả về mặc định
            }
        }
    }
}
