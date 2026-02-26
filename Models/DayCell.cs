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
        private bool isDeadline;

        // Khởi tạo giới hạn thời gian cơ bản của ca
        private const double MaxShiftAMinutes = 480;
        private const double MaxShiftBMinutes = 480;

        public double TotalCapacity => MaxShiftAMinutes + MaxShiftBMinutes;

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
        }

        public double TotalUsed => Blocks.Sum(b => b.AllocatedMinutes);

        public double ShiftAUsed => Math.Min(TotalUsed, MaxShiftAMinutes);

        public double ShiftBUsed => Math.Max(0, TotalUsed - MaxShiftAMinutes);

        public double AvailableMinutes => TotalCapacity - TotalUsed;

        public string WatermarkText => IsWeekend ? "Nghỉ" : $"{Math.Round(ShiftAUsed)}/{MaxShiftAMinutes} | {Math.Round(ShiftBUsed)}/{MaxShiftBMinutes} | {Math.Round(TotalUsed)}";
    }
}
