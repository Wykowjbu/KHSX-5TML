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
        private ShiftConfig shiftA = new ShiftConfig { Workers = 1, Minutes = 480 };

        [ObservableProperty]
        private ShiftConfig shiftB = new ShiftConfig { Workers = 1, Minutes = 480 };

        public double TotalCapacity => (ShiftA.TotalCapacity + ShiftB.TotalCapacity) * EfficiencyRate;

        public ObservableCollection<ProductBlock> Blocks { get; } = new ObservableCollection<ProductBlock>();

        public DayCell(DateTime date)
        {
            Date = date;
            IsWeekend = date.DayOfWeek == DayOfWeek.Sunday;
            Blocks.CollectionChanged += (s, e) => 
            {
                OnPropertyChanged(nameof(ShiftAUsed));
                OnPropertyChanged(nameof(ShiftBUsed));
                OnPropertyChanged(nameof(TotalUsed));
                OnPropertyChanged(nameof(WatermarkText));
                OnPropertyChanged(nameof(AvailableMinutes));
            };

            // Listen to shift config changes
            ShiftA.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TotalCapacity));
                OnPropertyChanged(nameof(AvailableMinutes));
                OnPropertyChanged(nameof(WatermarkText));
                OnPropertyChanged(nameof(ShiftAUsed));
                OnPropertyChanged(nameof(ShiftBUsed));
            };

            ShiftB.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TotalCapacity));
                OnPropertyChanged(nameof(AvailableMinutes));
                OnPropertyChanged(nameof(WatermarkText));
                OnPropertyChanged(nameof(ShiftAUsed));
                OnPropertyChanged(nameof(ShiftBUsed));
            };
        }

        public double TotalUsed => Blocks.Sum(b => b.AllocatedMinutes);

        public double ShiftAUsed => Math.Min(TotalUsed, ShiftA.TotalCapacity);

        public double ShiftBUsed => Math.Max(0, TotalUsed - ShiftA.TotalCapacity);

        public double AvailableMinutes => TotalCapacity - TotalUsed;

        public string WatermarkText => IsWeekend ? "Nghỉ" : 
            $"A:{ShiftA.Workers}per({ShiftA.Minutes})\n" +
            $"B:{ShiftB.Workers}per({ShiftB.Minutes})\n" +
            $"Total: {Math.Round(TotalCapacity)}";
    }
}
