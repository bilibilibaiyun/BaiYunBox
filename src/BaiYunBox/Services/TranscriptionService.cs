using System.Diagnostics;
using System.Net.Http;
using System.Text;
using BaiYunBox.Core;

namespace BaiYunBox.Services;

/// <summary>
/// 本地转录服务：调用内置 whisper.cpp（whisper-cli.exe），模型按需从 HuggingFace 下载。
/// 完全离线运行，音频与文本不出本机。
/// </summary>
public sealed class TranscriptionService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(30),
    };

    /// <summary>可用模型（文件名 → 描述 + 下载大小）。</summary>
    public static readonly (string File, string Desc, long SizeMb)[] AvailableModels =
    {
        ("ggml-base.bin", "base（约 142 MB，快）", 142),
        ("ggml-small.bin", "small（约 466 MB，更准）", 466),
        ("ggml-medium.bin", "medium（约 1.5 GB，高精度）", 1536),
    };

    private const string ModelBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    public bool IsEngineReady() => File.Exists(AppPaths.WhisperCliPath);

    public string ModelPath(string modelFile) => Path.Combine(AppPaths.ModelDirectory, modelFile);

    public bool IsModelReady(string modelFile) => File.Exists(ModelPath(modelFile));

    /// <summary>确保模型已下载，缺失则下载。</summary>
    public async Task EnsureModelAsync(string modelFile, IProgress<double>? progress = null)
    {
        if (IsModelReady(modelFile)) return;

        Directory.CreateDirectory(AppPaths.ModelDirectory);
        var url = ModelBaseUrl + modelFile;
        var dest = ModelPath(modelFile) + ".part";

        AppLogger.Info($"下载转录模型 {modelFile} ...");
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

        File.Move(dest, ModelPath(modelFile), overwrite: true);
        AppLogger.Info($"转录模型 {modelFile} 下载完成。");
    }

    /// <summary>转录音频文件，返回文本。language 传 auto 自动检测。</summary>
    public async Task<string> TranscribeAsync(string audioPath, string modelFile, string language = "auto")
    {
        if (!IsEngineReady())
            throw new InvalidOperationException("转录引擎 whisper-cli.exe 未找到");

        if (!IsModelReady(modelFile))
            throw new InvalidOperationException($"转录模型 {modelFile} 未下载");

        Directory.CreateDirectory(AppPaths.TranscriptDirectory);

        // 预处理：whisper-cli 仅支持 flac/mp3/ogg/wav，其他格式（m4a/aac 等）先用 ffmpeg 转 16kHz wav
        var inputPath = audioPath;
        string? tempWav = null;
        var ext = Path.GetExtension(audioPath).ToLowerInvariant();
        if (ext is not ".flac" and not ".mp3" and not ".ogg" and not ".wav")
        {
            tempWav = Path.Combine(AppPaths.TranscriptDirectory, $"pre_{Guid.NewGuid():N}.wav");
            await ConvertToWavAsync(audioPath, tempWav);
            inputPath = tempWav;
        }

        // 输出基名（whisper-cli 会追加 .txt）
        var outputBase = Path.Combine(AppPaths.TranscriptDirectory, $"transcript_{DateTime.Now:yyyyMMddHHmmssfff}");
        var outputTxt = outputBase + ".txt";

        var psi = new ProcessStartInfo
        {
            FileName = AppPaths.WhisperCliPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add(ModelPath(modelFile));
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(inputPath);
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(language);
        psi.ArgumentList.Add("-otxt");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add(outputBase);
        psi.ArgumentList.Add("-nt"); // 无时间戳（纯文本）

        AppLogger.Info($"开始转录 {audioPath}（模型 {modelFile}）...");
        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
                throw new InvalidOperationException("无法启动 whisper-cli.exe");

            var stderrTask = proc.StandardError.ReadToEndAsync();
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            await Task.WhenAll(stderrTask, stdoutTask);

            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                AppLogger.Error($"转录失败（exit {proc.ExitCode}）: {stderr}");
                throw new InvalidOperationException($"转录失败：{stderr.Trim()}");
            }

            if (File.Exists(outputTxt))
            {
                var text = await File.ReadAllTextAsync(outputTxt, Encoding.UTF8);
                AppLogger.Info($"转录完成，文本长度 {text.Length}。");
                return text.Trim();
            }

            // 回退：从 stdout 取
            var stdout = await stdoutTask;
            return stdout.Trim();
        }
        finally
        {
            TryDelete(outputTxt);
            TryDelete(outputBase + ".srt");
            if (tempWav != null) TryDelete(tempWav);
        }
    }

    /// <summary>用 ffmpeg 将任意音频转为 16kHz 单声道 PCM WAV。</summary>
    private async Task ConvertToWavAsync(string input, string output)
    {
        if (!File.Exists(AppPaths.FfmpegPath))
            throw new InvalidOperationException("ffmpeg 引擎缺失，无法转换音频格式");

        var psi = new ProcessStartInfo
        {
            FileName = AppPaths.FfmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(input);
        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("16000");
        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("pcm_s16le");
        psi.ArgumentList.Add(output);

        using var proc = Process.Start(psi);
        if (proc == null)
            throw new InvalidOperationException("无法启动 ffmpeg.exe");

        var errTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var err = await errTask;

        if (proc.ExitCode != 0 || !File.Exists(output))
        {
            AppLogger.Error($"ffmpeg 转换失败（exit {proc.ExitCode}）: {err}");
            throw new InvalidOperationException($"音频格式转换失败：{err.Trim()}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 忽略
        }
    }
}
