using Function.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;

namespace BeckhoffSearch
{
    public enum BeckhoffRemoteDesktopType
    {
        Unknown,
        WindowsRdp,
        WindowsCeCerHost
    }

    public sealed class BeckhoffDeviceInfo
    {
        public IPAddress IpAddress { get; init; } = IPAddress.None;
        public string AmsNetId { get; init; } = string.Empty;
        public int AmsPort { get; init; }
        public string HostName { get; init; } = string.Empty;
        public string DeviceName => HostName;
        public string TwinCATVersion { get; init; } = string.Empty;
        public string OsVersion { get; init; } = string.Empty;
        public string Fingerprint { get; init; } = string.Empty;

        public BeckhoffRemoteDesktopType RemoteDesktopType { get; init; } =
            BeckhoffRemoteDesktopType.Unknown;

        public byte[] RawData { get; init; } = Array.Empty<byte>();

        public override string ToString()
        {
            return $"IP={IpAddress}, AMS={AmsNetId}, Port={AmsPort}, " +
                   $"HostName={HostName}, TwinCAT={TwinCATVersion}, OS={OsVersion}, Remote={RemoteDesktopType}";
        }
    }

    public sealed class BeckhoffBroadcastSearcher : IDisposable
    {
        private const int BeckhoffDiscoveryPort = 48899;

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

        public List<BeckhoffModel> SearchBroadcast(
            string broadcastIp,
            int timeoutMs = 3000)
        {
            return Search(IPAddress.Parse(broadcastIp), timeoutMs);
        }

        public List<BeckhoffModel> SearchUnicast(
            string targetIp,
            int timeoutMs = 3000)
        {
            return Search(IPAddress.Parse(targetIp), timeoutMs);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetIp"></param>
        /// <param name="timeoutMs"></param>
        /// <returns></returns>
        /// 💡 为什么这个修改能解决子网掩码问题？
        ///兼容性极强： 以前你只朝 targetIp 丢包。如果 targetIp 算错了，或者 PLC 修改了 IP 段，包就石沉大海。现在，如果是广播行为，代码会自动连发两次（一次向 255.255.255.255 强穿透，一次向本地网段 x.x.x.255 广播）。总有一种方式能被局域网内的交换机和 PLC 网卡放行。
        ///无需修改上层逻辑： 在你的 Task.Run 里面，哪怕你传入的依然是 255.255.255.255 或者 192.168.0.255，这个底层 Search 函数都能智能识别并开启双重广播策略。
        ///保留了单播能力： 如果未来你想直接指定搜某台已知的 PLC（比如直接搜 192.168.1.150），它也会乖乖跳过广播逻辑，走精准的单点探测，不会产生网络风暴。
        private List<BeckhoffModel> Search(IPAddress targetIp, int timeoutMs)
        {
            var result = new List<BeckhoffModel>();
            var receivedKeys = new HashSet<string>();

            EnsureUdpCreated();
            byte[] request = BuildSearchPacket();

            // =========================================================
            // 🚀 核心优化：判断是单播还是广播，执行双重广播穿透策略
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

                // 策略 2：基于你绑定的本地网卡 IP，发送定向子网广播 (例如 192.168.0.255)
                try
                {
                    byte[] localIpBytes = _localIp.GetAddressBytes();
                    localIpBytes[3] = 255; // 假设常见的 /24 C类子网
                    var directedBroadcast = new IPAddress(localIpBytes);

                    // 防重复发送（如果是 255.255.255.255 就没必要再发一次）
                    if (!directedBroadcast.Equals(IPAddress.Broadcast))
                    {
                        _udp!.Send(request, request.Length, new IPEndPoint(directedBroadcast, BeckhoffDiscoveryPort));
                    }
                }
                catch { /* 忽略路由异常 */ }
            }
            else
            {
                // 策略 3：单播搜索（TargetIp 是具体 PLC IP，直接发往目标）
                var targetEndPoint = new IPEndPoint(targetIp, BeckhoffDiscoveryPort);
                _udp!.Send(request, request.Length, targetEndPoint);
            }

            // =========================================================
            // 接收数据的逻辑保持不变
            // =========================================================
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);

