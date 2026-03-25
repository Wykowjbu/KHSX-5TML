using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace KHSX.Views
{
    /// <summary>
    /// Popup cho phép sắp xếp thứ tự BuildGroup trên mỗi ca trước khi export.
    /// Chỉ hiện những ca có >1 BuildGroup.
    /// </summary>
    public partial class ExportOrderDialog : Wpf.Ui.Controls.FluentWindow
    {
        public class BlockItem
        {
            public string Code { get; set; } = string.Empty;
            public string DisplayText { get; set; } = string.Empty;
        }

        public class LineBlockGroup
        {
            public string LineName { get; set; } = string.Empty;
            public ObservableCollection<BlockItem> Blocks { get; set; } = new();
        }

        public ObservableCollection<LineBlockGroup> LineGroups { get; } = new();

        /// <summary>
        /// Kết quả: thứ tự block per line. Key = LineName, Value = danh sách block code theo thứ tự.
        /// </summary>
        public Dictionary<string, List<string>>? ResultBlockOrder { get; private set; }

        public bool IsConfirmed { get; private set; }

        /// <summary>
        /// Khởi tạo dialog.
        /// </summary>
        /// <param name="lineBlockData">Key = LineName, Value = danh sách block code</param>
        public ExportOrderDialog(Dictionary<string, List<string>> lineBlockData)
        {
            InitializeComponent();

            foreach (var kvp in lineBlockData) // Giữ thứ tự Rows gốc, không sort theo tên
            {
                // Chỉ hiện line có >1 block
                if (kvp.Value.Count <= 1) continue;

                var group = new LineBlockGroup { LineName = kvp.Key };
                foreach (var code in kvp.Value)
                {
                    group.Blocks.Add(new BlockItem
                    {
                        Code = code,
                        DisplayText = $"BuildGroup: {code}"
                    });
                }
                LineGroups.Add(group);
            }

            LinesPanel.ItemsSource = LineGroups;
        }

        private void MoveBlockUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is BlockItem item)
            {
                foreach (var group in LineGroups)
                {
                    int index = group.Blocks.IndexOf(item);
                    if (index > 0)
                    {
                        group.Blocks.Move(index, index - 1);
                        break;
                    }
                }
            }
        }

        private void MoveBlockDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is BlockItem item)
            {
                foreach (var group in LineGroups)
                {
                    int index = group.Blocks.IndexOf(item);
                    if (index >= 0 && index < group.Blocks.Count - 1)
                    {
                        group.Blocks.Move(index, index + 1);
                        break;
                    }
                }
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ResultBlockOrder = new Dictionary<string, List<string>>();
            foreach (var group in LineGroups)
            {
                ResultBlockOrder[group.LineName] = group.Blocks.Select(b => b.Code).ToList();
            }
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
