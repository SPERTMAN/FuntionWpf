using Function.Services;
using Function.ViewModels.Pages;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Function.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    { 
        
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
           
            InitializeComponent();
        }
        // ... 类定义中 ...

        private void MyDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 1. 获取鼠标点击的原始元素（可视树中的某个小部件，比如文字块或边框）
            // 注意：如果是键盘触发的菜单，OriginalSource 可能是 null 或 DataGrid 本身，这里主要处理鼠标
            var source = e.OriginalSource as DependencyObject;

            // 2. 向上查找，看看这个元素是不是在 DataGridRow 里面
            var row = FindVisualParent<DataGridRow>(source);

            if (row != null)
            {
                // === 情况 A：点到了有数据的行 (Row) ===

                // 你说“右击到不是空的不是我定义的”，意思是你不想要 DataGrid 的那个菜单。
                // 这里把 Handled 设为 true，表示“事件已处理”，WPF 就不会弹出 DataGrid 的 ContextMenu 了。
                e.Handled = true;

                // 【可选】如果你以后想给行单独设置菜单，可以在这里动态赋值：
                // row.ContextMenu = this.Resources["RowContextMenu"] as ContextMenu;
                // row.ContextMenu.IsOpen = true;
            }
            else
            {
                // === 情况 B：点到了空白处 ===

                // 什么都不用做，e.Handled 默认为 false。
                // DataGrid 会自动弹出我们在 XAML 里定义的那个 ContextMenu。
            }
        }

        // 通用的辅助方法：在可视树中向上查找指定类型的父控件
        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void MyDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 获取我们在 XAML 资源里定义的 "RowContextMenu"
            var rowMenu = this.Resources["RowContextMenu"] as ContextMenu;

            // 直接赋值给当前加载的这一行
            // 这样做完全不会影响行的外观样式（颜色、圆角等），只是挂载了一个右键菜单
            e.Row.ContextMenu = rowMenu;
        }
    }
}
