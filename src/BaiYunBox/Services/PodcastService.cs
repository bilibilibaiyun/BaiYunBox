using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using BaiYunBox.Models;

namespace BaiYunBox.Services;

/// <summary>
/// 播客服务：RSS/Atom feed 解析、Apple Podcasts 搜索、剧集音频下载。
/// 小宇宙 / 喜马拉雅等平台通过其 RSS 地址直接导入。
/// </summary>
public sealed class PodcastService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("BaiYunBox/1.0 (podcast client)");
        c.Timeout = TimeSpan.FromSeconds(30);
        return c;
    }

    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    // ---------- 搜索（Apple Podcasts / iTunes Lookup API） ----------

    public async Task<List<PodcastShow>> SearchAppleAsync(string keyword, int limit = 20)
    {
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(keyword)}&media=podcast&entity=podcast&limit={limit}";
        var json = await Http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var results = new List<PodcastShow>();

        if (doc.RootElement.TryGetProperty("results", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                results.Add(new PodcastShow
                {
                    Title = GetStr(item, "collectionName"),
                    Author = GetStr(item, "artistName"),
                    FeedUrl = GetStr(item, "feedUrl"),
                    ImageUrl = GetStr(item, "artworkUrl600"),
                });
            }
        }
        return results;
    }

    private static string GetStr(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    // ---------- RSS / Atom 解析 ----------

    public async Task<PodcastShow> ParseFeedAsync(string feedUrl)
    {
        var xml = await Http.GetStringAsync(feedUrl);
        return ParseFeed(xml, feedUrl);
    }

    public static PodcastShow ParseFeed(string xml, string feedUrl)
    {
        var doc = XDocument.Parse(xml);
        var show = new PodcastShow { FeedUrl = feedUrl };

        if (doc.Root?.Name.LocalName == "feed")
        {
            // Atom
            show.Title = doc.Root.Element(Atom + "title")?.Value?.Trim() ?? "";
            show.Author = doc.Root.Element(Atom + "author")?.Element(Atom + "name")?.Value?.Trim() ?? "";
            show.ImageUrl = doc.Root.Element(Atom + "logo")?.Value?.Trim() ?? "";

            foreach (var entry in doc.Root.Elements(Atom + "entry"))
            {
                var ep = new PodcastEpisode
                {
                    Title = entry.Element(Atom + "title")?.Value?.Trim() ?? "未命名",
                    Description = entry.Element(Atom + "summary")?.Value?.Trim() ?? "",
                    AudioUrl = entry.Elements(Atom + "link")
                        .FirstOrDefault(l => (string?)l.Attribute("rel") == "enclosure")?.Attribute("href")?.Value?.Trim() ?? "",
                };
                show.Episodes.Add(ep);
            }
        }
        else
        {
            // RSS 2.0
            var channel = doc.Root?.Element("channel");
            if (channel == null) return show;

            show.Title = channel.Element("title")?.Value?.Trim() ?? "";
            show.Description = channel.Element("description")?.Value?.Trim() ?? "";
            show.ImageUrl = channel.Element("image")?.Element("url")?.Value?.Trim() ?? "";

            foreach (var item in channel.Elements("item"))
            {
                var ep = new PodcastEpisode
                {
                    Title = item.Element("title")?.Value?.Trim() ?? "未命名",
                    Description = item.Element("description")?.Value?.Trim() ?? "",
                    AudioUrl = item.Element("enclosure")?.Attribute("url")?.Value?.Trim() ?? "",
                    Published = ParseDate(item.Element("pubDate")?.Value),
                    DurationSeconds = ParseDuration(item.Element(Itunes + "duration")?.Value),
                };
                show.Episodes.Add(ep);
            }
        }
        return show;
    }

    private static DateTime? ParseDate(string? s)
    {
        if (DateTime.TryParse(s, out var d)) return d;
        return null;
    }

    private static long? ParseDuration(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        // HH:MM:SS 或 MM:SS 或纯秒
        var parts = s.Trim().Split(':');
        if (parts.Length == 3 && long.TryParse(parts[0], out var h) && long.TryParse(parts[1], out var m) && long.TryParse(parts[2], out var sec))
            return h * 3600 + m * 60 + sec;
        if (parts.Length == 2 && long.TryParse(parts[0], out var m2) && long.TryParse(parts[1], out var s2))
            return m2 * 60 + s2;
        if (long.TryParse(s, out var plain))
            return plain;
        return null;
    }

    // ---------- 下载 ----------

    public async Task<string> DownloadAsync(string url, string destDirectory, string fileName, IProgress<double>? progress = null)
    {
        Directory.CreateDirectory(destDirectory);
        var dest = Path.Combine(destDirectory, SanitizeFileName(fileName));
        dest = EnsureExtension(dest, url);

        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? 0;
        await using var src = await resp.Content.ReadAsStreamAsync();
        await using var dst = File.Create(dest);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }
        return dest;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "episode" : safe.Trim();
    }

    private static string EnsureExtension(string dest, string url)
    {
        if (!Path.HasExtension(dest))
        {
            var ext = Path.GetExtension(new Uri(url).AbsolutePath);
            if (!string.IsNullOrEmpty(ext)) dest += ext;
        }
        return dest;
    }
}
