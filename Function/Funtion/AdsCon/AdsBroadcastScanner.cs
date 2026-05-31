using Function.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

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

        private List<BeckhoffModel> Search(IPAddress targetIp, int timeoutMs)
        {
            var result = new List<BeckhoffModel>();
            var receivedKeys = new HashSet<string>();

            EnsureUdpCreated();

            byte[] request = BuildSearchPacket();

            var targetEndPoint = new IPEndPoint(targetIp, BeckhoffDiscoveryPort);

            _udp!.Send(request, request.Length, targetEndPoint);

            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);

            while (DateTime.Now < deadline)
            {
                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref remote);

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

        private static BeckhoffModel? ParseResponse(IPEndPoint remote, byte[] data)
        {
            if (data == null || data.Length < 20)
                return null;

            if (data[0] != 0x03 ||
                data[1] != 0x66 ||
                data[2] != 0x14 ||
                data[3] != 0x71)
            {
                return null;
            }

            string amsNetId = string.Empty;
            int amsPort = 0;

            if (data.Length >= 18)
            {
                amsNetId = $"{data[12]}.{data[13]}.{data[14]}.{data[15]}.{data[16]}.{data[17]}";
            }

            if (data.Length >= 20)
            {
                amsPort = data[18] | (data[19] << 8);
            }

            var parsed = ParseDeviceTextFields(data);
            var remoteType = DetectRemoteDesktopType(parsed.OsVersion);

            return new BeckhoffModel
            {
                Ip = remote.Address.ToString(),
                AmsNetId = amsNetId,
                
                HostName = parsed.HostName,
               
                OsVersion = parsed.OsVersion,
               
            };
        }

        private sealed class ParsedTextFields
        {
            public string HostName { get; set; } = string.Empty;
            public string TwinCATVersion { get; set; } = string.Empty;
            public string OsVersion { get; set; } = string.Empty;
            public string Fingerprint { get; set; } = string.Empty;
        }

        private static ParsedTextFields ParseDeviceTextFields(byte[] data)
        {
            var fields = new ParsedTextFields();

            string ascii = ToPrintableAscii(data);

            var tokens = ascii
                .Split(new[] { ' ', '\t', '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            fields.HostName = ExtractHostName(tokens);
            fields.TwinCATVersion = ExtractTwinCatVersion(tokens);

            fields.OsVersion = ParseOsVersionFromSystemInfoBlock(data);

            if (string.IsNullOrWhiteSpace(fields.OsVersion))
                fields.OsVersion = ExtractOsVersion(ascii, tokens);

            fields.Fingerprint = ExtractFingerprint(tokens);

            return fields;
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