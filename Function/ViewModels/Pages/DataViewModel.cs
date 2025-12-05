using Function.Models;
using System;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Function.ViewModels.Pages
{
    public partial class DataViewModel : ObservableObject, INavigationAware
    {
        

        private bool _isInitialized = false;
        
        private Task _UpdateIpTask;
        [ObservableProperty]
        private IEnumerable<DataColor> _colors;
        [ObservableProperty]
        private Visibility _proRingVis=Visibility.Hidden ;
        [ObservableProperty]
        private IpInfo _ipInfoVar;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

       
       
        [RelayCommand]
        private void OnCounterIncrement()
        {
            var random = new Random();
            var colorCollection = new List<DataColor>();
            for (int i = 0; i < 255; i++)
                colorCollection.Add(
                    new DataColor
                    {
                        Color = new SolidColorBrush(
                            Color.FromArgb(
                                (byte)200,
                                (byte)random.Next(0, 250),
                                (byte)random.Next(0, 250),
                                (byte)random.Next(0, 250)
                            )
                        )
                    }
                );
            Colors = colorCollection;
        }
        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {

            var random = new Random();
            var colorCollection = new List<DataColor>();
            // 这里以 MediumGray 为例 (RGB: 128, 128, 128)
            //var fixedGrayBrush = new SolidColorBrush(Color.FromArgb(200, 128, 128, 128));
            for (int i = 0; i < 255; i++)
                colorCollection.Add(
                    new DataColor
                    {
                        Color = Brushes.Gray,
                        Num = i
                    }
                );

            Colors = colorCollection;

            _isInitialized = true;
        }

        [RelayCommand]
        private async Task OnLoadIpIncrement()
        {
            //IpInfoVar = GetRealtekInfo();
            // 使用 Task.Run 将耗时/持续操作放到后台线程池
            if (_UpdateIpTask != null && !_UpdateIpTask.IsCompleted) return;
            
            _UpdateIpTask = Task.Run(async () =>
            {
                while(true)
                {
                    IpInfoVar = GetRealtekInfo();
                    // 3. 模拟间隔时间
                    await Task.Delay(1000); // 每秒更新一次
                }
               
            } );
        }

        [RelayCommand]
        private async Task OnPingIncrement()
        {
        ProRingVis=Visibility.Visible;

            // 3. 触发事件，将创建好的ProgressRing传递出去
            // 必须将其添加到您的顶层容器，例如 RootGrid

            //IpInfo IpInfo = GetRealtekInfo();
            //if (IpInfo.Status != Brushes.Green)
            //{


            //}

        }

        private IpInfo GetRealtekInfo()
        {
            //获取Realtek网卡信息
            IpInfo ipInfo = new IpInfo();
            //这里可以添加获取IP地址、子网掩码和网关的逻辑

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // 只要以太网和WiFi
                if (ni.Description != "Realtek PCIe GbE Family Controller" )
                    continue;

                var ipProps = ni.GetIPProperties();
                var ip = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";
                var mask = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.IPv4Mask?.ToString() ?? "";
                var gateway = ipProps.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";
                Brush status = ni.OperationalStatus.ToString() == "Up" ? Brushes.Green :
                               ni.OperationalStatus.ToString() == "Down" ? Brushes.Red :
                               ip == "" ? Brushes.Gray : Brushes.Yellow;

                ipInfo.Ip = ip;
                ipInfo.SubNet = mask;

                ipInfo.GetWay = gateway==""?"0.0.0.0": gateway;
                ipInfo.Status = status;
            }
            return ipInfo;

        }
    }
}
