using System.Net.Http;
using System.Text.Json;
using BaiYunBox.Core;
using BaiYunBox.Crawler;
using BaiYunBox.Models;

namespace BaiYunBox.Services;

/// <summary>
/// 点播服务：加载 TVBox 源、按站点类型分流（CMS JSON / JS 爬虫）、播放线路解析。
/// 支持 TVBox 单线路源（顶层 sites）与 FongMi 多线路源（storeHouse → 子源）。
/// </summary>
public sealed class VodService
{
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly Dictionary<string, JsCrawler> _crawlers = new();

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
        LoadSourceFromJson(json, sourceUrl);
    }

    private void LoadSourceFromJson(string json, string? baseUrl = null)
    {
        var source = JsonSerializer.Deserialize<TvBoxSource>(json);
        if (source == null)
            throw new InvalidOperationException("源格式无法解析（不是有效的 TVBox JSON）");

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

        // 解析相对路径（js 源的 api/ext 常是 ./js/xxx.js）
        foreach (var s in sites)
        {
            s.Api = ResolveRelativeUrl(baseUrl, s.Api);
            if (!string.IsNullOrEmpty(s.Ext)) s.Ext = ResolveRelativeUrl(baseUrl, s.Ext);
        }

        // 桌面端支持：type=1 CMS + type=3 JS 爬虫（dr_py / FongMi js0 声明式）。
        // csp_ JAR（安卓 dex）与 type=0（xpath）不支持。
        var supported = sites.Where(s => s.Type == 1 || IsJsSite(s)).ToList();
        if (supported.Count == 0)
        {
            throw new InvalidOperationException(
                "该源不含可用的点播站点。\n\n桌面端支持两类站点：\n" +
                "· 苹果 CMS 采集源（type=1，api 形如 http://…/api.php/provide/vod/）\n" +
                "· JS 爬虫源（type=3，api 指向 .js 声明式爬虫）\n\n" +
                "源内若全是 csp_ JAR 爬虫（安卓专用，Windows 无法运行）或 xpath，则无法使用。");
        }

        Sites = supported;
        _crawlers.Clear();
    }

    private static bool IsJsSite(TvBoxSite s)
    {
        if (s.Type != 3) return false;
        if (s.Api.StartsWith("csp_", StringComparison.OrdinalIgnoreCase)) return false;
        var api = s.Api.ToLowerInvariant();
        var ext = s.Ext?.ToLowerInvariant() ?? "";
        return api.EndsWith(".js") || ext.EndsWith(".js");
    }

    private static string ResolveRelativeUrl(string? baseUrl, string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith("http://") || path.StartsWith("https://")) return path;
        if (string.IsNullOrEmpty(baseUrl)) return path;
        try
        {
            return new Uri(new Uri(baseUrl), path).ToString();
        }
        catch
        {
            return path;
        }
    }

    private async Task<JsCrawler> GetCrawlerAsync(TvBoxSite site)
    {
        var key = site.Key + "|" + site.LineName;
        if (_crawlers.TryGetValue(key, out var existing)) return existing;

        var crawler = new JsCrawler();
        // 优先 ext（dr_py 新式的爬虫脚本），否则 api
        var scriptUrl = !string.IsNullOrEmpty(site.Ext) && site.Ext.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            ? site.Ext : site.Api;
        await crawler.LoadFromUrlAsync(scriptUrl);
        _crawlers[key] = crawler;
        return crawler;
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
        if (site.Type == 3)
        {
            var crawler = await GetCrawlerAsync(site);
            var classes = await Task.Run(crawler.Classes);
            return classes.Select(c => new VodCategory { TypeId = c.TypeId, TypeName = c.TypeName }).ToList();
        }
        var url = BuildApi(site.Api, "class");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return resp?.Class ?? new List<VodCategory>();
    }

    /// <summary>获取分类下的点播列表。</summary>
    public async Task<(List<VodItem> items, int pageCount)> GetListAsync(TvBoxSite site, string typeId, int page = 1)
    {
        if (site.Type == 3)
        {
            var crawler = await GetCrawlerAsync(site);
            var vods = await Task.Run(() => crawler.Category(typeId, page));
            return (vods.Select(ToVodItem).ToList(), 1);
        }
        var url = BuildApi(site.Api, "videolist", $"t={Uri.EscapeDataString(typeId)}&pg={page}");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return (resp?.List ?? new List<VodItem>(), resp?.PageCount ?? 1);
    }

    /// <summary>全站搜索。</summary>
    public async Task<List<VodItem>> SearchAsync(TvBoxSite site, string keyword)
    {
        if (site.Type == 3)
        {
            var crawler = await GetCrawlerAsync(site);
            var vods = await Task.Run(() => crawler.Search(keyword));
            return vods.Select(ToVodItem).ToList();
        }
        var url = BuildApi(site.Api, "videolist", $"wd={Uri.EscapeDataString(keyword)}");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return resp?.List ?? new List<VodItem>();
    }

    /// <summary>获取详情（含剧集与线路）。</summary>
    public async Task<VodItem?> GetDetailAsync(TvBoxSite site, string vodId)
    {
        if (site.Type == 3)
        {
            var crawler = await GetCrawlerAsync(site);
            var detail = await Task.Run(() => crawler.Detail(vodId));
            return ToVodDetail(detail);
        }
        var url = BuildApi(site.Api, "videolist", $"ids={Uri.EscapeDataString(vodId)}");
        var resp = await GetJsonAsync<CmsListResponse>(url);
        return resp?.List?.FirstOrDefault();
    }

    /// <summary>解析播放地址（仅 js 爬虫源需要；CMS 源返回原地址）。</summary>
    public async Task<string> ResolvePlayUrlAsync(TvBoxSite site, string flag, string id)
    {
        if (site.Type != 3) return id;
        var crawler = await GetCrawlerAsync(site);
        return await Task.Run(() => crawler.Play(flag, id));
    }

    private static VodItem ToVodItem(CrawlerVod v) => new()
    {
        VodId = v.VodId,
        Name = v.VodName,
        Pic = v.VodPic,
        TypeName = v.TypeName,
        Remarks = v.VodRemarks,
        Content = v.VodContent,
    };

    private static VodItem ToVodDetail(CrawlerDetail d) => new()
    {
        VodId = d.VodId,
        Name = d.VodName,
        Pic = d.VodPic,
        TypeName = d.TypeName,
        Remarks = d.VodRemarks,
        Content = d.VodContent,
        Year = d.VodYear,
        Area = d.VodArea,
        PlayFrom = d.VodPlayFrom,
        PlayUrl = d.VodPlayUrl,
    };

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
