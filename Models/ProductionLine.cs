using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ProductionLine : ObservableObject
    {
        [ObservableProperty]
        private string lineName = string.Empty;

        public ObservableCollection<DayCell> Days { get; } = new ObservableCollection<DayCell>();
        
        public ProductionLine(string name)
        {
            LineName = name;
        }
    }
}
