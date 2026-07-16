using BeckhoffSearch;
using Function.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Button = Wpf.Ui.Controls.Button;
using ContentDialog = Wpf.Ui.Controls.ContentDialog;


namespace Function.ViewModels.Pages
{
    public enum AdsConnectChoice
    {
        Cancel,

        CE,
        OPCON,
        NEXEED,
        Other
    }
    public struct UserPwd
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
    public partial class DataViewModel(ISnackbarService snackbarService, IConfiguration config, IContentDialogService contentDialogService) : ObservableObject, INavigationAware
    {


        private bool _isInitialized = false;

        private Task? _UpdateIpTask;
        [ObservableProperty]
        private ObservableCollection<DataColor> _colors;

        private List<DataColor> _Gray = new List<DataColor>();
        [ObservableProperty]
        private Visibility _proRingVis = Visibility.Hidden;
        [ObservableProperty]
        private Visibility _proAdsVis = Visibility.Hidden;
        [ObservableProperty]
        private bool _isTemporaryConnection = true;
        [ObservableProperty]
        private Statu _ipInfoVar;
        //[ObservableProperty]
        //private List<BeckhoffModel> _beckhoffDevice=new List<BeckhoffModel>();

        [ObservableProperty]
        private ObservableCollection<BeckhoffModel> _beckhoffInfo=new ObservableCollection<BeckhoffModel>();

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
            _Gray = colorCollection;
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


                try
                {
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
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                        // 4. 等待所有任务完成
                        
                    }

                    await Task.WhenAll(pingTasks);
                    // Colors = DataColors;
                    // 扫描完成后的逻辑...


                    ProRingVis = Visibility.Hidden;
                }
                catch (Exception ex)
                {
                    
                }



               
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
                if (ni.Name != config["NetWorkName"])
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
        private AdsConnectChoice ShowAdsChoiceDialog()
        {
            var window = new Window
            {
                Title = "请选择目标系统",
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            AdsConnectChoice choice = AdsConnectChoice.Cancel;

            var root = new StackPanel
            {
                Margin = new Thickness(20)
            };

            root.Children.Add(new Wpf.Ui.Controls.TextBlock
            {
                Text = "请选择系统",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var grid = new UniformGrid
            {
                Rows = 2,
                Columns = 2
            };

            Button CreateButton(string text, AdsConnectChoice result)
            {
                var btn = new Button
                {
                    Content = text,
                    Width = 150,
                    Height = 80,
                    Margin = new Thickness(8),
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold
                };

                btn.Click += (_, _) =>
                {
                    choice = result;
                    window.DialogResult = true;
                    window.Close();
                };

                return btn;
            }

            grid.Children.Add(CreateButton("CE", AdsConnectChoice.CE));
            grid.Children.Add(CreateButton("OPCON", AdsConnectChoice.OPCON));
            grid.Children.Add(CreateButton("NEXEED", AdsConnectChoice.NEXEED));
            grid.Children.Add(CreateButton("...", AdsConnectChoice.Other));

            root.Children.Add(grid);

            window.Content = root;

            window.ShowDialog();

            return choice;
        }
        [RelayCommand]
        private async Task ConnectAds(object parameter)
        {
            try
            {
                if (parameter == null) return;

                var choice = ShowAdsChoiceDialog();


                UserPwd UserName = choice switch
                {
                    AdsConnectChoice.CE => new UserPwd { UserName = "Administrator", Password = "1" },
                    AdsConnectChoice.OPCON => new UserPwd { UserName = "OpconAdmin", Password = "OpconnAdminn" },
                    AdsConnectChoice.NEXEED => new UserPwd { UserName = "NexeedAdmin", Password = "NexeeddAdminn" },
                    AdsConnectChoice.Other => new UserPwd { UserName = "Administrator", Password = "1" },
                    _ => new UserPwd { UserName = "Administrator", Password = "1" }
                };


            ProAdsVis = Visibility.Visible;
                BeckhoffModel device= parameter as BeckhoffModel;
                var connector = new BeckhoffRouteConnector();
                var result=new BeckhoffRouteConnectResult();
                await Task.Run(() =>
                {
                     result = connector.Connect(new BeckhoffRouteConnectRequest
                    {
                        Address = device.Ip,
                        UserName = UserName.UserName,
                        Password = UserName.Password,

                        // Static：不带 -Temporary
                        // Temporary：带 -Temporary

                        RouteType = IsTemporaryConnection ? BeckhoffRouteType.Temporary : BeckhoffRouteType.Static,

                        ModulePath = @"C:\TwinCAT\AdsApi\Powershell\TcXaeMgmt\TcXaeMgmt.psd1"
                    });
                });

                if (!result.CommandSuccess)
                {
                    snackbarService.Show(
                      "连接错误",
                      $"请检查错误信息：{result.CommandError}",
                      ControlAppearance.Caution,
                      new SymbolIcon(SymbolRegular.Warning24),
                      TimeSpan.FromSeconds(3)
                        );
                    


                }
                else
                {
                    snackbarService.Show(
                     "连接成功",
                     $"",
                     ControlAppearance.Success,
                     new SymbolIcon(SymbolRegular.Accessibility16),
                     TimeSpan.FromSeconds(3)
                       );
                    device.Connected="x";
                    var item = BeckhoffInfo.FirstOrDefault(x => x.Ip == device.Ip);

                    if (item != null)
                    {
                       
                        BeckhoffInfo.Remove(item);

                       
                        item.Connected = "x";

                        
                        BeckhoffInfo.Add(item);

                    }
                   

                }
                ProAdsVis = Visibility.Hidden;

            }
            catch (Exception ex)
            {
                ProAdsVis = Visibility.Hidden;
                throw;
            }
            

        }

        [RelayCommand]
        private async Task SearchDevice()
        {
            try
            {
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
                ProAdsVis = Visibility.Visible;
                if (BeckhoffInfo.Count > 0) BeckhoffInfo.Clear();
                string LocalIp = IpInfoVar.Ip;

                using var searcher = new BeckhoffBroadcastSearcher(
                LocalIp,
                "192.168.0.1.1.1",
                localPort: 50000);

                await Task.Run(() => { 
                string newIp = LocalIp.Substring(0, LocalIp.LastIndexOf('.') + 1) + "255";
                // 广播搜索，返回多个设备
                var devices2 = searcher.SearchBroadcast(newIp, timeoutMs: 3000);
                //List<BeckhoffModel> beckhoff = new List<BeckhoffModel>();

                foreach (var device in devices2)
                {
                   
                    if (device.Ip == LocalIp) continue;
                    if(BeckhoffRouteConnector.CheckRoute(device))
                        device.Connected="x";

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            BeckhoffInfo.Add(device);
                        });
                    }
                });
                ProAdsVis = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                snackbarService.Show(
              "搜索错误",
              $"请检查错误信息：{ex.Message}",
              ControlAppearance.Caution,
              new SymbolIcon(SymbolRegular.Warning24),
              TimeSpan.FromSeconds(3)
               );
                ProAdsVis = Visibility.Hidden;
            }
          
        }
    }
}
