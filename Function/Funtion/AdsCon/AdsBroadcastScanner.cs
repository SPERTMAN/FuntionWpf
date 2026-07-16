using Function.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BeckhoffSearch
{
    public enum BeckhoffRemoteDesktopType
    {
        Unknown,
        WindowsRdp,
        WindowsCeCerHost
    }

    public sealed class BeckhoffBroadcastSearcher : IDisposable
    {
        private const int BeckhoffDiscoveryPort = 48899;
        // Windows 底层 Socket 控制码：屏蔽 ICMP 端口不可达导致的 ConnectionReset 异常
        private const int SIO_UDP_CONNRESET = -1744830452;

        private readonly IPAddress _localIp;
        private readonly int _localPort;
        private readonly byte[] _localAmsNetId;
        private UdpClient? _udp;

        public BeckhoffBroadcastSearcher(
            string localIp,
            string localAmsNetId,
            int localPort = 50000)
            : this(IPAddress.Parse(localIp), ParseAmsNetId(localAmsNetId), localPort)
        {
        }

        public BeckhoffBroadcastSearcher(
            IPAddress localIp,
            byte[] localAmsNetId,
            int localPort = 50000)
        {
            if (localAmsNetId == null)
                throw new ArgumentNullException(nameof(localAmsNetId));

            if (localAmsNetId.Length != 6)
                throw new ArgumentException("AMS NetId 必须是 6 字节，例如 192.168.105.167.1.1。");

            _localIp = localIp;
            _localAmsNetId = localAmsNetId.ToArray();
            _localPort = localPort;
        }

        /// <summary>
        /// 兼容旧调用的批量广播搜索，新增 onDeviceFound 实时回调支持
        /// </summary>
        public List<BeckhoffModel> SearchBroadcast(
            string broadcastIp,
            int timeoutMs = 1500,
            Action<BeckhoffModel>? onDeviceFound = null)
        {
            return Search(IPAddress.Parse(broadcastIp), timeoutMs, onDeviceFound);
        }

        public List<BeckhoffModel> SearchUnicast(
            string targetIp,
            int timeoutMs = 1000,
            Action<BeckhoffModel>? onDeviceFound = null)
        {
            return Search(IPAddress.Parse(targetIp), timeoutMs, onDeviceFound);
        }

        private List<BeckhoffModel> Search(
            IPAddress targetIp,
            int timeoutMs,
            Action<BeckhoffModel>? onDeviceFound)
        {
            var result = new List<BeckhoffModel>();
            var receivedKeys = new HashSet<string>();

            EnsureUdpCreated();
            byte[] request = BuildSearchPacket();

            // =========================================================
            // 🚀 双重广播穿透策略（完全保留你的原有设计）
            // =========================================================
            byte[] targetBytes = targetIp.GetAddressBytes();
            bool isBroadcast = targetIp.Equals(IPAddress.Broadcast) || targetBytes[3] == 255;

            if (isBroadcast)
            {
                // 策略 1：发送全局限制广播 (255.255.255.255)
                try
                {
                    _udp!.Send(request, request.Length, new IPEndPoint(IPAddress.Broadcast, BeckhoffDiscoveryPort));
                }
                catch { /* 忽略底层网络不支持全网广播的异常 */ }

                // 策略 2：基于绑定的本地网卡 IP，发送定向子网广播 (例如 192.168.0.255)
                try
                {
                    byte[] localIpBytes = _localIp.GetAddressBytes();
                    localIpBytes[3] = 255;
                    var directedBroadcast = new IPAddress(localIpBytes);

                    if (!directedBroadcast.Equals(IPAddress.Broadcast))
                    {
                        _udp!.Send(request, request.Length, new IPEndPoint(directedBroadcast, BeckhoffDiscoveryPort));
                    }
                }
                catch { /* 忽略路由异常 */ }
            }
            else
            {
                // 策略 3：单播搜索
                var targetEndPoint = new IPEndPoint(targetIp, BeckhoffDiscoveryPort);
                _udp!.Send(request, request.Length, targetEndPoint);
            }

            // =========================================================
            // ⚡ 极速流式接收：收到一个解析一个，立刻触发回调上屏！
            // =========================================================
            long deadlineTicks = Environment.TickCount64 + timeoutMs;

            while (Environment.TickCount64 < deadlineTicks)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp!.Receive(ref remote);

                    // 过滤掉本机自己发出的广播回环包，省去后续无意义的解析
                    if (remote.Address.Equals(_localIp))
                        continue;

                    var device = ParseResponse(remote, data);
                    if (device == null)
                        continue;

                    string key = $"{device.Ip}-{device.AmsNetId}";
                    if (receivedKeys.Add(key))
                    {
                        result.Add(device);
                        // 🔥 核心提速点：不等循环结束，立刻把设备抛给外部 UI 和路由检测线程！
                        onDeviceFound?.Invoke(device);
                    }
                }
                catch (SocketException ex)
                {
                    // 连续 200ms 内没有任何新 PLC 回包，说明局域网内设备已全部响应完毕，直接提前退出！
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        break;

                    // 忽略 ICMP 端口不可达重置，继续收其他 PLC 的包
                    if (ex.SocketErrorCode == SocketError.ConnectionReset)
                        continue;

                    throw;
                }
            }

            return result;
        }

        private void EnsureUdpCreated()
        {
            if (_udp != null)
                return;

            _udp = new UdpClient(new IPEndPoint(_localIp, _localPort));
            _udp.EnableBroadcast = true;
            // ⚡ 将单次静默超时从 500ms 压榨到 200ms：局域网 PLC 回包通常 <15ms，200ms 无新包即可判定结束
            _udp.Client.ReceiveTimeout = 200;

            // 屏蔽 Windows UDP Socket 的 ConnectionReset 异常崩溃
            try
            {
                _udp.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            }
            catch { /* 非 Windows 平台或特殊网卡驱动忽略 */ }
        }

        private byte[] BuildSearchPacket()
        {
            var packet = new byte[24];
            packet[0] = 0x03;
            packet[1] = 0x66;
            packet[2] = 0x14;
            packet[3] = 0x71;

            packet[8] = 0x01;

            Array.Copy(_localAmsNetId, 0, packet, 12, 6);

            packet[18] = 0x10;
            packet[19] = 0x27;

            return packet;
        }

        private sealed class ParsedTextFields
        {
            public string HostName { get; set; } = string.Empty;
            public string TwinCATVersion { get; set; } = string.Empty;
            public string OsVersion { get; set; } = string.Empty;
            public string Fingerprint { get; set; } = string.Empty;
            public string AmsNetId { get; set; } = string.Empty;
        }

        private static BeckhoffModel? ParseResponse(IPEndPoint remote, byte[] data)
        {
            if (data == null || data.Length < 20)
                return null;

            if (data[0] != 0x03 || data[1] != 0x66)
                return null;

            var parsed = ParseDeviceTextFields(data);

            string amsNetId = parsed.AmsNetId;
            if (string.IsNullOrEmpty(amsNetId) && data.Length >= 18)
            {
                amsNetId = $"{data[12]}.{data[13]}.{data[14]}.{data[15]}.{data[16]}.{data[17]}";
            }

            return new BeckhoffModel
            {
                Ip = remote.Address.ToString(),
                AmsNetId = amsNetId,
                HostName = string.IsNullOrEmpty(parsed.HostName) ? "Unknown Device" : parsed.HostName,
                OsVersion = parsed.OsVersion,
                TwinCATVersion = parsed.TwinCATVersion,
                Fingerprint = parsed.Fingerprint
            };
        }

        private static ParsedTextFields ParseDeviceTextFields(byte[] data)
        {
            var fields = new ParsedTextFields();
            if (data == null || data.Length < 24)
                return fields;

            int currentIndex = 20;

            while (currentIndex <= data.Length - 4)
            {
                ushort blockType = (ushort)(data[currentIndex] | (data[currentIndex + 1] << 8));
                ushort blockLength = (ushort)(data[currentIndex + 2] | (data[currentIndex + 3] << 8));

                int valueOffset = currentIndex + 4;
                if (valueOffset + blockLength > data.Length)
                    break;

                switch (blockType)
                {
                    case 0x0001:
                        if (string.IsNullOrEmpty(fields.HostName))
                            fields.HostName = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                        break;

                    case 0x0005:
                        fields.HostName = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                        break;

                    case 0x0003:
                        if (blockLength == 4)
                        {
                            int major = data[valueOffset];
                            int minor = data[valueOffset + 1];
                            int build = data[valueOffset + 2] | (data[valueOffset + 3] << 8);
                            fields.TwinCATVersion = $"{major}.{minor}.{build}";
                        }
                        else
                        {
                            fields.TwinCATVersion = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                        }
                        break;

                    case 0x0004:
                        if (blockLength >= 4)
                        {
                            string rawText = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                            string rawUnicodeText = Encoding.Unicode.GetString(data, valueOffset, blockLength).Trim('\0', ' ');

                            if (rawText.Contains("Windows") || rawText.Contains("Win7") || rawText.Contains("Win10"))
                            {
                                fields.OsVersion = rawText;
                            }
                            else if (rawUnicodeText.Contains("Windows") || rawUnicodeText.Contains("Service Pack"))
                            {
                                fields.OsVersion = "Windows 7 ";
                            }
                            else if (blockLength >= 14)
                            {
                                int major = data[valueOffset + 4];
                                int minor = data[valueOffset + 8];
                                int build = data[valueOffset + 12] | (data[valueOffset + 13] << 8);

                                if (major == 0 && blockLength >= 16)
                                {
                                    major = data[valueOffset + 6] | (data[valueOffset + 7] << 8);
                                    minor = data[valueOffset + 10] | (data[valueOffset + 11] << 8);
                                    build = data[valueOffset + 14] | (data[valueOffset + 15] << 8);
                                }

                                if (major > 0)
                                {
                                    fields.OsVersion = GetOsNameFromBinary(major, minor, build);
                                }
                            }
                        }
                        break;
                }

                currentIndex += 4 + blockLength;
            }

            if (string.IsNullOrWhiteSpace(fields.HostName))
                fields.HostName = "Unknown Beckhoff Device";

            if (string.IsNullOrWhiteSpace(fields.OsVersion) || fields.OsVersion.Contains("0.0"))
                fields.OsVersion = "Windows 7";

            return fields;
        }

        private static string GetOsNameFromBinary(int major, int minor, int build)
        {
            if (major == 10)
            {
                return build switch
                {
                    10240 => "Windows 10 1507",
                    10586 => "Windows 10 1511",
                    14393 => "Windows 10 1607",
                    15063 => "Windows 10 1703",
                    16299 => "Windows 10 1709",
                    17134 => "Windows 10 1803",
                    17763 => "Windows 10 1809",
                    18362 => "Windows 10 1903",
                    18363 => "Windows 10 1909",
                    19041 => "Windows 10 2004",
                    19042 => "Windows 10 20H2",
                    19043 => "Windows 10 21H1",
                    19044 => "Windows 10 21H2",
                    19045 => "Windows 10 22H2",
                    22000 => "Windows 11 21H2",
                    22621 => "Windows 11 22H2",
                    22631 => "Windows 11 23H2",
                    _ => build >= 22000 ? $"Windows 11 Build {build}" : $"Windows 10 Build {build}"
                };
            }

            if (major == 6)
            {
                return minor switch
                {
                    0 => "Windows Vista",
                    1 => "Windows 7",
                    2 => "Windows 8",
                    3 => "Windows 8.1",
                    _ => $"Windows NT 6.{minor}"
                };
            }

            if (major == 5)
            {
                return minor switch
                {
                    0 => "Windows 2000",
                    1 => "Windows XP",
                    2 => "Windows XP 64-Bit / Server 2003",
                    _ => "Windows XP"
                };
            }

            if (major == 1 || major == 3)
                return "Windows CE";

            return $"Windows {major}.{minor} Build {build}";
        }

        public static BeckhoffRemoteDesktopType DetectRemoteDesktopType(string osVersion)
        {
            string os = (osVersion ?? string.Empty).ToUpperInvariant();

            if (os.Contains("WINDOWS CE") || os.Contains("WINCE") || os.Contains("COMPACT"))
                return BeckhoffRemoteDesktopType.WindowsCeCerHost;

            if (os.Contains("WINDOWS 10") || os.Contains("WIN10") || os.Contains("WINDOWS 11") ||
                os.Contains("WINDOWS 7") || os.Contains("WIN7") || os.Contains("WINDOWS XP") ||
                os.Contains("EMBEDDED STANDARD") || os.Contains("IOT ENTERPRISE"))
                return BeckhoffRemoteDesktopType.WindowsRdp;

            return BeckhoffRemoteDesktopType.Unknown;
        }

        private static byte[] ParseAmsNetId(string amsNetId)
        {
            if (string.IsNullOrWhiteSpace(amsNetId))
                throw new ArgumentException("AMS NetId 不能为空。");

            string[] parts = amsNetId.Split('.');
            if (parts.Length != 6)
                throw new ArgumentException("AMS NetId 格式错误，应为 6 段，例如 192.168.105.167.1.1。");

            var bytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                if (!byte.TryParse(parts[i], out bytes[i]))
                    throw new ArgumentException($"AMS NetId 第 {i + 1} 段非法：{parts[i]}");
            }

            return bytes;
        }

        public void Dispose()
        {
            _udp?.Dispose();
            _udp = null;
        }
    }
}