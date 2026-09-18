using System.Text.Json;

namespace BaiYunBox.Core;

/// <summary>配置读写（JSON）。损坏时写明确错误并重建默认，不静默清空用户数据。</summary>
public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public AppSettingsStore(string configDirectory)
    {
        _path = Path.Combine(configDirectory, "config.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            var fresh = new AppSettings();
            Save(fresh);
            return fresh;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return settings;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"配置文件损坏（{_path}），已重建默认配置。原文件保留为 .bak。", ex);
            TryBackupCorrupt();
            var fresh = new AppSettings();
            Save(fresh);
            return fresh;
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_path, json, System.Text.Encoding.UTF8);
    }

    private void TryBackupCorrupt()
    {
        try
        {
            var backup = _path + ".bak";
            if (File.Exists(_path))
            {
                File.Copy(_path, backup, overwrite: true);
            }
        }
        catch
        {
            // 备份失败忽略
        }
    }
}
