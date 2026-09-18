using System.Runtime.InteropServices;
using BaiYunBox.Native;

namespace BaiYunBox.Services;

/// <summary>播放结束事件参数。</summary>
public sealed class PlaybackEndedEventArgs : EventArgs
{
    /// <summary>true 表示正常播完（EOF），false 表示出错/停止。</summary>
    public bool ReachedEnd { get; init; }
    public int Reason { get; init; }
    public int Error { get; init; }
}

/// <summary>
/// libmpv 高级封装：创建实例、渲染到指定 HWND、播放控制、后台事件循环。
/// mpv 命令/属性接口线程安全，事件经回调抛给订阅方。
/// </summary>
public sealed class MpvPlayer : IDisposable
{
    private IntPtr _handle = IntPtr.Zero;
    private Thread? _eventThread;
    private volatile bool _running;
    private bool _disposed;

    public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<double>? PositionChanged;
    public event EventHandler? Started;

    public bool IsInitialized => _handle != IntPtr.Zero;

    /// <summary>创建并初始化 mpv 实例，渲染到指定窗口句柄。</summary>
    public void Initialize(IntPtr hwnd)
    {
        if (IsInitialized) return;

        // 将 libmpv 所在目录加入 DLL 搜索路径（engine/mpv/ 在子目录，默认 DllImport 找不到）
        MpvApi.SetDllDirectory(Core.AppPaths.MpvDirectory);

        _handle = MpvApi.mpv_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("mpv_create 失败");

        try
        {
            // 渲染目标窗口（必须在 initialize 之前设置）
            MpvApi.mpv_set_option_string(_handle, "wid", hwnd.ToString());
            // 关闭 OSD 弹窗、无边框，交给 WPF 层绘制控件
            MpvApi.mpv_set_option_string(_handle, "keep-open", "no");
            MpvApi.mpv_set_option_string(_handle, "terminal", "no");
            MpvApi.mpv_set_option_string(_handle, "msg-level", "all=warn");
            // 优先硬解
            MpvApi.mpv_set_option_string(_handle, "hwdec", "auto-safe");

            if (MpvApi.mpv_initialize(_handle) < 0)
                throw new InvalidOperationException("mpv_initialize 失败");

            // 观察关键属性
            MpvApi.mpv_observe_property(_handle, 1, "time-pos", MpvFormat.Double);
            MpvApi.mpv_observe_property(_handle, 2, "duration", MpvFormat.Double);
            MpvApi.mpv_observe_property(_handle, 3, "pause", MpvFormat.Flag);

            StartEventLoop();
        }
        catch
        {
            MpvApi.mpv_terminate_destroy(_handle);
            _handle = IntPtr.Zero;
            throw;
        }
    }

    private void StartEventLoop()
    {
        _running = true;
        _eventThread = new Thread(EventLoop) { IsBackground = true, Name = "mpv-event" };
        _eventThread.Start();
    }

