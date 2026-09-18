using System.Text.Json;
using BaiYunBox.Core;
using BaiYunBox.Models;

namespace BaiYunBox.Services;

/// <summary>观看历史与断点续播：JSON 存储。</summary>
public sealed class HistoryService
{
    private readonly string _path = Path.Combine(AppPaths.ConfigDirectory, "history.json");
    private List<WatchHistoryEntry> _entries = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public HistoryService()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                _entries = JsonSerializer.Deserialize<List<WatchHistoryEntry>>(File.ReadAllText(_path)) ?? new();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"历史文件损坏，重建: {ex.Message}");
            _entries = new();
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, Options), System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"历史保存失败: {ex.Message}");
        }
    }

    /// <summary>记录/更新观看进度。</summary>
    public void Update(string key, string title, string source, string url, double position, double duration)
    {
        var entry = _entries.FirstOrDefault(e => e.Key == key);
        if (entry == null)
        {
            entry = new WatchHistoryEntry { Key = key };
            _entries.Insert(0, entry);
        }
        entry.Title = title;
        entry.Source = source;
        entry.Url = url;
        entry.Position = position;
        entry.Duration = duration;
        entry.Updated = DateTime.Now;

        // 播完（>95%）可保留在历史但标记位置，便于下次重播
        Save();
    }

    /// <summary>获取断点位置（秒）。</summary>
    public double GetPosition(string key)
    {
        var e = _entries.FirstOrDefault(x => x.Key == key);
        return e?.Position ?? 0;
    }

    public List<WatchHistoryEntry> GetRecent(int count = 50)
    {
        return _entries.OrderByDescending(e => e.Updated).Take(count).ToList();
    }

    public void Remove(string key)
    {
        _entries.RemoveAll(e => e.Key == key);
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    public static string BuildKey(string url) => url;
}
