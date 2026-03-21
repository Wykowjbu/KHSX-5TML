using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class DaySummary : ObservableObject
    {
        [ObservableProperty]
        private DateTime date;

        [ObservableProperty]
        private double totalWorkers;

        [ObservableProperty]
        private bool isDayOff;

        public DaySummary(DateTime date)
        {
            Date = date;
        }

        public string DisplayText => TotalWorkers > 0 ? $"Tổng = {TotalWorkers:0.##}" : "0";

        partial void OnTotalWorkersChanged(double value)
        {
            OnPropertyChanged(nameof(DisplayText));
        }

        partial void OnIsDayOffChanged(bool value)
        {
            OnPropertyChanged(nameof(DisplayText));
        }
    }
}
