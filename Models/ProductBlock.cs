using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ProductBlock : ObservableObject
    {
        [ObservableProperty]
        private string sourceId = string.Empty;

        [ObservableProperty]
        private string code = string.Empty; // Mã sản phẩm

        [ObservableProperty]
        private double totalMinutesRequired; // Tổng số phút yêu cầu ban đầu của cả Item

        [ObservableProperty]
        private double allocatedMinutes; // Số phút được cấp phát cho khối này

        [ObservableProperty]
        private Brush displayColor = Brushes.LightBlue; // Màu hiển thị trên UI

        [ObservableProperty]
        private bool isExceedingDeadline; // Đánh dấu khối này có vượt qua deadline không

        // Dùng để identify block này khi kéo thả
        public Guid Id { get; } = Guid.NewGuid();

        // Thuộc tính để nhận diện block gốc (danh sách chờ) hay đoạn cắt ra (trên lưới)
        public Guid? ParentId { get; set; }
        
        // Tên hiển thị trên lưới hoặc trên block chờ
        public string DisplayText => $"{Code} ({AllocatedMinutes}m)";

        public ProductBlock CloneWithSplit(double minutesToSplit)
        {
            return new ProductBlock
            {
                ParentId = this.ParentId ?? this.Id,
                SourceId = this.SourceId,
                Code = this.Code,
                TotalMinutesRequired = this.TotalMinutesRequired,
                AllocatedMinutes = minutesToSplit,
                DisplayColor = this.DisplayColor
            };
        }
    }
}
