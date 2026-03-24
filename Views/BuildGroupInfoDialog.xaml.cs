using KHSX.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace KHSX.Views
{
    public partial class BuildGroupInfoDialog : Window
    {
        public BuildGroupInfoDialog(List<BuildGroupInfoItem> items)
        {
            InitializeComponent();
            
            // Tính toán tổng số phút của mỗi nhóm nạp vào Header
            // Dùng ListCollectionView để Group 
            var collectionView = new ListCollectionView(items);
            collectionView.GroupDescriptions.Add(new PropertyGroupDescription("GroupId"));
            
            // Do WPF GroupHeader chỉ bind được Name (của PropertyGroupDescription)
            // Ta cần chỉnh lại Model hoặc tạo view helper. 
            // Giải pháp đơn giản: Gom tay lại thành Name = "GroupId - Tổng: xxx phút"
            
            var groupedItems = items.GroupBy(x => x.GroupId).ToList();
            var displayList = new List<BuildGroupDisplayItem>();

            foreach (var group in groupedItems)
            {
                double totalMin = group.Sum(x => x.OpenMinutes);
                string groupHeader = $"{group.Key} (Tổng: {totalMin:0.##} phút)";
                
                foreach (var item in group)
                {
                    displayList.Add(new BuildGroupDisplayItem
                    {
                        GroupHeader = groupHeader,
                        ProductId = item.ProductId,
                        GroupId = item.GroupId,
                        OpenMinutes = item.OpenMinutes
                    });
                }
            }

            var displayView = new ListCollectionView(displayList);
            displayView.GroupDescriptions.Add(new PropertyGroupDescription("GroupHeader"));
            ProductListGrid.ItemsSource = displayView;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class BuildGroupDisplayItem
    {
        public string GroupHeader { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public double OpenMinutes { get; set; }
    }
}
