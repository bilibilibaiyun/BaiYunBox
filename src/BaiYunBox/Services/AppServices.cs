using BaiYunBox.Core;

namespace BaiYunBox.Services;

/// <summary>全局服务容器（单例），供各 UI 页面共享。</summary>
public static class AppServices
{
    public static AppSettings Settings { get; private set; } = null!;
    public static AppSettingsStore SettingsStore { get; } = null!;

    public static VodService Vod { get; } = new();
    public static LiveService Live { get; } = new();
    public static MediaLibraryService Media { get; } = new();
    public static PodcastService Podcast { get; } = new();
    public static HistoryService History { get; } = new();

    static AppServices()
    {
        SettingsStore = new AppSettingsStore(AppPaths.ConfigDirectory);
        Settings = SettingsStore.Load();
    }

    public static void SaveSettings() => SettingsStore.Save(Settings);
}
