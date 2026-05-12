using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KHSX.Models
{
    public partial class ProductBlock : ObservableObject
    {
        // Dùng để scale chiều rộng - block có AllocatedMinutes này sẽ có chiều rộng tối đa (100%)
        private const double MaxMinutesForScale = 50000; // 50000 phút = 100% chiều rộng
        private const double MinWidthPercent = 20;       // Chiều rộng tối thiểu 20%
        private const double MaxWidthPercent = 100;      // Chiều rộng tối đa 100%

        [ObservableProperty]
        private string sourceId = string.Empty;

        [ObservableProperty]
        private string code = string.Empty; // Mã sản phẩm (ví dụ: ProductGroup)

        [ObservableProperty]
        private string productionGroup = string.Empty; // Mã Gr.xxx

        [ObservableProperty]
        private string functionName = string.Empty; // Tên fuction từ cấu hình Groups (Marketing)

        [ObservableProperty]
        private double totalMinutesRequired; // Tổng số phút yêu cầu ban đầu của cả Item

        [ObservableProperty]
        private double allocatedMinutes; // Số phút được cấp phát cho khối này

        [ObservableProperty]
        private Brush displayColor = Brushes.LightBlue; // Màu hiển thị trên UI

        [ObservableProperty]
        private bool isExceedingDeadline; // Đánh dấu khối này có vượt qua deadline không

        [ObservableProperty]
        private bool isCapacityOverflow; // Đánh dấu phần phút vượt capacity trong cùng cell

        // Dùng để identify block này khi kéo thả
        public Guid Id { get; set; } = Guid.NewGuid();

        // Thuộc tính để nhận diện block gốc (danh sách chờ) hay đoạn cắt ra (trên lưới)
        public Guid? ParentId { get; set; }

        public string DisplayColorHex
        {
            get
            {
                if (DisplayColor is SolidColorBrush solidBrush)
                    return solidBrush.Color.ToString();
                return "#FFADD8E6"; // Default LightBlue
            }
        }

        public ProductBlock() { }

        public ProductBlock(BlockData data)
        {
            Id = data.BlockId != Guid.Empty ? data.BlockId : Guid.NewGuid();
            ParentId = data.ParentId;
            SourceId = data.SourceId ?? string.Empty;
            Code = data.GroupId ?? string.Empty;
            ProductionGroup = data.ProductionGroup ?? string.Empty;
            FunctionName = data.FunctionName ?? string.Empty;
            TotalMinutesRequired = data.TotalMinutesRequired;
            AllocatedMinutes = data.AllocatedMinutes;
            IsCapacityOverflow = data.IsCapacityOverflow;
            IsExceedingDeadline = data.IsCapacityOverflow;
            
            if (!string.IsNullOrEmpty(data.DisplayColorHex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(data.DisplayColorHex);
                    DisplayColor = new SolidColorBrush(color);
                }
                catch
                {
                    DisplayColor = Brushes.LightBlue;
                }
            }
        }
        
        // Tên hiển thị trên lưới hoặc trên block chờ - làm tròn để tránh số .99
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(FunctionName))
                    return $"{FunctionName}\n{Code}\n({ProductionGroup})\n{AllocatedMinutes:0.##}m";
                return $"{Code}\n({ProductionGroup})\n{AllocatedMinutes:0.##}m";
            }
        }

        /// <summary>
        /// Tooltip hiển thị thông tin chi tiết cho block
        /// </summary>
        public string TooltipText
        {
            get
            {
                if (IsCapacityOverflow)
                {
                    return $"{Code}\n" +
                           $"Phút: {AllocatedMinutes:0.##}\n" +
                           $"⚠️ VƯỢT CAPACITY\n" +
                           $"Phần này vượt công suất của cell hiện tại.";
                }

                if (IsExceedingDeadline)
                {
                    return $"{Code}\n" +
                           $"Phút: {AllocatedMinutes:0.##}\n" +
                           $"⚠️ VƯỢT DEADLINE\n" +
                           $"💡 Kéo BuildGroup này sang cell khác để điều phối.\n" +
                           $"   Chỉ phần vượt deadline sẽ được di chuyển.";
                }
                return $"{Code}\n" +
                       $"Phút: {AllocatedMinutes:0.##}\n" +
                       $"Tổng yêu cầu: {TotalMinutesRequired:0.##}";
            }
        }

        // Tỷ lệ phần trăm chiều rộng dựa trên số phút (0.2 - 1.0)
        // Block có nhiều phút sẽ chiếm nhiều % chiều rộng hơn
        public double WidthPercentage
        {
            get
            {
                if (AllocatedMinutes <= 0)
                    return MinWidthPercent / 100.0;

                // Tính tỷ lệ phần trăm so với MaxMinutesForScale
                double ratio = Math.Min(AllocatedMinutes / MaxMinutesForScale, 1.0);
                
                // Dùng căn bậc 2 để tạo sự khác biệt rõ hơn giữa các block nhỏ
                double scaledRatio = Math.Sqrt(ratio);
                
                // Tính % trong khoảng Min-Max (0.2 - 1.0)
                double percent = (MinWidthPercent + (MaxWidthPercent - MinWidthPercent) * scaledRatio) / 100.0;
                
                return percent;
            }
        }

        // Cập nhật UI khi AllocatedMinutes thay đổi
        partial void OnAllocatedMinutesChanged(double value)
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(WidthPercentage));
            OnPropertyChanged(nameof(TooltipText));
        }

        // Cập nhật UI khi IsExceedingDeadline thay đổi
        partial void OnIsExceedingDeadlineChanged(bool value)
        {
            OnPropertyChanged(nameof(TooltipText));
        }

        partial void OnIsCapacityOverflowChanged(bool value)
        {
            OnPropertyChanged(nameof(TooltipText));
        }

        partial void OnFunctionNameChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayText));
        }

        partial void OnProductionGroupChanged(string value)
        {
            OnPropertyChanged(nameof(DisplayText));
        }

        public ProductBlock CloneWithSplit(double minutesToSplit)
        {
            return new ProductBlock
            {
                ParentId = this.ParentId ?? this.Id,
                SourceId = this.SourceId,
                Code = this.Code,
                ProductionGroup = this.ProductionGroup,
                FunctionName = this.FunctionName,
                TotalMinutesRequired = this.TotalMinutesRequired,
                AllocatedMinutes = Math.Round(minutesToSplit, 2),
                DisplayColor = this.DisplayColor,
                IsCapacityOverflow = this.IsCapacityOverflow
            };
        }
    }
}
