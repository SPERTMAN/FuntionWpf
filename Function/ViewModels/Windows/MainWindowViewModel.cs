using AutoUpdaterDotNET;
using Function.Helpers;
using System.Collections.ObjectModel;
using System.Reflection;
using Wpf.Ui.Controls;

namespace Function.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "IP Function " + Assembly.GetExecutingAssembly().GetName().Version?.ToString();

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
                Icon = new SymbolIcon { Symbol = SymbolRegular.DesktopToolbox20 },
                TargetPageType = typeof(Views.Pages.DataPage)
            },
             new NavigationViewItem()
            {
                Content = "IpSettings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.TextboxSettings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            },
             new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings16 },
                TargetPageType = typeof(Views.Pages.GoSettingsPage)
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
        public MainWindowViewModel()
        {
            ModifierKeys newModifiers = ModifierKeys.Alt;
            Key newKey = Key.S;

            // TODO: 把 newModifiers 和 newKey 存入你的配置文件 config.json 中

            // 告诉管理器：把 'ShowHideApp' 这个功能的实体快捷键更新为最新的！
            GlobalHotkeyManager.BindOrUpdateHotkey("ShowHideApp", newModifiers, newKey);
        }
        ////自动更新
        //string updateUrl = configuration["UpdateXmlUrl"];
        //AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
        //    AutoUpdater.Start(updateUrl);
    }  
}
