using BenchmarkDotNet.Attributes;
using Serilog;

namespace Logging.Sink.Benchmark.Console;

[MemoryDiagnoser]
[ThreadingDiagnoser]
public class SerilogBenchmark
{
    private readonly ILogger _syncLogger;
    private readonly ILogger _asyncLogger;

    public SerilogBenchmark()
    {
        // Initialize synchronous logger
        _syncLogger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(
                path: PathUtils.GetLogFilePath(),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter())
            .CreateLogger();

        // Initialize asynchronous logger
        _asyncLogger = new LoggerConfiguration()
            .WriteTo.Async(a => a.Console())
            .WriteTo.Async(a => a.File(
                path: PathUtils.GetLogFilePath(),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter()))
            .CreateLogger();
    }
    
    [Benchmark]
    public void SyncLogging()
    {
        for (var i = 0; i < 1000; i++)
        {
            _syncLogger.Information("This is a sync log message {Number}", i);
        }
    }

    [Benchmark]
    public void AsyncLogging()
    {
        for (var i = 0; i < 1000; i++)
        {
            _asyncLogger.Information("This is an async log message {Number}", i);
        }
    }
}