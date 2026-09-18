using BaiYunBox.Models;

namespace BaiYunBox.Services;

/// <summary>本地媒体库：递归扫描视频文件，识别同名海报，断点续播由历史服务统一管理。</summary>
public sealed class MediaLibraryService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".rmvb", ".rm", ".ts", ".m2ts", ".m4v", ".mov",
        ".wmv", ".flv", ".webm", ".mpg", ".mpeg", ".3gp", ".vob", ".mts", ".iso",
    };

    private static readonly HashSet<string> PosterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp",
    };

    /// <summary>扫描一个目录，返回其中的视频条目（递归）。</summary>
    public List<MediaItem> ScanDirectory(string directory)
    {
        var result = new List<MediaItem>();
        if (!Directory.Exists(directory)) return result;

        try
        {
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (!VideoExtensions.Contains(ext)) continue;

                var fi = new FileInfo(file);
                result.Add(new MediaItem
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    Size = fi.Length,
                    Modified = fi.LastWriteTime,
                    PosterPath = FindPoster(file),
                });
            }
        }
        catch (Exception)
        {
            // 权限不足等，忽略该目录
        }
        return result;
    }

    private static string? FindPoster(string videoPath)
    {
        var dir = Path.GetDirectoryName(videoPath);
        if (dir == null) return null;
        var baseName = Path.GetFileNameWithoutExtension(videoPath);

        foreach (var candidate in new[]
        {
            Path.Combine(dir, baseName + ".jpg"),
            Path.Combine(dir, baseName + ".png"),
            Path.Combine(dir, baseName + ".webp"),
            Path.Combine(dir, "poster.jpg"),
            Path.Combine(dir, "folder.jpg"),
        })
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
