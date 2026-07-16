using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Function.Models;
using Microsoft.VSDiagnostics;

namespace Function.Benchmarks;
[CPUUsageDiagnoser]
public class SearchDeviceBenchmark
{
    private BeckhoffModel _device = null !;
    [GlobalSetup]
    public void Setup()
    {
        _device = new BeckhoffModel
        {
            Ip = "127.0.0.1"
        };
    }

    [Benchmark]
    public bool CheckRoute()
    {
        return BeckhoffRouteConnector.CheckRoute(_device);
    }
}