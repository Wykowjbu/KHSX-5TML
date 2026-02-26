using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ShiftConfig : ObservableObject
    {
        [ObservableProperty]
        private int workers = 1; // Số người làm việc

        [ObservableProperty]
        private double minutes = 480; // Số phút làm việc (8 giờ mặc định)

        public double TotalCapacity => Workers * Minutes; // Tổng công suất

        partial void OnWorkersChanged(int value)
        {
            OnPropertyChanged(nameof(TotalCapacity));
        }

        partial void OnMinutesChanged(double value)
        {
            OnPropertyChanged(nameof(TotalCapacity));
        }
    }
}
