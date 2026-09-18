using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using BaiYunBox.Models;

namespace BaiYunBox.Services;

/// <summary>直播源解析：M3U / M3U8 / TXT（支持 URL 或本地文件路径）。</summary>
public sealed class LiveService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };

    /// <summary>加载并解析直播源，返回按分组聚合的结果。</summary>
    public async Task<List<LiveGroup>> LoadAsync(string source)
    {
        string content;
        if (IsUrl(source))
        {
            content = await Http.GetStringAsync(source);
        }
        else
        {
            content = await File.ReadAllTextAsync(source, Encoding.UTF8);
        }

        return Parse(content);
    }

    public static bool IsUrl(string s) =>
        s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public static List<LiveGroup> Parse(string content)
    {
        var channels = content.Contains("#EXTINF")
            ? ParseM3u(content)
            : ParseTxt(content);

        return channels
            .GroupBy(c => c.Group)
            .Select(g => new LiveGroup { Name = g.Key, Channels = g.ToList() })
            .ToList();
    }

    private static List<LiveChannel> ParseM3u(string content)
    {
        var result = new List<LiveChannel>();
        var lines = content.Split('\n');

        string? currentName = null;
        string? currentLogo = null;
        string? currentGroup = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                currentName = ExtractAttr(line, "tvg-name") ?? ExtractNameAfterComma(line) ?? "未知频道";
                currentLogo = ExtractAttr(line, "tvg-logo") ?? "";
                currentGroup = ExtractAttr(line, "group-title") ?? "默认分组";
            }
            else if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
            {
                if (currentName != null)
                {
                    result.Add(new LiveChannel
                    {
                        Name = currentName,
                        Url = line,
                        Group = currentGroup ?? "默认分组",
                        Logo = currentLogo ?? "",
                    });
                }
                currentName = null;
                currentLogo = null;
                currentGroup = null;
            }
        }
        return result;
    }

    private static List<LiveChannel> ParseTxt(string content)
    {
        var result = new List<LiveChannel>();
        string currentGroup = "默认分组";

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 分组行：`分组名,#genre#`
            if (line.EndsWith("#genre#", StringComparison.OrdinalIgnoreCase))
            {
                currentGroup = line[..line.LastIndexOf(',')].Trim();
                continue;
            }

            // 频道行：`频道名,url`
            var idx = line.IndexOf(',');
            if (idx <= 0) continue;

            var name = line[..idx].Trim();
            var url = line[(idx + 1)..].Trim();
            if (string.IsNullOrEmpty(name) || !IsUrl(url)) continue;

            result.Add(new LiveChannel { Name = name, Url = url, Group = currentGroup });
        }
        return result;
    }

    private static string? ExtractAttr(string line, string attr)
    {
        var m = Regex.Match(line, $"{attr}=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? ExtractNameAfterComma(string line)
    {
        var idx = line.LastIndexOf(',');
        return idx >= 0 && idx < line.Length - 1 ? line[(idx + 1)..].Trim() : null;
    }
}
