using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace Function.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "WPF UI - Function";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Main",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "Ip Fun",
                Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
                TargetPageType = typeof(Views.Pages.DataPage)
            }
        };

        //[ObservableProperty]
        //private ObservableCollection<object> _footerMenuItems = new()
        //{
        //    new NavigationViewItem()
        //    {
        //        Content = "Settings",
        //        Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
        //        TargetPageType = typeof(Views.Pages.SettingsPage)
        //    }
        //};

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };
    }
}
