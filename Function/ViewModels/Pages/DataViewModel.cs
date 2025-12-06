using Function.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Function.ViewModels.Pages
{
    public partial class DataViewModel(ISnackbarService snackbarService) : ObservableObject, INavigationAware
    {


        private bool _isInitialized = false;

        private Task? _UpdateIpTask;
        [ObservableProperty]
        private ObservableCollection<DataColor> _colors;

        private List<DataColor> _Gray = new List<DataColor>();
        [ObservableProperty]
        private Visibility _proRingVis = Visibility.Hidden;
        [ObservableProperty]
        private Statu _ipInfoVar;

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
            Colors = new ObservableCollection<DataColor>(colorCollection); ;
        }
        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {


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

            Colors = new ObservableCollection<DataColor>(colorCollection); ;
            _Gray =colorCollection ;
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
                while (true)
                {
                    IpInfoVar = GetRealtekInfo();
                    // 3. 模拟间隔时间
                    await Task.Delay(1000); // 每秒更新一次
                }

            });
        }

        [RelayCommand]
        private async Task OnPingIncrement()
        {
            //先将所有颜色重置为灰色
            Colors = new ObservableCollection<DataColor>(_Gray);
            Statu IpInfo = GetRealtekInfo();
            if (IpInfo.Status != Brushes.Green)
            {
                snackbarService.Show(
               "网卡未连接",
               "请检查是否插上网线",
               ControlAppearance.Caution,
               new SymbolIcon(SymbolRegular.Warning24),
               TimeSpan.FromSeconds(2)
                );

                return;

            }

            ProRingVis = Visibility.Visible;

            //ping当前Ipinfo中所有网段的ip地址，为了不影响主页面使用异步来做

            await Task.Run(async () =>
            {
                
               // List<DataColor> DataColors = Colors.ToList();
                #region old
                //for (int i = 0; i <= 255; i++)
                //{
                //    string targetIp = $"{IpInfo.Ip.Substring(0, IpInfo.Ip.LastIndexOf('.') + 1)}{i}";

                //    using (Ping ping = new Ping())
                //    {
                //        try
                //        {
                //            var reply = await ping.SendPingAsync(targetIp, 5);

                //            if (reply.Status == IPStatus.Success)
                //            {
                //                //找到对应的颜色并修改为绿色

                //                DataColors[i] = new DataColor() {Num=i, Color = Brushes.Green };
                //            }
                //            else
                //            {
                //                DataColors[i] = new DataColor() { Num = i, Color = Brushes.Red };
                //            }

                //            //await Task.Delay(5);
                //        }
                //        catch (Exception)
                //        {
                //            // 忽略异常（如 Ping 失败）
                //        }
                //    }
                //}
                //Colors = DataColors;
                #endregion



                // 2. 创建所有 Ping 任务的列表
                var pingTasks = new List<Task>();

                // 确保 Colors 集合已经被初始化，并且包含 254 个 DataColor 对象
                for (int i = 0; i <= Colors.Count - 1; i++)
                {


                    // ****** 关键修复 A: 避免索引捕获 ******
                    int index = i;

                    // ****** 关键修复 B: 直接在主线程构造 IP，减少线程池工作 ******
                    // 假设您的 IP 地址构造方法是正确的
                    string targetIp = $"{IpInfo.Ip.Substring(0, IpInfo.Ip.LastIndexOf('.') + 1)}{index}";

                    // 为每个 IP 创建一个异步任务
                    var task = Task.Run(async () =>
                    {
                        
                            using (Ping ping = new Ping())
                        {
                            // 注意：这里是 SendPingAsync，它不会阻塞线程池，效率高。
                            PingReply reply = await ping.SendPingAsync(targetIp, 50);

                            // 4. **核心修复 C: 线程安全更新绑定的集合**
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (IpInfo.Ip == targetIp)
                                {
                                    Colors[index] = new DataColor() { Num = index, Color = Brushes.Blue }; return;
                                }
                                Brush newColor = (reply.Status == IPStatus.Success) ? Brushes.Green : Brushes.Red;

                                // 直接使用 ObservableCollection 的索引来替换元素
                                // 这样 UI 就能实时更新该位置的颜色
                                Colors[index] = new DataColor() { Num = index, Color = newColor };
                            });
                        }
                    });
                    pingTasks.Add(task);
                }


                // 4. 等待所有任务完成
                await Task.WhenAll(pingTasks);
               // Colors = DataColors;
                // 扫描完成后的逻辑...


                ProRingVis = Visibility.Hidden;
            });

        }

        private Statu GetRealtekInfo()
        {
            //获取Realtek网卡信息
            Statu ipInfo = new Statu();
            //这里可以添加获取IP地址、子网掩码和网关的逻辑

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // 只要以太网和WiFi
                if (ni.Name != "以太网")
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

                ipInfo.GetWay = gateway == "" ? "0.0.0.0" : gateway;
                ipInfo.Status = status;
            }
            return ipInfo;

        }
    }
}
