using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
public enum BeckhoffRouteType
{
    Static,
    Temporary
}

public sealed class BeckhoffRouteConnectRequest
{
    public string Address { get; set; } = string.Empty;

    public string UserName { get; set; } = "Administrator";

    public string Password { get; set; } = string.Empty;

    public BeckhoffRouteType RouteType { get; set; } = BeckhoffRouteType.Static;

    public string ModulePath { get; set; } =
        @"C:\TwinCAT\AdsApi\Powershell\TcXaeMgmt\TcXaeMgmt.psd1";
}

public sealed class BeckhoffRouteConnectResult
{
    /// <summary>
    /// Add-AdsRoute 指令是否执行成功
    /// </summary>
    public bool CommandSuccess { get; set; }

    /// <summary>
    /// Add-AdsRoute 成功后，Test-AdsRoute 是否 Ok
    /// </summary>
    public bool Connected { get; set; }

    public string CommandOutput { get; set; } = string.Empty;

    public string CommandError { get; set; } = string.Empty;

    public string TestOutput { get; set; } = string.Empty;

    public string TestError { get; set; } = string.Empty;
}

public sealed class BeckhoffRouteConnector
{
    public BeckhoffRouteConnectResult Connect(BeckhoffRouteConnectRequest request)
    {
        Validate(request);

        var result = new BeckhoffRouteConnectResult();

        PowerShellResult addResult = ExecuteAddRoute(request);

        result.CommandSuccess = addResult.ExitCode == 0;
        result.CommandOutput = addResult.Output;
        result.CommandError = addResult.Error;

        if (!result.CommandSuccess)
        {
            Console.WriteLine("Add-AdsRoute Error:");
            Console.WriteLine(addResult.Error);
            return result;
        }

        Console.WriteLine("OK");

        PowerShellResult testResult = ExecuteTestRoute(request);

        result.TestOutput = testResult.Output;
        result.TestError = testResult.Error;
        result.Connected = testResult.ExitCode == 0;

        if (result.Connected)
        {
            Console.WriteLine("Connected: OK");
        }
        else
        {
            Console.WriteLine("Connected: Failed");

            if (!string.IsNullOrWhiteSpace(testResult.Error))
                Console.WriteLine(testResult.Error);

            if (!string.IsNullOrWhiteSpace(testResult.Output))
                Console.WriteLine(testResult.Output);
        }

        return result;
    }

    private PowerShellResult ExecuteAddRoute(BeckhoffRouteConnectRequest request)
    {
        string tempParam = request.RouteType == BeckhoffRouteType.Temporary
            ? "-Temporary"
            : string.Empty;

        string script = $@"
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

try {{
    Import-Module '{Escape(request.ModulePath)}' -Force

    Get-Command Add-AdsRoute -ErrorAction Stop | Out-Null

    $user = '{Escape(request.UserName)}'
    $password = '{Escape(request.Password)}'

    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential($user, $securePassword)

    Add-AdsRoute -Address '{Escape(request.Address)}' -Credential $cred {tempParam}

    Write-Output 'OK'
    exit 0
}}
catch {{
    [Console]::Error.WriteLine('PSERR:' + $_.Exception.Message)
    exit 1
}}
";

        return RunPowerShell(script);
    }

    private PowerShellResult ExecuteTestRoute(BeckhoffRouteConnectRequest request)
    {
        string script = $@"
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

try {{
    Import-Module '{Escape(request.ModulePath)}' -Force

    Get-Command Get-AdsRoute -ErrorAction Stop | Out-Null
    Get-Command Test-AdsRoute -ErrorAction Stop | Out-Null

    $test = Get-AdsRoute -All -Address '{Escape(request.Address)}' | Test-AdsRoute

    $test | Format-Table -AutoSize | Out-String | Write-Output

    $ok = $test | Where-Object {{ $_.Result -eq 'Ok' }}

    if ($ok) {{
        exit 0
    }} else {{
        exit 2
    }}
}}
catch {{
    [Console]::Error.WriteLine('PSERR:' + $_.Exception.Message)
    exit 1
}}
";

        return RunPowerShell(script);
    }

    private static PowerShellResult RunPowerShell(string script)
    {
        string encodedCommand = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments =
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
                encodedCommand,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process();

        process.StartInfo = psi;
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        error = CleanPowerShellError(error);

        return new PowerShellResult
        {
            ExitCode = process.ExitCode,
            Output = output.Trim(),
            Error = error
        };
    }
    private static string CleanPowerShellError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return string.Empty;

        string text = error.Trim();

        int index = text.IndexOf("PSERR:", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            text = text.Substring(index + "PSERR:".Length).Trim();
        }

        text = text
            .Replace("\r", " ")
            .Replace("\n", " ");

        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    private static string Escape(string value)
    {
        return value.Replace("'", "''");
    }

    private static void Validate(BeckhoffRouteConnectRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Address))
            throw new ArgumentException("Address 不能为空。");

        if (string.IsNullOrWhiteSpace(request.UserName))
            throw new ArgumentException("UserName 不能为空。");

        if (request.Password == null)
            request.Password = string.Empty;

        if (string.IsNullOrWhiteSpace(request.ModulePath))
            throw new ArgumentException("ModulePath 不能为空。");

        if (!File.Exists(request.ModulePath))
            throw new FileNotFoundException("找不到 TcXaeMgmt.psd1。", request.ModulePath);
    }

    private sealed class PowerShellResult
    {
        public int ExitCode { get; set; }

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;
    }
}