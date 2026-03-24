using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KHSX.Models;
using KHSX.Services;

namespace KHSX.Views
{
    public partial class ConfigGroupsDialog : Window
    {
        private readonly List<ProductGroupData> _groups;
        private readonly Dictionary<string, TextBox> _nameBoxes = new();
        private readonly Dictionary<string, ComboBox> _groupBoxes = new();
        private readonly List<string> _availableGr;

        public ConfigGroupsDialog()
        {
            InitializeComponent();

            // Load dữ liệu
            _groups = JsonStorage.Load<List<ProductGroupData>>("productGroups.json") ?? new();
            var products = JsonStorage.Load<List<ProductData>>("products.json");

            // Tạo danh sách Gr.xxx
            _availableGr = new List<string> { "" };
            if (products != null)
            {
                var allGrs = products.SelectMany(p => p.QuantitiesByGroup.Keys).Distinct().OrderBy(g => g);
                _availableGr.AddRange(allGrs);
            }

            // Setup global group selector
            GlobalGroupSelector.ItemsSource = _availableGr;
            var initialMaxGr = _availableGr.Where(g => !string.IsNullOrEmpty(g)).OrderByDescending(g => g).FirstOrDefault();
            if (!string.IsNullOrEmpty(initialMaxGr)) GlobalGroupSelector.SelectedItem = initialMaxGr;

            // Build grid rows
            BuildGrid(products);
        }

        private void BuildGrid(List<ProductData>? products)
        {
            int rowIdx = 0;
            foreach (var group in _groups)
            {
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Column 0: Group ID
                var idLabel = new TextBlock
                {
                    Text = group.GroupId,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5)
                };
                Grid.SetRow(idLabel, rowIdx);
                Grid.SetColumn(idLabel, 0);
                ContentGrid.Children.Add(idLabel);

                // Column 1: Name TextBox
                var nameBox = new TextBox
                {
                    Text = group.Name,
                    Margin = new Thickness(0, 5, 10, 5),
                    Padding = new Thickness(2)
                };
                _nameBoxes[group.GroupId] = nameBox;
                Grid.SetRow(nameBox, rowIdx);
                Grid.SetColumn(nameBox, 1);
                ContentGrid.Children.Add(nameBox);

                // Column 2: Default Group ComboBox
                var defaultGroupBox = new ComboBox
                {
                    Margin = new Thickness(0, 5, 0, 5),
                    Padding = new Thickness(2),
                    ItemsSource = _availableGr
                };

                // Mặc định chọn Gr lớn nhất nếu chưa có
                if (string.IsNullOrEmpty(group.ProductionGroup) && products != null)
                {
                    var groupProducts = products.Where(p => p.GroupId == group.GroupId).ToList();
                    var allGroupKeys = groupProducts.SelectMany(p => p.QuantitiesByGroup.Keys)
                                                    .Where(k => !string.IsNullOrEmpty(k))
                                                    .Distinct()
                                                    .ToList();
                    if (allGroupKeys.Any())
                    {
                        var maxGr = allGroupKeys.OrderByDescending(k => k).First();
                        defaultGroupBox.SelectedItem = maxGr;
                    }
                }
                else
                {
                    defaultGroupBox.SelectedItem = group.ProductionGroup;
                }

                _groupBoxes[group.GroupId] = defaultGroupBox;
                Grid.SetRow(defaultGroupBox, rowIdx);
                Grid.SetColumn(defaultGroupBox, 2);
                ContentGrid.Children.Add(defaultGroupBox);

                rowIdx++;
            }
        }

        private void SetAllGroups_Click(object sender, RoutedEventArgs e)
        {
            var selectedGr = GlobalGroupSelector.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedGr)) return;

            var confirmed = MessageBox.Show(
                $"Bạn có chắc chắn muốn thiết lập mặc định '{selectedGr}' cho TẤT CẢ các dòng không?",
                "Xác nhận thay đổi hàng loạt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmed == MessageBoxResult.Yes)
            {
                foreach (var cb in _groupBoxes.Values)
                {
                    cb.SelectedItem = selectedGr;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _groups)
            {
                if (_nameBoxes.TryGetValue(group.GroupId, out var nb)) group.Name = nb.Text;
                if (_groupBoxes.TryGetValue(group.GroupId, out var cb))
                {
                    group.ProductionGroup = cb.SelectedItem as string ?? "";
                }
            }
            JsonStorage.Save("productGroups.json", _groups);
            MessageBox.Show("Đã lưu cấu hình product groups!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
