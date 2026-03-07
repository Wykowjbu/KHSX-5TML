using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ShiftConfig : ObservableObject
    {
        [ObservableProperty]
        private double workers = 1; // Số người làm việc (cho phép số thập phân)

        [ObservableProperty]
        private double minutes = 480; // Số phút làm việc (8 giờ mặc định)

        [ObservableProperty]
        private double efficiency = 1.15; // Hiệu suất (mặc định 115%)

        public double TotalCapacity => Workers * Minutes * Efficiency; // Tổng công suất

        partial void OnWorkersChanged(double value)
        {
            OnPropertyChanged(nameof(TotalCapacity));
        }

        partial void OnMinutesChanged(double value)
        {
            OnPropertyChanged(nameof(TotalCapacity));
        }

        partial void OnEfficiencyChanged(double value)
        {
            OnPropertyChanged(nameof(TotalCapacity));
        }
    }
}
