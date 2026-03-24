using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using KHSX.Models;
using KHSX.Services;

namespace KHSX.Views
{
    /// <summary>
    /// Dialog cài đặt thứ tự SP trong từng block.
    /// Dữ liệu lưu vào productOrderSettings.json.
    /// </summary>
    public partial class ProductOrderSettingsDialog : Window
    {
        public class ProductItem
        {
            public string ProductId { get; set; } = string.Empty;
            public string MinPerSPText { get; set; } = string.Empty;
        }

        public class BlockGroup
        {
            public string BlockCode { get; set; } = string.Empty;
            public ObservableCollection<ProductItem> Products { get; set; } = new();
        }

        public ObservableCollection<BlockGroup> BlockGroups { get; } = new();

        public ProductOrderSettingsDialog()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            var products = JsonStorage.Load<List<ProductData>>("products.json") ?? new();
            var openMinutes = JsonStorage.Load<List<OpenMinutesData>>("openMinutes.json") ?? new();
            var settings = JsonStorage.Load<ProductOrderSettings>("productOrderSettings.json") ?? new();

            var activeProductIds = new HashSet<string>(openMinutes.Where(o => o.OpenMinutes > 0).Select(o => o.ProductId));

            // Gom SP theo GroupId (block)
            var spByBlock = products
                .Where(p => activeProductIds.Contains(p.ProductId))
                .GroupBy(p => p.GroupId)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in spByBlock)
            {
                var group = new BlockGroup { BlockCode = kvp.Key };
                
                List<string> orderedIds;
                if (settings.BlockProductOrder.TryGetValue(kvp.Key, out var savedOrder))
                {
                    // Dùng thứ tự đã lưu, thêm SP mới vào cuối, loại SP ko còn
                    var currentIds = kvp.Value.Select(p => p.ProductId).ToHashSet();
                    orderedIds = savedOrder.Where(id => currentIds.Contains(id)).ToList();
                    var newIds = kvp.Value.Select(p => p.ProductId).Where(id => !orderedIds.Contains(id)).OrderBy(id => id);
                    orderedIds.AddRange(newIds);
                }
                else
                {
                    orderedIds = kvp.Value.Select(p => p.ProductId).OrderBy(id => id).ToList();
                }

                foreach (var id in orderedIds)
                {
                    var prod = kvp.Value.FirstOrDefault(p => p.ProductId == id);
                    group.Products.Add(new ProductItem
                    {
                        ProductId = id,
                        MinPerSPText = prod != null && prod.MinutesPerProduct > 0
                            ? $"{prod.MinutesPerProduct}'/sp"
                            : "—"
                    });
                }

                if (group.Products.Count > 0)
                    BlockGroups.Add(group);
            }

            BlocksPanel.ItemsSource = BlockGroups;
        }

        private void MoveProductUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ProductItem item)
            {
                foreach (var group in BlockGroups)
                {
                    int index = group.Products.IndexOf(item);
                    if (index > 0)
                    {
                        group.Products.Move(index, index - 1);
                        break;
                    }
                }
            }
        }

        private void MoveProductDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ProductItem item)
            {
                foreach (var group in BlockGroups)
                {
                    int index = group.Products.IndexOf(item);
                    if (index >= 0 && index < group.Products.Count - 1)
                    {
                        group.Products.Move(index, index + 1);
                        break;
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = new ProductOrderSettings();
            foreach (var group in BlockGroups)
            {
                settings.BlockProductOrder[group.BlockCode] =
                    group.Products.Select(p => p.ProductId).ToList();
            }

            JsonStorage.Save("productOrderSettings.json", settings);
            MessageBox.Show("Đã lưu thứ tự sản phẩm!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
