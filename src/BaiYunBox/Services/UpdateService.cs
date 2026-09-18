using System.Net.Http;
using System.Text.Json;
using BaiYunBox.Core;

namespace BaiYunBox.Services;

/// <summary>检查更新：查询 GitHub Releases 最新版本，对比本地版本号。</summary>
public sealed class UpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    public const string RepoUrl = "https://github.com/bilibilibaiyun/BaiYunBox";

    public sealed class UpdateInfo
    {
        public string Version { get; init; } = "";
        public string Title { get; init; } = "";
        public string Notes { get; init; } = "";
        public string Url { get; init; } = "";
    }

    static UpdateService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("BaiYunBox-Updater");
    }

    /// <summary>检查更新。返回 null 表示已是最新。</summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var api = "https://api.github.com/repos/bilibilibaiyun/BaiYunBox/releases/latest";
            var json = await Http.GetStringAsync(api);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var title = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";

            var latest = tag.TrimStart('v', 'V');
            var current = AppPaths.AppVersion;

            if (string.IsNullOrEmpty(latest) || !IsNewer(latest, current))
                return null;

            return new UpdateInfo
            {
                Version = latest,
                Title = string.IsNullOrEmpty(title) ? $"v{latest}" : title,
                Notes = body,
                Url = string.IsNullOrEmpty(url) ? RepoUrl : url,
            };
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"检查更新失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>比较版本号字符串（X.Y.Z），latest 严格大于 current 返回 true。</summary>
    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(latest, out var l)) return false;
        if (!Version.TryParse(current, out var c)) return true; // 本地版本解析失败，视为有更新
        return l > c;
    }
}
