using CommunityToolkit.Mvvm.Messaging;
using Function.Helpers;
using Function.Models;
using Microsoft.Toolkit.Uwp.Notifications;
using ScreenGrab;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Windows.Media.Imaging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Function.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private const int DefaultCaptureDelayMs = 100; // 捕获前的默认延迟时间，单位为毫秒

        [ObservableProperty]
        private int _counter = 0;
        [ObservableProperty]
        private ObservableCollection<GenApp> _basicListViewItems = GeneratePersons();
        private readonly ISnackbarService _snackbarService;


        public DashboardViewModel(ISnackbarService snackbarService)
        {
            _snackbarService= snackbarService;
            GlobalHotkeyManager.RegisterAction("ShowHideApp", () => _ = OcrAsync());
        }
        private static ObservableCollection<GenApp> GeneratePersons()
        {
            var persons = new ObservableCollection<GenApp>();
            for (int i = 1; i <= 1; i++)
            {
                persons.Add(new GenApp
                {
                    AppName = $"应用程序 {i}",
                    AppVersion = $"版本 {i}.0",
                    AppDescription = $"这是应用程序 {i} 的描述。"
                });
            }
            return persons;
        }

        [RelayCommand]
        private void OnCounterIncrement()
        {
            Counter++;
        }

        [RelayCommand]
        private void OnShowIncrement()
        {
            _snackbarService.Show(
                "操作成功",
                "数据已成功保存到数据库",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(3)
            );
        }

        [RelayCommand]
        /// <summary>
        /// 删除
        /// </summary>
        public  void OnRestartPrintServiceIncrement()
        {
            // 替换为你想要重启的服务名称，例如 "Spooler" (打印后台处理程序)
            string serviceName = "Spooler";

            // 设置超时时间，防止服务卡死导致程序一直挂起
            TimeSpan timeout = TimeSpan.FromSeconds(30);

            bool success = RestartService(serviceName, timeout);

            if (success)
            {
                SetTaskPromat($"[{serviceName}] 服务重启成功", $"", ControlAppearance.Success);
            }
            else
            {
                SetTaskPromat($"[{serviceName}] 服务重启失败", $"", ControlAppearance.Danger);
            }
        }

        /// <summary>
        /// 重启指定的Windows服务并监控其状态
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="timeout">等待超时时间</param>
        /// <returns>是否重启成功</returns>
        public static bool RestartService(string serviceName, TimeSpan timeout)
        {
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    // 1. 如果服务正在运行或处于暂停状态，则先停止它
                    if (service.Status != ServiceControllerStatus.Stopped &&
                        service.Status != ServiceControllerStatus.StopPending)
                    {
                       
                        service.Stop();

                        // 监控：等待服务完全停止，直到超时
                        service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                       
                    }

                    // 2. 启动服务
                   
                    service.Start();

                    // 监控：等待服务完全启动，直到超时
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                   

                    return true;
                }
            }
            catch (System.TimeoutException)
            {
                Console.WriteLine($"错误：操作超时。服务 {serviceName} 未能在规定时间内改变状态。");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"错误：无法操作服务。请确保服务存在且程序以管理员权限运行。详细信息: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生未知错误: {ex.Message}");
                return false;
            }
        }

        private void SetTaskPromat(string Headtxt, string txt, ControlAppearance appearance)
        {
            
            SymbolRegular SymbolRegular = appearance == ControlAppearance.Success ? SymbolRegular.Accessibility16 :
                appearance == ControlAppearance.Danger ? SymbolRegular.ErrorCircle12 :
                SymbolRegular.Warning24;
            Application.Current.Dispatcher.Invoke(() =>
            {
                _snackbarService.Show(
                               $"{Headtxt}",
                               $"{txt}",
                               appearance,
                               new SymbolIcon(SymbolRegular),
                               TimeSpan.FromSeconds(3)
                                );
            });
        }

        [RelayCommand] // 这是一个 MVVM 命令，绑定到 UI 上的某个按钮或快捷键
        private async Task<Bitmap?> OcrAsync()
        {
            if (ScreenGrabber.IsCapturing)
                return default;

            if (App.Current.MainWindow.Visibility == Visibility.Visible &&
                !App.Current.MainWindow.Topmost)
                App.Current.MainWindow.WindowState= WindowState.Minimized;

            // Allow UI to update before capturing
            await Task.Delay(DefaultCaptureDelayMs);

            var bitmap = await ScreenGrabber.CaptureAsync(false);
            if (bitmap == null)
                return default;

            // 转换 Bitmap -> BitmapSource
            BitmapSource bmpSource = ConvertBitmapToBitmapSource(bitmap);
            // 将 Bitmap 放入剪贴板
            Clipboard.SetImage(bmpSource);

            // 2. 呼出 Windows 原生系统通知 (会进入你截图里的通知中心)
            new ToastContentBuilder()
                .AddText("已添加到剪贴板")            // 第一行：加粗的标题文本
                .AddText("")                      // 第二行：普通文本（这里显示刚才复制的内容预览）
                                                    // .AddAppLogoOverride(new Uri("ms-appx:///Assets/logo.png"), ToastGenericAppLogoCrop.Circle) // (可选) 如果你想加个好看的圆形图标
                .Show();                            // 触发显示

            return bitmap;
        }

        // 转换方法
        BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }
    }
}

