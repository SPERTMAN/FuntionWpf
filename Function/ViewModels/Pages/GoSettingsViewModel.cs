using ChefKeys;
using CommunityToolkit.Mvvm.Messaging;
using Function.Helpers;
using Function.Models;
using Function.Views.Pages;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;


namespace Function.ViewModels.Pages;

public sealed partial class GoSettingsViewModel(INavigationService navigationService) : ViewModel
{
   

    private bool _isInitialized = false;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    [ObservableProperty]
    private NavigationViewPaneDisplayMode _currentApplicationNavigationStyle =
        NavigationViewPaneDisplayMode.Left;


    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    partial void OnCurrentApplicationThemeChanged(ApplicationTheme oldValue, ApplicationTheme newValue)
    {
        ApplicationThemeManager.Apply(newValue);
    }

   

    private void InitializeViewModel()
    {
        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();
        AppVersion = $"{GetAssemblyVersion()}";

        ApplicationThemeManager.Changed += OnThemeChanged;

        _isInitialized = true;
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        // Update the theme if it has been changed elsewhere than in the settings.
        if (CurrentApplicationTheme != currentApplicationTheme)
        {
            CurrentApplicationTheme = currentApplicationTheme;
        }
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }

    [RelayCommand]
    private void OnCutBtn()
    {
        //开始录制
        //ChefKeysManager.StartMenuEnableBlocking = true;// 屏蔽开始菜单，防止录制 Win 组合键时开始菜单弹出来
        //ChefKeysManager.Start();
        // 假设用户设置了新的热键: Ctrl + Alt + A
        ModifierKeys newModifiers = ModifierKeys.Alt;
        Key newKey = Key.S;

        // TODO: 把 newModifiers 和 newKey 存入你的配置文件 config.json 中

        // 告诉管理器：把 'ShowHideApp' 这个功能的实体快捷键更新为最新的！
        GlobalHotkeyManager.BindOrUpdateHotkey("ShowHideApp", newModifiers, newKey);

       


    }

    

}