    private void EventLoop()
    {
        while (_running && _handle != IntPtr.Zero)
        {
            var evPtr = MpvApi.mpv_wait_event(_handle, 0.1);
            if (evPtr == IntPtr.Zero) continue;

            var ev = Marshal.PtrToStructure<MpvEvent>(evPtr);

            switch (ev.EventId)
            {
                case MpvEventId.EndFile:
                    var endFile = ev.Data != IntPtr.Zero
                        ? Marshal.PtrToStructure<MpvEventEndFile>(ev.Data)
                        : new MpvEventEndFile { Reason = (int)MpvEndFileReason.Error, Error = ev.Error };
                    var reachedEnd = endFile.Reason == (int)MpvEndFileReason.Eof;
                    PlaybackEnded?.Invoke(this, new PlaybackEndedEventArgs
                    {
                        ReachedEnd = reachedEnd,
                        Reason = endFile.Reason,
                        Error = endFile.Error,
                    });
                    break;

                case MpvEventId.PropertyChange:
                    HandlePropertyChange(ev);
                    break;

                case MpvEventId.FileLoaded:
                    Started?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    private void HandlePropertyChange(MpvEvent ev)
    {
        if (ev.Data == IntPtr.Zero) return;
        var prop = Marshal.PtrToStructure<MpvEventProperty>(ev.Data);

        // reply_userdata 1 = time-pos
        if (ev.ReplyUserdata == 1 && prop.Format == MpvFormat.Double && prop.Data != IntPtr.Zero)
        {
            var pos = Marshal.PtrToStructure<double>(prop.Data);
            PositionChanged?.Invoke(this, pos);
        }
    }

    // ---------- 播放控制 ----------

    public void LoadFile(string pathOrUrl, double startSeconds = 0)
    {
        EnsureReady();
        if (startSeconds > 0)
        {
            Command("loadfile", pathOrUrl, "replace", $"start={startSeconds:F2}");
        }
        else
        {
            Command("loadfile", pathOrUrl, "replace");
        }
    }

    public void Play()
    {
        EnsureReady();
        SetPropertyFlag("pause", false);
    }

    public void Pause()
    {
        EnsureReady();
        SetPropertyFlag("pause", true);
    }

    public void TogglePause()
    {
        EnsureReady();
        MpvApi.mpv_command_string(_handle, "cycle pause");
    }

    public void Stop()
    {
        EnsureReady();
        MpvApi.mpv_command_string(_handle, "stop");
    }

    public void Seek(double seconds)
    {
        EnsureReady();
        SetPropertyDouble("time-pos", seconds);
    }

    public void SetVolume(double volume0To100)
    {
        EnsureReady();
        SetPropertyDouble("volume", volume0To100);
    }

    public void SetSpeed(double rate)
    {
        EnsureReady();
        SetPropertyDouble("speed", rate);
    }

    public void SetMute(bool mute)
    {
        EnsureReady();
        SetPropertyFlag("mute", mute);
    }

    // ---------- 状态查询 ----------

    public double GetPosition()
    {
        if (!IsInitialized) return 0;
        var val = 0.0;
        if (MpvApi.mpv_get_property(_handle, "time-pos", MpvFormat.Double, ref val) >= 0) return val;
        return 0;
    }

    public double GetDuration()
    {
        if (!IsInitialized) return 0;
        var val = 0.0;
        if (MpvApi.mpv_get_property(_handle, "duration", MpvFormat.Double, ref val) >= 0) return val;
        return 0;
    }

    public bool IsPaused()
    {
        if (!IsInitialized) return true;
        var val = 0;
        if (MpvApi.mpv_get_property(_handle, "pause", MpvFormat.Flag, ref val) >= 0) return val != 0;
        return true;
    }

    public string? GetMediaTitle()
    {
        if (!IsInitialized) return null;
        var ptr = MpvApi.mpv_get_property_string(_handle, "media-title");
        if (ptr == IntPtr.Zero) return null;
        var s = Marshal.PtrToStringUTF8(ptr);
        MpvApi.mpv_free(ptr);
        return s;
    }

    // ---------- 私有 ----------

    private void Command(params string[] args)
    {
        // 构造以 null 结尾的 UTF-8 字符串指针数组
        var pointers = new IntPtr[args.Length + 1];
        var allocated = new List<IntPtr>();
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(args[i] + "\0");
                var p = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, p, bytes.Length);
                pointers[i] = p;
                allocated.Add(p);
            }
            pointers[args.Length] = IntPtr.Zero;

            var arrPtr = Marshal.AllocHGlobal(IntPtr.Size * pointers.Length);
            Marshal.Copy(pointers, 0, arrPtr, pointers.Length);

            MpvApi.mpv_command(_handle, arrPtr);

            Marshal.FreeHGlobal(arrPtr);
        }
        finally
        {
            foreach (var p in allocated) Marshal.FreeHGlobal(p);
        }
    }

    private void SetPropertyFlag(string name, bool value)
    {
        int v = value ? 1 : 0;
        MpvApi.mpv_set_property(_handle, name, MpvFormat.Flag, ref v);
    }

    private void SetPropertyDouble(string name, double value)
    {
        MpvApi.mpv_set_property(_handle, name, MpvFormat.Double, ref value);
    }

    private void EnsureReady()
    {
        if (!IsInitialized) throw new InvalidOperationException("播放器尚未初始化");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        _eventThread?.Join(500);
        if (_handle != IntPtr.Zero)
        {
            MpvApi.mpv_terminate_destroy(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
