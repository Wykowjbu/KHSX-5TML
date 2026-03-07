using System;
using System.Collections.Generic;

namespace KHSX.Models
{
    // Cấu trúc Data models như tài liệu json design yêu cầu
    public class ProductData
    {
        public string ProductId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty; // Dùng để gom block (Cột K)
        public string ProductionGroup { get; set; } = string.Empty; // Gr.xxx (Cột E, F, H, I, J)
        public string Function { get; set; } = string.Empty;
        public double MinutesPerProduct { get; set; }
        
        // Cần thiết cho việc lưu và merge (Theo 07-thiet-ke-luu-tru-thong-tin-bang-json.md)
        public Dictionary<string, double> QuantitiesByGroup { get; set; } = new Dictionary<string, double>();
        public double TotalQuantity { get; set; }
    }

    public class ProductGroupData
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProductionGroup { get; set; } = string.Empty; // Gr.xxx (Cột E, F, H, I, J)
    }

    public class OpenMinutesData
    {
        public string ProductId { get; set; } = string.Empty;
        public double OpenMinutes { get; set; }
    }

    public class DeadlineData
    {
        public string GroupNumber { get; set; } = string.Empty; // Trong doc dung groupNumber (Gr.xxx)
        public DateTime Deadline { get; set; }
    }

    public class BlockData
    {
        public Guid BlockId { get; set; }
        public Guid? ParentId { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty; // ProductGroup
        public string ProductionGroup { get; set; } = string.Empty; // Gr.xxx
        public string FunctionName { get; set; } = string.Empty; // Tên fuction từ cấu hình Groups
        public double TotalMinutesRequired { get; set; }
        public double AllocatedMinutes { get; set; }
        public string DisplayColorHex { get; set; } = string.Empty;
    }

    public class ScheduleData
    {
        public string RowId { get; set; } = string.Empty; 
        public DateTime Date { get; set; }
        public BlockData BlockInfo { get; set; }
    }

    public class SettingsData
    {
        public string CurrentMESGroup { get; set; } = string.Empty;
        public double Efficiency { get; set; } = 1.15;
        public double DefaultShiftMinutes { get; set; } = 480;
    }
}
