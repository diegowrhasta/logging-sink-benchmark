// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using Logging.Sink.Benchmark.Console;

Console.WriteLine("Press any key to start benchmark");
Console.ReadLine();
BenchmarkRunner.Run<SerilogBenchmark>();