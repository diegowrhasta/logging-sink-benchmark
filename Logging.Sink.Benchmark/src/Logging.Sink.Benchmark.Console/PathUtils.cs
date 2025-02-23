namespace Logging.Sink.Benchmark.Console;

public static class PathUtils
{
    public static string GetLogFilePath()
    {
        var currentDir = AppContext.BaseDirectory; // or Directory.GetCurrentDirectory()
        var dir = new DirectoryInfo(currentDir);

        while (dir != null && !dir.Name.Equals("Logging.Sink.Benchmark.Console", StringComparison.OrdinalIgnoreCase))
        {
            dir = dir.Parent;
        }

        var srcFolder = dir?.FullName ?? throw new Exception("src folder not found");
        
        return Path.Combine(srcFolder, "logs", "log.txt");
    }
}