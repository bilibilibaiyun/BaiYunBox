using System.Reflection;

namespace BaiYunBox.Core;

/// <summary>
/// 应用路径管理。数据目录默认 D 盘（D:\Users\&lt;用户&gt;\BaiYunBox），
/// 无 D 盘时回退 %LOCALAPPDATA%\BaiYunBox，可用 --data-dir 覆盖。
/// </summary>
public static class AppPaths
{
    public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>程序可执行文件所在目录（安装目录或便携运行目录）。</summary>
    public static string AppDirectory => AppContext.BaseDirectory;

    /// <summary>数据目录（日志/配置/历史/模型/下载）。</summary>
    public static string DataDirectory { get; private set; } = ResolveDefaultDataDirectory();

    /// <summary>日志目录。</summary>
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>配置目录。</summary>
    public static string ConfigDirectory => DataDirectory;

    /// <summary>whisper 转录模型目录。</summary>
    public static string ModelDirectory => Path.Combine(DataDirectory, "models");

    /// <summary>播客下载目录。</summary>
    public static string PodcastDirectory => Path.Combine(DataDirectory, "podcasts");

    /// <summary>转录结果目录。</summary>
    public static string TranscriptDirectory => Path.Combine(DataDirectory, "transcripts");

    /// <summary>libmpv 引擎目录（随包内置）。</summary>
    public static string MpvDirectory => Path.Combine(AppDirectory, "engine", "mpv");

    /// <summary>whisper.cpp 引擎目录（随包内置）。</summary>
    public static string WhisperDirectory => Path.Combine(AppDirectory, "engine", "whisper");

    /// <summary>ffmpeg 引擎目录（随包内置，用于音频格式预处理）。</summary>
    public static string FfmpegDirectory => Path.Combine(AppDirectory, "engine", "ffmpeg");

    /// <summary>ffmpeg.exe 全路径。</summary>
    public static string FfmpegPath => Path.Combine(FfmpegDirectory, "ffmpeg.exe");

    /// <summary>libmpv DLL 全路径。</summary>
    public static string MpvDllPath
    {
        get
        {
            var libmpv = Path.Combine(MpvDirectory, "libmpv-2.dll");
            if (File.Exists(libmpv)) return libmpv;
            var mpv = Path.Combine(MpvDirectory, "mpv-2.dll");
            if (File.Exists(mpv)) return mpv;
            return libmpv;
        }
    }

    /// <summary>whisper-cli.exe 全路径。</summary>
    public static string WhisperCliPath => Path.Combine(WhisperDirectory, "whisper-cli.exe");

    /// <summary>处理 --data-dir 命令行参数。</summary>
    public static void ApplyCommandLine(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--data-dir" && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                var dir = Path.GetFullPath(args[i + 1]);
                DataDirectory = dir;
                EnsureDirectories();
                return;
            }
        }
    }

    private static string ResolveDefaultDataDirectory()
    {
        // 优先 D 盘用户目录
        var user = Environment.UserName;
        var d = Path.Combine("D:\\Users", user, "BaiYunBox");
        if (Directory.Exists("D:\\")) return d;

        // 无 D 盘回退 LocalAppData
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BaiYunBox");
    }

    /// <summary>确保数据相关目录存在。</summary>
    public static void EnsureDirectories()
    {
        foreach (var dir in new[] { DataDirectory, LogDirectory, ConfigDirectory, ModelDirectory, PodcastDirectory, TranscriptDirectory })
        {
            Directory.CreateDirectory(dir);
        }
    }
}
