using Function.Helpers;
using Function.Models;
using Function.Services;
using Function.Views.Pages;
using Microsoft.Extensions.Configuration;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Function.ViewModels.Pages
{
    public partial class SettingsViewModel(ISnackbarService snackbarService,
        INetworkDataService dataService, IConfiguration config) : ObservableObject, INavigationAware
    {
        // 导入 Windows 系统库 iphlpapi.dll 中的 SendARP 函数。
        // 这是 P/Invoke 的核心，用于在底层发送 ARP 请求。
       
        // SendARP 函数的返回码，表示成功。
        private const int NO_ERROR = 0;
        private NetworkInterface _networkInterface;

        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;
        [ObservableProperty]
        private Visibility _proRingVis = Visibility.Hidden;
        [ObservableProperty]
        private ObservableCollection<IpInfoConfig> _ipInfoConfigs;//= GenerateProducts();
        [ObservableProperty]
        private ObservableCollection<RomoteInfo> _romoteInfos;//= GenerateProducts();
        [ObservableProperty]
        private ObservableCollection<RomoteFile> _romoteFiles;//= GenerateProducts();
        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        private string _adapterName = config["NetWorkName"];
        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"UiDesktopApp1 - {GetAssemblyVersion()}";
            IpInfoConfigs = new ObservableCollection<IpInfoConfig>(dataService.GetAll<IpInfoConfig>());

            RomoteInfos = new ObservableCollection<RomoteInfo>(dataService.GetAll<RomoteInfo>());

            RomoteFiles = new ObservableCollection<RomoteFile>(dataService.GetAll<RomoteFile>());
            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }

        [RelayCommand]
        private void OnOpenCmd()
        {
            // 纯粹打开一个黑窗口，默认路径是你的程序运行目录
            Process.Start("cmd.exe");

        
        }
        [RelayCommand]
        private void OnOpenMstsc()
        {
            Process.Start("mstsc.exe");
        }
        private static ObservableCollection<IpInfoConfig> GenerateProducts()
        {
            return new ObservableCollection<IpInfoConfig>
            {
                new IpInfoConfig { remark="test",Ip = "192.168.1.100",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                },
                new IpInfoConfig { remark="example", Ip = "192.168.1.101",
                    SubNet = "255.255.255.0",
                    GetWay = "0.0.0.0"
                }
            };
        }

        private void SetTaskPromat(string Headtxt, string txt, ControlAppearance appearance)
        {
            ProRingVis = Visibility.Hidden;
            SymbolRegular SymbolRegular= appearance==ControlAppearance.Success? SymbolRegular.Accessibility16:
                appearance==ControlAppearance.Danger? SymbolRegular.ErrorCircle12:
                SymbolRegular.Warning24;
            Application.Current.Dispatcher.Invoke(() =>
            {
                snackbarService.Show(
                               $"{Headtxt}",
                               $"{txt}",
                               appearance,
                               new SymbolIcon(SymbolRegular),
                               TimeSpan.FromSeconds(3)
                                );
            });
        }
        /// <summary>
        /// 将网卡设置为自动获取状态（自动获取）
        /// </summary>
        [RelayCommand]
        public async Task OnSetDhcpIncrement()
        {
            ProRingVis= Visibility.Visible;
            await Task.Run(async () =>
            {

                try
                {
                     
                    ManagementObject adapter = GetNetworkAdapter(_adapterName);
                    if (adapter == null)
                    {
                        SetTaskPromat("警告", $"网卡未连接或者未找到网卡（设置参数）", ControlAppearance.Caution);
                        return;
                    }

                    // 1. 启用 DHCP
                    adapter.InvokeMethod("EnableDHCP", null);

                    // 2. 清除静态 DNS 设置 (可选，通常 DHCP 也会自动设置 DNS)
                    adapter.InvokeMethod("SetDNSServerSearchOrder", new object[] {  });


                    // 3. 重新获取IP地址 (需要调用 RenewDHCPLease 方法)
                    // 注意: RenewDHCPLease 只对 DHCP 启用的网卡有效
                    // 自动获取后，通常系统会立即开始获取 IP，但调用 Renew 可以确保立即触发。
                    uint result = (uint)adapter.InvokeMethod("RenewDHCPLease", null);

                    if (result == 0)
                    {
                        SetTaskPromat("IP自动获取设置成功", $"{result}", ControlAppearance.Success);

                    }
                    else
                    {
                        SetTaskPromat("更改失败", $"错误代码：{result}", ControlAppearance.Caution);

                    }

                }
                catch (Exception ex)
                {
                    SetTaskPromat("发生异常", $"{ex.Message}", ControlAppearance.Danger);

                }
            });
            ProRingVis = Visibility.Hidden;
        }
        // 获取指定名称的网络适配器 ManagementObject
        private ManagementObject GetNetworkAdapter(string adapterName)
        {
            // 1. 使用 .NET 内置的 NetworkInterface 查找网卡的 WMI Description
            NetworkInterface networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(ni => ni.Name .Equals(adapterName, StringComparison.OrdinalIgnoreCase) &&
                                       ni.OperationalStatus == OperationalStatus.Up); // 只查找活动的

            if (networkInterface == null) return null;
            _networkInterface = networkInterface;

            string adapterDescription = networkInterface.Description;
            var scope = new ManagementScope("\\\\.\\root\\cimv2");
            var query = new SelectQuery($"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE Description='{adapterDescription}' AND IPEnabled = TRUE");

            using (var searcher = new ManagementObjectSearcher(scope, query))
            {
                return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            }
        }
        [RelayCommand]
        /// <summary>
        /// 设置网卡为静态 IP 地址、子网掩码和网关。
        /// </summary>
        public async Task OnSetStaticIpIncrement(object parameter)
        {
          
            if (!(parameter is IpInfo clickedItem))
            {
                return;
            }
            
            ProRingVis = Visibility.Visible;
            await Task.Run(async () =>
            {
                try
                {
                    //检测是否冲突，但是貌似不行
                    //if (IsIpInUseByArp(clickedItem.Ip))
                    //{
                    //    System.Windows.MessageBox.Show($"{clickedItem.Ip}存在冲突！！");
                    //    ProRingVis = Visibility.Hidden;
                    //    return;
                    //}
                    ManagementObject adapter = GetNetworkAdapter(_adapterName);
                    if (adapter == null)
                    {
                        SetTaskPromat("警告", $"网卡未连接或者未找到网卡（设置参数）", ControlAppearance.Caution);
                        return;
                    }

                    // 1. 设置 IP 地址和子网掩码
                    ManagementBaseObject newIP = adapter.GetMethodParameters("EnableStatic");
                    //if (clickedItem.GetWay == "0.0.0.0")
                    //{
                    //    newIP["IPAddress"] = new string[] { clickedItem.Ip };
                    //    newIP["SubnetMask"] = new string[] { "255.255.255.125" };
                    //    adapter.InvokeMethod("EnableStatic", newIP, null);

                    //    await Task.Delay(500);
                    //}
                        newIP["IPAddress"] = new string[] { clickedItem.Ip };
                        newIP["SubnetMask"] = new string[] { clickedItem.SubNet };

                    uint result = (uint)adapter.InvokeMethod("EnableStatic", newIP, null)["ReturnValue"];
                    if (result != 0)
                    {
                        SetTaskPromat("错误: 设置静态 IP 失败", $"错误码{result}", ControlAppearance.Danger);
                       
                        return;
                    }
                  
                    // 2. 设置默认网关

                    ManagementBaseObject newGateway = adapter.GetMethodParameters("SetGateways");
                    
                    if (clickedItem.GetWay == "0.0.0.0")
                    {
                        // 如果您只想清除网关，保留其他静态 IP：
                        // 正确的 netsh 命令：重新设置 IP，但将 gateway 参数设为 none
                        string arguments = $"interface ip set address name=\"{_adapterName}\" static {clickedItem.Ip} {clickedItem.SubNet} none";

                        ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        };

                        using (Process process = Process.Start(psi))
                        {
                            process.WaitForExit();
                          
                        }
                    }
                    else
                    {
                        newGateway["DefaultIPGateway"] = new string[] { clickedItem.GetWay };
                        newGateway["GatewayCostMetric"] = new int[] { 1 };
                        result = (uint)adapter.InvokeMethod("SetGateways", newGateway, null)["ReturnValue"];
                    }
                       
                    if (result == 0)
                    {
                        ProRingVis = Visibility.Hidden;
                        SetTaskPromat("IP设置成功", $"{result}", ControlAppearance.Success);
                        ProRingVis = Visibility.Hidden;
                        SetTaskPromat("设置成功", $"{result}", ControlAppearance.Success);

                        await Task.Delay(500);
                        GetNetworkAdapter(_adapterName);
                        var ipProps = _networkInterface.GetIPProperties();
                        // 3. 找所有的单播地址
                        foreach (var ipInfo in ipProps.UnicastAddresses)
                        {
                            // 找到我们刚刚设置的那个 IP
                            if (ipInfo.Address.ToString() == clickedItem.Ip)
                            {
                                // 4. 核心：检查状态
                                // Windows 10/11 会把冲突的 IP 标记为 Duplicate
                                if (ipInfo.DuplicateAddressDetectionState == DuplicateAddressDetectionState.Duplicate)
                                {
                                    SetTaskPromat("警告", $"{clickedItem.Ip}存在冲突！！", ControlAppearance.Caution);
                                    return;
                                }

                                // 如果状态是 Preferred (首选) 或 Tentative (探测中)，说明暂时没问题
                            }
                        }
                        return;
                    }
                    else
                    {
                        SetTaskPromat("错误: 设置网关失败 失败", $"错误码{result}", ControlAppearance.Danger);
                        
                        return;
                    }
                }
                catch (ManagementException mex) when (mex.Message.Contains("拒绝访问"))
                {
                    SetTaskPromat("错误: 拒绝访问", $"请确保您的程序以管理员身份运行", ControlAppearance.Danger);
                  
                    return;
                }
                catch (Exception ex)
                {
                    SetTaskPromat("错误: 拒绝访问", $"错误码{ex.Message}", ControlAppearance.Danger);
                    //Console.WriteLine($"设置静态 IP 失败: {ex.Message}");
                    return;
                }
            });
        }


        /// <summary>
        /// 启用或禁用网卡。
        /// </summary>
        public bool SetAdapterState(string userFriendlyName, bool enable)
        {
            string action = enable ? "启用" : "禁用";
            Console.WriteLine($"\n--- 尝试 {action} 网卡 '{userFriendlyName}' ---");

            try
            {
                ManagementObject adapter = GetNetworkAdapter(userFriendlyName);
                if (adapter == null) return false;

                string methodName = enable ? "Enable" : "Disable";
                uint result = (uint)adapter.InvokeMethod(methodName, null);

                if (result == 0)
                {
                    Console.WriteLine($"成功: 网卡 '{userFriendlyName}' 已成功被 {action}。");
                    return true;
                }
                else
                {
                    Console.WriteLine($"错误: {action} 网卡失败，错误码: {result}");
                    return false;
                }
            }
            catch (ManagementException mex) when (mex.Message.Contains("拒绝访问"))
            {
                Console.WriteLine("致命错误: 拒绝访问。请确保您的程序以管理员身份运行！");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{action} 网卡失败: {ex.Message}");
                return false;
            }
        }

        [RelayCommand]
        /// <summary>
        /// 删除
        /// </summary>
        public async Task OnDeleteIpIncrement(object parameter)
        {
            if (parameter == null) return;
            if(parameter is RomoteInfo Item)
            {
                ProRingVis = Visibility.Visible;
                dataService.Delete(Item, Item.Id);
                RomoteInfos = new ObservableCollection<RomoteInfo>(dataService.GetAll<RomoteInfo>());
                SetTaskPromat("删除成功", $"已成功删除：{Item.remark}", ControlAppearance.Success);
            }
            if (parameter is IpInfoConfig clickedItem)
            {
                ProRingVis = Visibility.Visible;
                dataService.Delete(clickedItem, clickedItem.Id);
                IpInfoConfigs = new ObservableCollection<IpInfoConfig>(dataService.GetAll<IpInfoConfig>());
                SetTaskPromat("删除成功", $"已成功删除：{clickedItem.remark}", ControlAppearance.Success);
            }
            if (parameter is RomoteFile Rfile)
            {
                ProRingVis = Visibility.Visible;
                dataService.Delete(Rfile, Rfile.Id);
                RomoteFiles = new ObservableCollection<RomoteFile>(dataService.GetAll<RomoteFile>());
                SetTaskPromat("删除成功", $"已成功删除：{Rfile.remark}", ControlAppearance.Success);
            }

            ProRingVis = Visibility.Hidden;
           

        }
        [RelayCommand]
        private void OnOpenEditorIncrement(object parameter)
        {
           
            // 这里不能用依赖注入，必须手动 new，因为 item 是运行时才有的数据
            var editor = new EditDataDia(1);

            // 设置父窗口（让弹窗在主窗口中间打开）
            editor.Owner = Application.Current.MainWindow;

            // 打开模态窗口
            if (editor.ShowDialog() == true)
            {
                // 获取结果并处理
                var data = editor.ResultDataIp;
                // ... 保存逻辑
                dataService.Add(data);
                IpInfoConfigs = new ObservableCollection<IpInfoConfig>(dataService.GetAll<IpInfoConfig>());

                SetTaskPromat("新增成功", $"已成功加入：{data.remark}", ControlAppearance.Success);
            }
        }

        [RelayCommand]
        private void OnOpenEditorEditIncrement(object parameter)
        {
            if (parameter == null) return;
            var mode = 2;
            var text = "";
            if (parameter is RomoteInfo Item)
            {
                mode = 4;
            }
            if (parameter is IpInfoConfig clickedItem)
            {
                mode = 2;
            }
            if (parameter is RomoteFile Rfile)
            {
                mode = 6;
            }
            // 这里不能用依赖注入，必须手动 new，因为 item 是运行时才有的数据
            var editor = new EditDataDia(mode, parameter);
            // 设置父窗口（让弹窗在主窗口中间打开）
            editor.Owner = Application.Current.MainWindow;

            // 打开模态窗口
            if (editor.ShowDialog() == true)
            {
                
                // 获取结果并处理
                if (mode == 2)
                {
                   var data= editor.ResultDataIp;
                    // ... 保存逻辑
                    dataService.Update(data);
                    text = data.remark;
                    IpInfoConfigs = new ObservableCollection<IpInfoConfig>(dataService.GetAll<IpInfoConfig>());
                }
                else if (mode == 4)
                {
                    var data = editor.ResultDataRem;
                    // ... 保存逻辑
                    dataService.Update(data);
                    text = data.remark;
                    RomoteInfos = new ObservableCollection<RomoteInfo>(dataService.GetAll<RomoteInfo>());
                }
                else if (mode == 6)
                {
                    var data = editor.ResultDataFile;
                    // ... 保存逻辑
                    dataService.Update(data);
                    text = data.remark;
                    RomoteFiles = new ObservableCollection<RomoteFile>(dataService.GetAll<RomoteFile>());
                }



                SetTaskPromat("修改成功", $"已成功修改：{text}", ControlAppearance.Success);
            }
        }
        [RelayCommand]
        private void OnAddRometeIncrement(object parameter)
        {

            // 这里不能用依赖注入，必须手动 new，因为 item 是运行时才有的数据
            var editor = new EditDataDia(3);

            // 设置父窗口（让弹窗在主窗口中间打开）
            editor.Owner = Application.Current.MainWindow;

            // 打开模态窗口
            if (editor.ShowDialog() == true)
            {
                // 获取结果并处理
                var data = editor.ResultDataRem;
                // ... 保存逻辑
                dataService.Add<RomoteInfo>(data);
                RomoteInfos = new ObservableCollection<RomoteInfo>(dataService.GetAll<RomoteInfo>());

                SetTaskPromat("新增成功", $"已成功加入：{data.remark}", ControlAppearance.Success);
            }
        }
       
        [RelayCommand]
        private void OnAddRometeFileIncrement(object parameter)
        {

            // 这里不能用依赖注入，必须手动 new，因为 item 是运行时才有的数据
            var editor = new EditDataDia(5);

            // 设置父窗口（让弹窗在主窗口中间打开）
            editor.Owner = Application.Current.MainWindow;

            // 打开模态窗口
            if (editor.ShowDialog() == true)
            {
                // 获取结果并处理
                var data = editor.ResultDataFile;
                // ... 保存逻辑
                dataService.Add<RomoteFile>(data);
                RomoteFiles = new ObservableCollection<RomoteFile>(dataService.GetAll<RomoteFile>());

                SetTaskPromat("新增成功", $"已成功加入：{data.remark}", ControlAppearance.Success);
            }
        }
       
        /// <summary>
        /// 远程桌面连接
        /// </summary>
        /// <param name="parameter"></param>
        [RelayCommand]
        private void OnRemoteConIncrement(object parameter)
        {
            if (parameter == null) return;

            NetworkHelper.ConnectToRdp(((RomoteInfo)parameter).Ip,
                ((RomoteInfo)parameter).UserName,
                ((RomoteInfo)parameter).PassWord);

            
        }

        [RelayCommand]
        private async Task OnRemoteConFileIncrement(object parameter)
        {
            if (parameter == null) return;

            await Task.Run(async () =>
            {
                ProRingVis = Visibility.Visible;
                string path = $@"\\{((RomoteFile)parameter).Ip}\";
                if (NetworkHelper.ConnectFile(path,
                    ((RomoteFile)parameter).UserName,
                    ((RomoteFile)parameter).PassWord) != 0)
                {
                    // 2. 连接成功后，直接用 Explorer 打开这个路径
                    // Process.Start("explorer.exe", ((RomoteFile)parameter).Ip);

                    SetTaskPromat("连接失败", "网络连接失败或者账号密码错误", ControlAppearance.Danger);
                }
                else
                {
                    // 2. 连接成功后，直接用 Explorer 打开这个路径
                    Process.Start("explorer.exe", path);
                }
                ProRingVis = Visibility.Hidden;
            });
            


        }

        [RelayCommand]
        private void OnNetCon()
        {
            try
            {
                // 使用 ncpa.cpl 命令直接打开网络连接
                Process.Start("ncpa.cpl");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // 某些系统环境下可能需要显式调用 explorer.exe
                Process.Start("explorer.exe", "shell:::{7007ACC7-3202-11D1-AAD2-00805FC1270E}");
            }
        }


    }
}
