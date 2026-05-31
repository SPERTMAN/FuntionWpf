// See https://aka.ms/new-console-template for more information
// 本机 IP：你的网卡 IP
using BeckhoffSearch;
using System.Diagnostics;
using TwinCAT.Ads;

string localIp = "192.168.0.150";

// 本机 AMS NetId：从 TwinCAT 抓包看到的是 C0 A8 69 A7 01 01
// 对应 192.168.105.167.1.1
string localAmsNetId = "192.168.105.167.1.1";

using var searcher = new BeckhoffBroadcastSearcher(
    localIp,
    localAmsNetId,
    localPort: 50000);

// 方式 1：已知 PLC IP，单播搜索
//var devices1 = searcher.SearchUnicast("192.168.0.50", timeoutMs: 3000);

//foreach (var device in devices1)
//{
//    Console.WriteLine(device);
//}

Console.WriteLine("--------------");

// 方式 2：未知设备，广播搜索，可能返回多个设备
var devices2 = searcher.SearchBroadcast("192.168.0.255", timeoutMs: 3000);

foreach (var device in devices2)
{
    Console.WriteLine(device);
}


var connector = new BeckhoffRouteConnector();

var result = connector.Connect(new BeckhoffRouteConnectRequest
{
    Address = "192.168.0.50",
    UserName = "Administrato",
    Password = "1",

    // Static：不带 -Temporary
    // Temporary：带 -Temporary
    RouteType = BeckhoffRouteType.Temporary,

    ModulePath = @"C:\TwinCAT\AdsApi\Powershell\TcXaeMgmt\TcXaeMgmt.psd1"
});

Console.WriteLine("-------- Result --------");
Console.WriteLine($"CommandSuccess: {result.CommandSuccess}");
Console.WriteLine($"Connected: {result.Connected}");

if (!result.CommandSuccess)
{
    Console.WriteLine("Command Error:");
    Console.WriteLine(result.CommandError);
}

//var psi = new ProcessStartInfo
//{
//    FileName = "E:\\Desktop\\TOOL\\CERHOST.exe",
//    Arguments = "192.168.0.50",
//    UseShellExecute = true
//};

//Process.Start(psi);