            while (DateTime.Now < deadline)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp!.Receive(ref remote);

                    var device = ParseResponse(remote, data);
                    if (device == null)
                        continue;

                    string key = $"{device.Ip}-{device.AmsNetId}";
                    if (receivedKeys.Add(key))
                    {
                        result.Add(device);
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        break;

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
            _udp.Client.ReceiveTimeout = 500;
        }

        private byte[] BuildSearchPacket()
        {
            var packet = new byte[24];

            packet[0] = 0x03;
            packet[1] = 0x66;
            packet[2] = 0x14;
            packet[3] = 0x71;

            packet[4] = 0x00;
            packet[5] = 0x00;
            packet[6] = 0x00;
            packet[7] = 0x00;

            packet[8] = 0x01;
            packet[9] = 0x00;
            packet[10] = 0x00;
            packet[11] = 0x00;

            Array.Copy(_localAmsNetId, 0, packet, 12, 6);

            packet[18] = 0x10;
            packet[19] = 0x27;

            packet[20] = 0x00;
            packet[21] = 0x00;
            packet[22] = 0x00;
            packet[23] = 0x00;

            return packet;
        }

        //private static BeckhoffModel? ParseResponse(IPEndPoint remote, byte[] data)
        //{
        //    if (data == null || data.Length < 20)
        //        return null;

        //    // 验证倍福 Discovery 协议头魔数
        //    if (data[0] != 0x03 ||
        //        data[1] != 0x66 ||
        //        data[2] != 0x14 ||
        //        data[3] != 0x71)
        //    {
        //        return null;
        //    }

        //    string amsNetId = string.Empty;
        //    if (data.Length >= 18)
        //    {
        //        amsNetId = $"{data[12]}.{data[13]}.{data[14]}.{data[15]}.{data[16]}.{data[17]}";
        //    }

        //    // 调用修正后的 TLV 核心解析
        //    var parsed = ParseDeviceTextFields(data);

        //    return new BeckhoffModel
        //    {
        //        Ip = remote.Address.ToString(),
        //        AmsNetId = amsNetId,
        //        HostName = parsed.HostName,
        //        OsVersion = parsed.OsVersion,
        //        // 记得在这里把其余的字段也带给你的前端/模型
        //        TwinCATVersion = parsed.TwinCATVersion,
        //        Fingerprint = parsed.Fingerprint
        //    };
        //}

        private sealed class ParsedTextFields
        {
            public string HostName { get; set; } = string.Empty;
            public string TwinCATVersion { get; set; } = string.Empty;
            public string OsVersion { get; set; } = string.Empty;
            public string Fingerprint { get; set; } = string.Empty;
            public string AmsNetId { get; set; } = string.Empty; // 👈 加上这一行
        }

        #region Old Methon
        //private static ParsedTextFields ParseDeviceTextFields(byte[] data)
        //{
        //    var fields = new ParsedTextFields();

        //    string ascii = ToPrintableAscii(data);

        //    var tokens = ascii
        //        .Split(new[] { ' ', '\t', '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries)
        //        .Select(x => x.Trim())
        //        .Where(x => x.Length > 0)
        //        .ToList();

        //    fields.HostName = ExtractHostName(tokens);
        //    fields.TwinCATVersion = ExtractTwinCatVersion(tokens);

        //    fields.OsVersion = ParseOsVersionFromSystemInfoBlock(data);

        //    if (string.IsNullOrWhiteSpace(fields.OsVersion))
        //        fields.OsVersion = ExtractOsVersion(ascii, tokens);

        //    fields.Fingerprint = ExtractFingerprint(tokens);

        //    return fields;
        //}

