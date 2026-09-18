using System.Text.Json.Serialization;

namespace BaiYunBox.Models;

// ---------- 点播（TVBox / CMS）模型 ----------

/// <summary>TVBox 源（顶层）。</summary>
public sealed class TvBoxSource
{
    [JsonPropertyName("sites")]
    public List<TvBoxSite> Sites { get; set; } = new();

    [JsonPropertyName("storeHouse")]
    public List<StoreHouseItem>? StoreHouse { get; set; }
}

/// <summary>FongMi 多线路源里的单个线路条目。</summary>
public sealed class StoreHouseItem
{
    [JsonPropertyName("sourceName")]
    public string SourceName { get; set; } = "";

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = "";
}

/// <summary>TVBox 站点。</summary>
public sealed class TvBoxSite
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public int Type { get; set; } = 1;

    [JsonPropertyName("api")]
    public string Api { get; set; } = "";

    [JsonPropertyName("searchable")]
    public int Searchable { get; set; } = 1;

    [JsonPropertyName("playUrl")]
    public string? PlayUrl { get; set; }

    /// <summary>所属线路名（多线路源时填充）。</summary>
    [JsonIgnore]
    public string LineName { get; set; } = "";
}

/// <summary>CMS 分类。</summary>
public sealed class VodCategory
{
    [JsonPropertyName("type_id")]
    public string TypeId { get; set; } = "";

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; } = "";
}

/// <summary>点播条目（列表项）。</summary>
public sealed class VodItem
{
    [JsonPropertyName("vod_id")]
    public string VodId { get; set; } = "";

    [JsonPropertyName("vod_name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type_id")]
    public string TypeId { get; set; } = "";

    [JsonPropertyName("type_name")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("vod_pic")]
    public string Pic { get; set; } = "";

    [JsonPropertyName("vod_remarks")]
    public string Remarks { get; set; } = "";

    [JsonPropertyName("vod_year")]
    public string Year { get; set; } = "";

    [JsonPropertyName("vod_area")]
    public string Area { get; set; } = "";

    [JsonPropertyName("vod_actor")]
    public string Actor { get; set; } = "";

    [JsonPropertyName("vod_director")]
    public string Director { get; set; } = "";

    [JsonPropertyName("vod_content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("vod_play_from")]
    public string PlayFrom { get; set; } = "";

    [JsonPropertyName("vod_play_url")]
    public string PlayUrl { get; set; } = "";
}

/// <summary>单个剧集/播放地址。</summary>
public sealed class VodEpisode
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>一条播放线路（如「线路1」「备用线路」）。</summary>
public sealed class VodPlayLine
{
    public string Name { get; set; } = "";
    public List<VodEpisode> Episodes { get; set; } = new();
}

/// <summary>CMS 列表响应。</summary>
public sealed class CmsListResponse
{
    [JsonPropertyName("list")]
    public List<VodItem> List { get; set; } = new();

    [JsonPropertyName("class")]
    public List<VodCategory>? Class { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pagecount")]
    public int PageCount { get; set; }
}

// ---------- 直播（M3U / TXT）模型 ----------

public sealed class LiveChannel
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Group { get; set; } = "默认分组";
    public string Logo { get; set; } = "";
}

public sealed class LiveGroup
{
    public string Name { get; set; } = "";
    public List<LiveChannel> Channels { get; set; } = new();
}

// ---------- 播客模型 ----------

public sealed class PodcastShow
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string FeedUrl { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public List<PodcastEpisode> Episodes { get; set; } = new();
}

public sealed class PodcastEpisode
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string AudioUrl { get; set; } = "";
    public DateTime? Published { get; set; }
    public long? DurationSeconds { get; set; }
    /// <summary>本地下载路径（下载后填充）。</summary>
    public string? LocalPath { get; set; }
    /// <summary>转录文本（转录后填充）。</summary>
    public string? Transcript { get; set; }
    /// <summary>搜索结果的订阅 feed URL（搜索结果时填充，普通剧集为 null）。</summary>
    [JsonIgnore]
    public string? SubscribeFeedUrl { get; set; }

    [JsonIgnore]
    public string DurationDisplay
    {
        get
        {
            if (DurationSeconds is not long s || s <= 0) return "";
            var ts = TimeSpan.FromSeconds(s);
            return ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
        }
    }
}

/// <summary>已订阅的播客源。</summary>
public sealed class PodcastSubscription
{
    public string Title { get; set; } = "";
    public string FeedUrl { get; set; } = "";
    public DateTime SubscribedAt { get; set; } = DateTime.Now;
}

// ---------- 本地媒体模型 ----------

public sealed class MediaItem
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string? PosterPath { get; set; }
    public double? LastPosition { get; set; }
}

// ---------- 观看历史模型 ----------

public sealed class WatchHistoryEntry
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public double Position { get; set; }
    public double Duration { get; set; }
    public DateTime Updated { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string PositionDisplay => $"{Format(Position)} / {Format(Duration)}";

    private static string Format(double s)
    {
        if (s <= 0 || double.IsNaN(s)) return "00:00";
        var ts = TimeSpan.FromSeconds(s);
        return ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
    }
}
