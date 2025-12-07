using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Function.Helpers
{
    public static class NetworkHelper
    {
        // Windows 网络资源结构体
        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint DestIP, uint SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);

        // 引入 mpr.dll 的 WNetAddConnection2 方法
        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NETRESOURCE lpNetResource, string lpPassword, string lpUsername, int dwFlags);

        // 引入 mpr.dll 的 WNetCancelConnection2 方法 (用于断开)
        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string lpName, int dwFlags, bool fForce);


        public static void ConnectToRdp(string ip, string username, string password)
        {
            // 1. 准备 cmdkey 命令
            // 格式: cmdkey /generic:TERMSRV/目标IP /user:用户名 /pass:密码
            // 注意: TERMSRV/ 是必须的前缀，告诉系统这是远程桌面的凭据
            string cmdKeyArgs = $"/generic:TERMSRV/{ip} /user:{username} /pass:{password}";

            // 2. 执行 cmdkey 添加凭据 (隐藏窗口执行)
            ProcessStartInfo cmdKeyProcess = new ProcessStartInfo("cmdkey", cmdKeyArgs)
            {
                WindowStyle = ProcessWindowStyle.Hidden, // 不显示黑窗口
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(cmdKeyProcess)?.WaitForExit(); // 等待凭据添加完成

            // 3. 启动远程桌面
            // /v:IP 指定目标
            // /admin (可选) 以管理员控制台模式连接
            // /f (可选) 全屏模式
            Process.Start("mstsc.exe", $"/v:{ip}");
        }

        /// <summary>
        /// 检查网络上是否有设备正在使用指定的 IP 地址。
        /// </summary>
        /// <param name="ipAddress">要检查的 IP 地址字符串。</param>
        /// <returns>如果收到 MAC 地址回复，返回 True (存在冲突或 IP 正在被使用)；否则返回 False。</returns>
        public static bool IsIpInUseByArp(string ipAddress)
        {
            // 1. 验证 IP 地址格式
            if (!IPAddress.TryParse(ipAddress, out IPAddress ip))
            {
                throw new ArgumentException("IP 地址格式无效。", nameof(ipAddress));
            }
            // 1. 【优先】尝试 Ping (最快，且支持跨网段)
            // 即使对方有防火墙，如果不在同一网段，Ping 是唯一的检测手段
            if (PingCheck(ip))
            {
                return true; // Ping 通了，肯定被占用了
            }

            // 2. 【保底】尝试 ARP (专治同网段防火墙)
            // 如果 Ping 不通，且我们在同一个网段，ARP 可以穿透防火墙检测
            // 如果 SendARP 返回 67，说明不在同网段，这里会返回 false，这是符合预期的
            if (ArpCheck(ip))
            {
                return true; // ARP 查到了 MAC，肯定被占用了
            }

            // 3. 都没反应，大概率是空闲的
            return false;



        }
        // --- Ping 检测逻辑 ---
        private static bool PingCheck(IPAddress ip)
        {
            try
            {
                using (var p = new Ping())
                {
                    // 超时设置短一点 (200ms)，提高体验
                    var reply = p.Send(ip, 200);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        // --- ARP 检测逻辑 ---
        private static bool ArpCheck(IPAddress ip)
        {
            byte[] addressBytes = ip.GetAddressBytes();
            uint destIp = BitConverter.ToUInt32(addressBytes, 0);

            byte[] macAddr = new byte[6];
            uint macAddrLen = (uint)macAddr.Length;

            // SrcIP 传 0，让系统自动选择网卡
            int result = SendARP(destIp, 0, macAddr, ref macAddrLen);

            // result = 0 表示成功 (NO_ERROR)
            // result = 67 表示 ERROR_BAD_NET_NAME (不在同网段，无法 ARP)
            // result = 1168 表示 ERROR_NOT_FOUND (没人回应 ARP，即 IP 空闲)
            if (result == 0)
            {
                return true;
            }

            // 调试用：你可以在这里输出 result 看看具体错误码
            // System.Diagnostics.Debug.WriteLine($"ARP Failed: {result}");

            return false;
        }

        /// <summary>
        /// 连接到共享文件夹
        /// </summary>
        /// <param name="networkPath">路径 (如 \\192.168.1.10\Share)</param>
        /// <param name="username">账号 (如 Administrator)</param>
        /// <param name="password">密码</param>
        public static int ConnectFile(string networkPath, string username, string password)
        {
            string target = networkPath.TrimStart('\\').Split('\\')[0];

            // 3. 此时 Windows 已经有了“钥匙”，直接用 Explorer 打开即可

            // 1. 构造资源对象
            var netResource = new NETRESOURCE
            {
                dwScope = 2,       // RESOURCE_GLOBALNET
                dwType = 1,        // RESOURCETYPE_DISK
                dwDisplayType = 3, // RESOURCEDISPLAYTYPE_GENERIC
                dwUsage = 1,       // RESOURCEUSAGE_CONNECTABLE
                lpRemoteName = networkPath, // 目标路径
                lpLocalName = null // 不映射为盘符(Z:)，直接连接
            };

            // 2. 先尝试断开一次，防止“不允许一个用户使用一个以上用户名与服务器建立连接”的错误
            try { Disconnect(networkPath); } catch { }

            // 3. 建立连接
            int result = WNetAddConnection2(netResource, password, username, 0);

            // 4. 处理结果 (0 表示成功)
            return result;
        }
        /// <summary>
        /// 断开连接
        /// </summary>
        public static void Disconnect(string networkPath)
        {
            WNetCancelConnection2(networkPath, 0, true);
        }

    }
}
