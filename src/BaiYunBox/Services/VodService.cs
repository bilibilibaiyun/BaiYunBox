using System.Net.Http;
using System.Text.Json;
using BaiYunBox.Core;
using BaiYunBox.Models;

namespace BaiYunBox.Services;

/// <summary>
/// 点播服务：加载 TVBox 源、CMS JSON API 分类/列表/搜索/详情、播放线路解析。
/// 支持 TVBox 单线路源（顶层 sites）与 FongMi 多线路源（storeHouse → 子源）。
/// </summary>
public sealed class VodService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        c.Timeout = TimeSpan.FromSeconds(25);
        return c;
    }

    /// <summary>当前所有站点（展开多线路源后的扁平列表）。</summary>
    public List<TvBoxSite> Sites { get; private set; } = new();

    /// <summary>解析并加载一个点播源地址。</summary>
    public async Task LoadSourceAsync(string sourceUrl)
    {
        var json = await Http.GetStringAsync(sourceUrl);
        LoadSourceFromJson(json);
    }

    private void LoadSourceFromJson(string json)
    {
        var source = JsonSerializer.Deserialize<TvBoxSource>(json);
        if (source == null) return;

        var sites = new List<TvBoxSite>();

        if (source.Sites is { Count: > 0 })
        {
            sites.AddRange(source.Sites);
        }

        if (source.StoreHouse is { Count: > 0 })
        {
            // FongMi 多线路：每个 storeHouse 条目是一路，sourceUrl 指向子源
            foreach (var line in source.StoreHouse)
            {
                var subSites = TryLoadSubSource(line.SourceUrl).GetAwaiter().GetResult();
                foreach (var s in subSites)
                {
                    s.LineName = string.IsNullOrEmpty(line.SourceName) ? line.SourceUrl : line.SourceName;
                    sites.Add(s);
                }
            }
        }

        if (sites.Count > 0)
            Sites = sites;
    }

    private async Task<List<TvBoxSite>> TryLoadSubSource(string url)
    {
        try
        {
            var json = await Http.GetStringAsync(url);
            var sub = JsonSerializer.Deserialize<TvBoxSource>(json);
            if (sub?.Sites != null)
            {
                return sub.Sites;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"加载子源失败 {url}: {ex.Message}");
        }
        return new List<TvBoxSite>();
    }

    // ---------- CMS API ----------

    private static string BuildApi(string api, string action, string? param = null)
    {
        var sep = api.Contains('?') ? '&' : '?';
        var url = $"{api}{sep}ac={action}";
        if (!string.IsNullOrEmpty(param))
            url += $"&{param}";
        return url;
    }

    /// <summary>获取站点分类列表。</summary>
    public async Task<List<VodCategory>> GetCategoriesAsync(TvBoxSite site)
    {
        var url = BuildApi(site.Api, "class");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return resp?.Class ?? new List<VodCategory>();
    }

    /// <summary>获取分类下的点播列表。</summary>
    public async Task<(List<VodItem> items, int pageCount)> GetListAsync(TvBoxSite site, string typeId, int page = 1)
    {
        var url = BuildApi(site.Api, "videolist", $"t={Uri.EscapeDataString(typeId)}&pg={page}");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return (resp?.List ?? new List<VodItem>(), resp?.PageCount ?? 1);
    }

    /// <summary>全站搜索。</summary>
    public async Task<List<VodItem>> SearchAsync(TvBoxSite site, string keyword)
    {
        var url = BuildApi(site.Api, "videolist", $"wd={Uri.EscapeDataString(keyword)}");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return resp?.List ?? new List<VodItem>();
    }

    /// <summary>获取详情（含剧集与线路）。</summary>
    public async Task<VodItem?> GetDetailAsync(TvBoxSite site, string vodId)
    {
        var url = BuildApi(site.Api, "videolist", $"ids={Uri.EscapeDataString(vodId)}");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return resp?.List?.FirstOrDefault();
    }

    private async Task<T?> GetJsonAsync<T>(string url)
    {
        var json = await Http.GetStringAsync(url);
        return JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>解析播放线路：vod_play_from（线路名） + vod_play_url（剧集）。</summary>
    public static List<VodPlayLine> ParsePlayLines(string playFrom, string playUrl)
    {
        var result = new List<VodPlayLine>();
        if (string.IsNullOrWhiteSpace(playUrl)) return result;

        var fromParts = string.IsNullOrWhiteSpace(playFrom)
            ? new[] { "线路1" }
            : playFrom.Split(new[] { "$$$" }, StringSplitOptions.RemoveEmptyEntries);

        var urlParts = playUrl.Split(new[] { "$$$" }, StringSplitOptions.None);

        for (int i = 0; i < fromParts.Length; i++)
        {
            var line = new VodPlayLine { Name = fromParts[i].Trim() };
            if (i < urlParts.Length)
            {
                line.Episodes = ParseEpisodes(urlParts[i]);
            }
            result.Add(line);
        }
        return result;
    }

    private static List<VodEpisode> ParseEpisodes(string raw)
    {
        var episodes = new List<VodEpisode>();
        if (string.IsNullOrWhiteSpace(raw)) return episodes;

        var parts = raw.Split('#');
        int index = 0;
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            index++;
            var idx = part.IndexOf('$');
            if (idx > 0)
            {
                episodes.Add(new VodEpisode
                {
                    Title = part[..idx].Trim(),
                    Url = part[(idx + 1)..].Trim(),
                });
            }
            else
            {
                episodes.Add(new VodEpisode { Title = $"第{index}集", Url = part.Trim() });
            }
        }
        return episodes;
    }
}