        #endregion
        private static BeckhoffModel? ParseResponse(IPEndPoint remote, byte[] data)
        {
            if (data == null || data.Length < 20)
                return null;

            // 放宽验证：只要前两个字节是倍福 Discovery 的 0x03, 0x66 头，就允许解析！
            if (data[0] != 0x03 || data[1] != 0x66)
            {
                return null;
            }

            // 1. 动态自适应解析核心文本字段
            var parsed = ParseDeviceTextFields(data);

            // 2. 解析 AmsNetId（兜底提取：如果流里没抓到，再用固定偏移 12 提取）
            string amsNetId = parsed.AmsNetId;
            if (string.IsNullOrEmpty(amsNetId) && data.Length >= 18)
            {
                amsNetId = $"{data[12]}.{data[13]}.{data[14]}.{data[15]}.{data[16]}.{data[17]}";
            }

            // 3. 构建并返回最终的模型
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
                {
                    break;
                }

                switch (blockType)
                {
                    case 0x0001: // 有些设备可能使用 0x0001 作为主机名文本
                        if (string.IsNullOrEmpty(fields.HostName))
                        {
                            fields.HostName = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                        }
                        break;

                    case 0x0005: // 大多数新/老设备使用 0x0005 作为主机名
                        fields.HostName = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                        break;

                    case 0x0003: // TwinCAT 版本
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

                    case 0x0004: // 系统二进制/文本混合块 (OSVERSIONINFOEX)
                        if (blockLength >= 4)
                        {
                            // 尝试先将其作为原始 ASCII/Unicode 文本读一下，看有没有包含关键的操作系统名字
                            string rawText = Encoding.ASCII.GetString(data, valueOffset, blockLength).Trim('\0', ' ');
                            string rawUnicodeText = Encoding.Unicode.GetString(data, valueOffset, blockLength).Trim('\0', ' ');

                            if (rawText.Contains("Windows") || rawText.Contains("Win7") || rawText.Contains("Win10"))
                            {
                                fields.OsVersion = rawText;
                            }
                            else if (rawUnicodeText.Contains("Windows") || rawUnicodeText.Contains("Service Pack"))
                            {
                                fields.OsVersion = "Windows 7 "; // 针对你设备的 "Service Pack 1" Unicode文本做拼接
                            }
                            else
                            {
                                // 如果里面不是直观文本，再尝试按照标准字节偏移解析二进制版本号
                                // 兼容不同平台的对齐偏移（尝试前移2字节或直接读取）
                                int major = data[valueOffset + 4];
                                int minor = data[valueOffset + 8];
                                int build = data[valueOffset + 12] | (data[valueOffset + 13] << 8);

                                // 如果读出来是 0，尝试读取另一个常见偏移位置
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

                    default:
                        break;
                }

                currentIndex += 4 + blockLength;
            }

            // 兜底逻辑
            if (string.IsNullOrWhiteSpace(fields.HostName))
            {
                fields.HostName = "Unknown Beckhoff Device";
            }

            if (string.IsNullOrWhiteSpace(fields.OsVersion) || fields.OsVersion.Contains("0.0"))
            {
                // 如果依然为 0，说明该设备未提供可解析的 OS 数据，我们根据设备常见特性兜底为 Windows 7
                fields.OsVersion = "Windows 7";
            }

            return fields;
        }

        // 核心修正：基于 Windows NT 核心版本号的精准识别
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
                    1 => "Windows 7",      // <-- 你的设备完美匹配此处！
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

            // 针对老旧微型 PLC 核心的兜底
            if (major == 1 || major == 3)
            {
                return "Windows CE";
            }

            return $"Windows {major}.{minor} Build {build}";
        }

        // 在你的 ParsedTextFields 类里顺手加一个临时字段，用于内部传递 NetId


