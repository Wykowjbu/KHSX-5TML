using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KHSX.Models;
using KHSX.Services;

namespace KHSX.Views
{
    public partial class DeadlineConfigDialog : Wpf.Ui.Controls.FluentWindow
    {
        private readonly Dictionary<string, DatePicker> _datePickers = new();

        public DeadlineConfigDialog()
        {
            InitializeComponent();
            BuildGrid();
        }

        private void BuildGrid()
        {
            var groups = JsonStorage.Load<List<ProductGroupData>>("productGroups.json");
            if (groups == null || groups.Count == 0) return;

            var existingDeadlines = JsonStorage.Load<List<DeadlineData>>("deadlines.json") ?? new();
            var products = JsonStorage.Load<List<ProductData>>("products.json");
            var allGroupsFromProducts = products?.SelectMany(p => p.QuantitiesByGroup.Keys) ?? Enumerable.Empty<string>();

            var productionGroups = groups.Select(g => g.ProductionGroup)
                                          .Concat(allGroupsFromProducts)
                                          .Where(g => !string.IsNullOrWhiteSpace(g))
                                          .Distinct()
                                          .OrderBy(g => g)
                                          .ToList();

            int rowIdx = 0;
            foreach (var grp in productionGroups)
            {
                ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var groupLabel = new TextBlock
                {
                    Text = grp,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5),
                    FontWeight = FontWeights.Bold
                };
                Grid.SetRow(groupLabel, rowIdx);
                Grid.SetColumn(groupLabel, 0);
                ContentGrid.Children.Add(groupLabel);

                var datePicker = new DatePicker { Margin = new Thickness(0, 5, 0, 5) };
                var existing = existingDeadlines.Find(d => d.GroupNumber == grp);
                if (existing != null)
                {
                    datePicker.SelectedDate = existing.Deadline;
                }
                _datePickers[grp] = datePicker;
                Grid.SetRow(datePicker, rowIdx);
                Grid.SetColumn(datePicker, 1);
                ContentGrid.Children.Add(datePicker);

                rowIdx++;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var newDeadlines = new List<DeadlineData>();
            foreach (var kvp in _datePickers)
            {
                if (kvp.Value.SelectedDate.HasValue)
                {
                    newDeadlines.Add(new DeadlineData { GroupNumber = kvp.Key, Deadline = kvp.Value.SelectedDate.Value });
                }
            }
            JsonStorage.Save("deadlines.json", newDeadlines);
            MessageBox.Show("Đã lưu thiết lập deadline thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
