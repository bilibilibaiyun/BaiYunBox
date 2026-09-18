using BaiYunBox.Models;

namespace BaiYunBox.Core;

/// <summary>应用设置模型。</summary>
public sealed class AppSettings
{
    /// <summary>点播源地址列表（TVBox 单线路源 / FongMi 多线路源 JSON URL）。</summary>
    public List<string> VodSources { get; set; } = new();

    /// <summary>直播源地址列表（M3U / M3U8 / TXT URL 或本地文件路径）。</summary>
    public List<string> LiveSources { get; set; } = new();

    /// <summary>本地媒体库目录列表。</summary>
    public List<string> MediaDirectories { get; set; } = new();

    /// <summary>当前选中的点播源索引。</summary>
    public int ActiveVodSource { get; set; } = -1;

    /// <summary>当前选中的直播源索引。</summary>
    public int ActiveLiveSource { get; set; } = -1;

    /// <summary>自动切集（播完自动下一集）。</summary>
    public bool AutoNextEpisode { get; set; }

    /// <summary>自动换源（播放失败/持续缓冲时按线路切换）。</summary>
    public bool AutoSwitchSource { get; set; }

    /// <summary>下一集预载。</summary>
    public bool PreloadNext { get; set; }

    /// <summary>界面语言 zh-CN / en-US。</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>主题 light / dark。</summary>
    public string Theme { get; set; } = "light";

    /// <summary>已订阅的播客源列表。</summary>
    public List<PodcastSubscription> PodcastSubscriptions { get; set; } = new();
}
