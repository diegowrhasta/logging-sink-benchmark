# Logging Sink Benchmarks

As we know, software is complicated, and sometimes issues that may arise in the 
future or only under certain conditions will present themselves, and if we didn't 
take precautions (we lacked the foresight), we will start paying for the broken 
dishes.

One of those such problems can be _port exhaustion_ or _thread exhaustion_. If 
we relied heavily on sync calls, and our application gets bigger and bigger loads, 
those sync calls will start to bottleneck. Making operations _async_ takes advantage 
of multithreading, in the sense that we don't block a thread until all I/O operations 
are done, we release it for other request to go in and make use of it.

There are many ways to deal with the problem, **but one of them** that whilst not 
_as_ important, a factor still, would be **logging**. Specially if our environments that 
are getting bombarded have settings that capture tons of logs, with a big spike of 
traffic, logs will start also adding into the deterioration of the application. And 
so, a solution for this is to also introduce the idea of async into the _logs_ 
themselves.

## Serilog and Async Sinks

Serilog, has a special namespace that introduces async wrappers for the different 
sinks that it offers, `Serilog.Sinks.Console.Async`.

All the dependencies we have in our Console Project are:

- `BenchmarkDotNet`
- `Serilog`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`
- `Serilog.Sinks.Async`
- `Serilog.Formatting.Compact`

The configuration is pretty straightforward, we declare a `Serilog` json object 
as we would for normal sync sinks, however we will wrap them with an `async` wrapper.

_Note:_ It's worth adding that I/O operations are the most taxing when it comes to 
computing power, (File Writing, Sending logs over the network). So depending on 
how many sinks we have configured, the more performance might be degrading.

## Benchmarking

_Note:_ Never forget to run the application in Release mode to ensure optimizations 
are applied.

First of all we can get a bit of thread information by adding the `[ThreadingDiagnoser]` 
decorator to our benchmark class. This however only returns two columns in the 
summary:

Completed Work Items: If the benchmark code utilizes `Task.Run`, `ThreadPool.QueueUserWorkItem`, 
`Parallel.For` or other asynchronous mechanisms that rely on the ThreadPool, this 
value will be non-zero. A high number might indicate significant use of multi-threading, 
which could be good or bad depending on whether the workload benefits from it. If the 
benchmark is expected to be CPU-bound and should not involve threading, but this 
number is high it might indicate unintended parallelism.

Lock Contentions: This counts how many times a thread was **blocked waiting for a 
lock**, a high value here suggests _frequent contention_ meaning that multiple 
threads are competing for the same lock, which can degrade performance. This 
number ideally should be **low** to be sure that threads are not frequently waiting 
and blocking each other.

How to make sense of both of these?

If **Completed Work Items** is high but **Lock Contentions** is low, it may 
suggest that the workload is effectively utilizing multi-threading without excessive blocking,
if **Lock Contentions** is high, performance might be bottlenecked by sync overhead, 
and optimizations may be needed, lastly if both are low, the benchmark might be 
single-threaded or not utilizing the ThreadPool significantly.

### Ideas

We can make usage of **custom columns** to add to the last summary table, however 
these are computed by the end of the whole benchmark, so custom logic has to be 
built around capturing that data as each case takes place, and then by the end 
adding an aggregated result.

Here's some sample code on how to declare a custom column for the final summary 
table:

```csharp
public class ThreadPoolSizeColumn : IColumn
{
    public string Id => "ThreadUsage";
    public string ColumnName => "Thread Usage (Avg)";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Average thread usage during the benchmark.";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var avgUsage = ThreadUsageTracker.GetAverageUsage();
        return
            $"WorkerThreadsInUse={avgUsage.AvgWorkerThreadsInUse:F2}, CompletionPortThreadsInUse={avgUsage.AvgCompletionPortThreadsInUse:F2}";
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) =>
        GetValue(summary, benchmarkCase);

    bool IColumn.IsDefault(Summary summary, BenchmarkCase benchmarkCase)
    {
        return false;
    }

    public bool IsAvailable(Summary summary) => true;
}
```

And the way to added to the benchmarker would be like: 

```csharp
var config = DefaultConfig.Instance.AddColumn(new ThreadPoolSizeColumn());
BenchmarkRunner.Run<SerilogBenchmark>(config);
```

And a way to retrieve the status of the thread pool plus threads that are available 
would be by doing something like this:

```csharp
// Get maximum threads
ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxCompletionPortThreads);

// Get available threads
ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out int availableCompletionPortThreads);
```

In the end we didn't make use of this, but the tool `dotnet-counters`. The reference 
for it is at the end of this file. We added a stop in the run of the main program 
so that we get a window to attach to the `Logging.Sink.Benchmark.Console` process 
and after an **ENTER** we start running the benchmark, and we can see how threads 
start going up plus work item counts.

## Notes on logging behavior for files

If we take a look at `Path.Utils`, there's a function that helps resolve the root 
of the project folder. We have to add this intermediate step due to the nature of 
how the method syntax works. If we were to use some kind of config file such as 
`appsettings.json` in ASP.NET we can easily switch to that same directory by adding 
a relative path such as `logs/log.txt`. However, for things such as our specific use 
case we have to compute the path programatically.

If we don't feed a proper path, then the file will never end up getting created nor 
something written onto it.

## Conclusions

Here's a trace of one random benchmark that was run:

| Method       | Mean         | Error       | StdDev       | Completed Work Items | Lock Contentions | Gen0    | Gen1    | Gen2   | Allocated  |
|------------- |-------------:|------------:|-------------:|---------------------:|-----------------:|--------:|--------:|-------:|-----------:|
| SyncLogging  | 112,450.1 us | 4,147.19 us | 12,228.08 us |                    - |                - |       - |       - |      - | 1435.98 KB |
| AsyncLogging |     429.0 us |    20.19 us |     59.53 us |                    - |           0.0010 | 42.4805 | 10.7422 | 5.3711 |  611.36 KB |

As we can see, the async logging sink (that goes to a Console, and a File) completely 
beats sync logging, both in terms of speed, but also in allocation of memory, there's 
a low level of contention (there's at the very least the mention that there's lock 
contention), meaning that threads are not ending blocked an awful lot.

And by analyzing a benchmark session with `dotnet-counters`, in where we did 
two different tests:

_Only Sync Sinks:_

_Only Async Sinks:_

## References

- [Concurrency and Asynchrony - C# 12 in a Nutshell](https://a.co/d/eUe6Huq)
- [Asynchronous Programming - Coding Clean, Reliable, and Safe REST APIs with ASP.NET Core 8: Develop Robust Minimal APIs with .NET 8](https://a.co/d/a0PPLmW)
- [Performance Considerations - Solutions Architect's Handbook: Kick-start your career with architecture design principles, strategies, and generative AI techniques](https://a.co/d/gBu8cRm)
- [Diagnosing thread pool exhaustion issues in .NET Core apps](https://www.youtube.com/watch?v=isK8Cel3HP0)