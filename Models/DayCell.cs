using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class DayCell : ObservableObject
    {
        private const double EfficiencyRate = 1.15; // 115% hiệu suất

        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private bool isWeekend;

        [ObservableProperty]
        private bool isDeadline;

        [ObservableProperty]
        private bool hasCustomConfig = false; // Đánh dấu cell này có cấu hình riêng

        [ObservableProperty]
        private ShiftConfig config = new ShiftConfig { Workers = 1, Minutes = 480 };

        public double TotalCapacity => Config.TotalCapacity * EfficiencyRate;

        public ObservableCollection<ProductBlock> Blocks { get; } = new ObservableCollection<ProductBlock>();

        public DayCell(DateTime date)
        {
            Date = date;
            IsWeekend = date.DayOfWeek == DayOfWeek.Sunday;
            
            Blocks.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(TotalUsed));
                OnPropertyChanged(nameof(AvailableMinutes));
                OnPropertyChanged(nameof(WatermarkText));
            };

            // Listen to shift config changes
            Config.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TotalCapacity));
                OnPropertyChanged(nameof(AvailableMinutes));
                OnPropertyChanged(nameof(WatermarkText));
            };
        }

        public double TotalUsed => Blocks.Sum(b => b.AllocatedMinutes);

        public double AvailableMinutes => Math.Max(0, TotalCapacity - TotalUsed);

        public string WatermarkText => IsWeekend ? "Nghỉ" : 
            $"{Config.Workers:0.##}per({Config.Minutes:0.##}){(HasCustomConfig ? "*" : "")}\n" +
            $"Total: {TotalCapacity:0.#}";
    }
}
