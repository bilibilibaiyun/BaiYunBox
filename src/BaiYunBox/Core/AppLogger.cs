using System.Text;

namespace BaiYunBox.Core;

/// <summary>简单文件日志。滚动到 log 文件，控制台可忽略。</summary>
public static class AppLogger
{
    private static readonly object _lock = new();
    private static string _logDir = string.Empty;

    public static void Init(string logDirectory)
    {
        _logDir = logDirectory;
        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch
        {
            // 忽略
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        if (string.IsNullOrEmpty(_logDir)) return;

        lock (_lock)
        {
            try
            {
                var file = Path.Combine(_logDir, $"baiyunbox-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // 日志写入失败静默
            }
        }
    }
}
