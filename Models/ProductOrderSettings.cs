using System.Collections.Generic;

namespace KHSX.Models
{
    /// <summary>
    /// Lưu thứ tự ưu tiên SP trong cùng 1 block (GroupId).
    /// Được lưu vào productOrderSettings.json qua JsonStorage.
    /// </summary>
    public class ProductOrderSettings
    {
        /// <summary>Key = GroupId (mã block), Value = danh sách ProductId theo thứ tự ưu tiên</summary>
        public Dictionary<string, List<string>> BlockProductOrder { get; set; } = new();
    }
}