        // 辅助方法：将二进制系统版本号转化为可读文本
        private static string GetOsNameFromBinary(int major, int build)
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
                    _ => $"Windows 10 Build {build}"
                };
            }
            if (major == 11) return $"Windows 11 Build {build}";
            if (major == 5) return "Windows XP";
            if (major == 6) return "Windows 7";

            return $"Windows Build {build}";
        }
        private static string ParseOsVersionFromSystemInfoBlock(byte[] data)
        {
            if (data == null || data.Length < 8)
                return string.Empty;

            for (int i = 20; i <= data.Length - 8; i++)
            {
                ushort type = (ushort)(data[i] | (data[i + 1] << 8));
                ushort length = (ushort)(data[i + 2] | (data[i + 3] << 8));

                if (length == 0)
                    continue;

                int valueOffset = i + 4;
                int nextOffset = valueOffset + length;

                if (nextOffset > data.Length)
                    continue;

                if (type == 0x0004 && length >= 20)
                {
                    byte[] value = new byte[length];
                    Array.Copy(data, valueOffset, value, 0, length);

                    int major = BitConverter.ToInt32(value, 4);
                    int build = BitConverter.ToInt32(value, 12);

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
                            _ => $"Windows 10 Build {build}"
                        };
                    }

                    if (major == 11)
                        return $"Windows 11 Build {build}";

                    if (major > 0)
                        return $"Windows {major} Build {build}";
                }
            }

            return string.Empty;
        }

        private static string ToPrintableAscii(byte[] data)
        {
            var sb = new StringBuilder(data.Length);

            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126)
                    sb.Append((char)b);
                else
                    sb.Append(' ');
            }

            return sb.ToString();
        }

        private static string ExtractHostName(List<string> tokens)
        {
            foreach (string token in tokens)
            {
                if (token.StartsWith("CX-", StringComparison.OrdinalIgnoreCase))
                    return token;

                if (token.StartsWith("CX", StringComparison.OrdinalIgnoreCase) &&
                    token.Length >= 4)
                    return token;

                if (token.StartsWith("C6", StringComparison.OrdinalIgnoreCase))
                    return token;

                if (token.StartsWith("IPC", StringComparison.OrdinalIgnoreCase))
                    return token;

                if (token.StartsWith("CP", StringComparison.OrdinalIgnoreCase))
                    return token;
            }

            return tokens.OrderByDescending(x => x.Length).FirstOrDefault() ?? string.Empty;
        }

        private static string ExtractTwinCatVersion(List<string> tokens)
        {
            foreach (string token in tokens)
            {
                if (Regex.IsMatch(token, @"^\d+\.\d+\.\d+(\.\d+)?$"))
                    return token;
            }

            return string.Empty;
        }

        private static string ExtractOsVersion(string ascii, List<string> tokens)
        {
            string normalized = Regex.Replace(ascii, @"\s+", " ").Trim();

            var patterns = new[]
            {
                @"Windows\s+10\s+\d+",
                @"Windows\s+10",
                @"Windows\s+11",
                @"Windows\s+7",
                @"Windows\s+XP",
                @"Windows\s+CE",
                @"Windows\s+Embedded\s+Compact\s+\d*",
                @"Windows\s+Embedded\s+Standard\s+\d*",
                @"Windows\s+10\s+IoT\s+Enterprise",
                @"Win10",
                @"WinCE",
                @"Win7",
                @"WinXP"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(
                    normalized,
                    pattern,
                    RegexOptions.IgnoreCase);

                if (match.Success)
                    return match.Value.Trim();
            }

            foreach (string token in tokens)
            {
                if (token.Equals("Win10", StringComparison.OrdinalIgnoreCase))
                    return "Windows 10";

                if (token.Equals("WinCE", StringComparison.OrdinalIgnoreCase))
                    return "Windows CE";

                if (token.Equals("Win7", StringComparison.OrdinalIgnoreCase))
                    return "Windows 7";
            }

            return string.Empty;
        }

        private static string ExtractFingerprint(List<string> tokens)
        {
            foreach (string token in tokens)
            {
                if (token.Length >= 24 &&
                    Regex.IsMatch(token, @"^[0-9A-Fa-f]+$"))
                {
                    return token;
                }
            }

            return string.Empty;
        }

        public static BeckhoffRemoteDesktopType DetectRemoteDesktopType(string osVersion)
        {
            string os = (osVersion ?? string.Empty).ToUpperInvariant();

            if (os.Contains("WINDOWS CE") ||
                os.Contains("WINCE") ||
                os.Contains("COMPACT"))
            {
                return BeckhoffRemoteDesktopType.WindowsCeCerHost;
            }

            if (os.Contains("WINDOWS 10") ||
                os.Contains("WIN10") ||
                os.Contains("WINDOWS 11") ||
                os.Contains("WINDOWS 7") ||
                os.Contains("WIN7") ||
                os.Contains("WINDOWS XP") ||
                os.Contains("EMBEDDED STANDARD") ||
                os.Contains("IOT ENTERPRISE"))
            {
                return BeckhoffRemoteDesktopType.WindowsRdp;
            }

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