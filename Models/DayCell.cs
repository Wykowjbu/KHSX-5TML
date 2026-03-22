using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class DayCell : ObservableObject
    {
        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private bool isWeekend;

        [ObservableProperty]
        private bool isDayOff;

        [ObservableProperty]
        private bool isWithinLineDeadline; // True = ngày nghỉ (không lên lịch được)

        [ObservableProperty]
        private bool isDeadline;

        [ObservableProperty]
        private bool hasCustomConfig = false; // Đánh dấu cell này có cấu hình riêng

        [ObservableProperty]
        private ShiftConfig config = new ShiftConfig { Workers = 1, Minutes = 480 };

        public double TotalCapacity => Config.TotalCapacity;

        public ObservableCollection<ProductBlock> Blocks { get; } = new ObservableCollection<ProductBlock>();

        public DayCell(DateTime date)
        {
            Date = date;
            IsWeekend = date.DayOfWeek == DayOfWeek.Sunday;
            IsDayOff = IsWeekend; // Chủ nhật mặc định nghỉ
            
            Blocks.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(TotalUsed));
                OnPropertyChanged(nameof(AvailableMinutes));
                OnPropertyChanged(nameof(WatermarkText));
                OnPropertyChanged(nameof(HasAvailableCapacity));
            };

            // Listen to shift config changes
            Config.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TotalCapacity));
                OnPropertyChanged(nameof(AvailableMinutes));
                OnPropertyChanged(nameof(WatermarkText));
                OnPropertyChanged(nameof(HasAvailableCapacity));
            };
        }

        public double TotalUsed => Blocks.Sum(b => b.AllocatedMinutes);

        public double AvailableMinutes => Math.Max(0, TotalCapacity - TotalUsed);

        public bool HasAvailableCapacity => !IsDayOff && AvailableMinutes >= 1.0 && IsWithinLineDeadline;

        public string WatermarkText => IsDayOff ? "Nghỉ" : 
            $"{Config.Workers:0.##}per({Config.Minutes:0.##})x{Config.Efficiency:0.##}{(HasCustomConfig ? "*" : "")}\n" +
            $"Total: {TotalCapacity:0.#}";

        partial void OnIsDayOffChanged(bool value)
        {
            OnPropertyChanged(nameof(WatermarkText));
            OnPropertyChanged(nameof(TotalCapacity));
            OnPropertyChanged(nameof(AvailableMinutes));
            OnPropertyChanged(nameof(HasAvailableCapacity));
        }

        partial void OnIsWithinLineDeadlineChanged(bool value)
        {
            OnPropertyChanged(nameof(HasAvailableCapacity));
        }
    }
}
