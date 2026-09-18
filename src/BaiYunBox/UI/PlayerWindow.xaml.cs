using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BaiYunBox.Services;

namespace BaiYunBox.UI;

public partial class PlayerWindow : Window
{
    private readonly string _url;
    private readonly double _startPos;
    private readonly string? _historyKey;
    private readonly string _title;
    private readonly HistoryService _history;

    private DispatcherTimer? _timer;
    private bool _seeking;
    private bool _ready;

    /// <summary>播放结束事件（ReachedEnd=true 表示正常播完，用于自动切集）。</summary>
    public event EventHandler<PlaybackEndedEventArgs>? Finished;

    public PlayerWindow(string title, string url, double startPos = 0, string? historyKey = null)
    {
        InitializeComponent();

        _title = title;
        _url = url;
        _startPos = startPos;
        _historyKey = historyKey;
        _history = new HistoryService();

        TitleText.Text = title;
        Title = $"{title} - BaiYun Box";

        // 倍速选项
        SpeedBox.ItemsSource = new[] { "0.5×", "0.75×", "1.0×", "1.25×", "1.5×", "2.0×" };
        SpeedBox.SelectedIndex = 2;

        MpvHostControl.ChildCreated += (_, hwnd) =>
        {
            Dispatcher.Invoke(() =>
            {
                InitializePlayer(hwnd);
            });
        };

        Loaded += (_, _) => StartProgressTimer();
        Closed += (_, _) => Cleanup();
    }

    private void InitializePlayer(IntPtr hwnd)
    {
        try
        {
            var player = MpvHostControl.Player;
            player.Initialize(hwnd);
            player.SetVolume(VolumeSlider.Value);
            player.SetSpeed(1.0);
            player.PlaybackEnded += OnPlaybackEnded;

            player.LoadFile(_url, _startPos);
            _ready = true;
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = "播放器初始化失败：" + ex.Message;
            Core.AppLogger.Error("播放器初始化失败", ex);
        }
    }

    private void OnPlaybackEnded(object? sender, PlaybackEndedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Finished?.Invoke(this, e);
        });
    }

    private void StartProgressTimer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => UpdateProgress();
        _timer.Start();
    }

    private void UpdateProgress()
    {
        if (!_ready || _seeking) return;
        var player = MpvHostControl.Player;
        var pos = player.GetPosition();
        var dur = player.GetDuration();

        if (dur > 0)
        {
            ProgressSlider.Maximum = dur;
            ProgressSlider.Value = pos;
            PositionText.Text = FormatTime(pos);
            DurationText.Text = FormatTime(dur);
        }

        // 断点续播：定时记录进度
        if (_historyKey != null && dur > 0 && pos > 5)
        {
            _history.Update(_historyKey, _title, "player", _url, pos, dur);
        }
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var player = MpvHostControl.Player;
        player.TogglePause();
        PlayPauseBtn.Content = player.IsPaused() ? "▶" : "⏸";
    }

    private void Progress_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _seeking = true;
    }

    private void Progress_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_ready)
        {
            MpvHostControl.Player.Seek(ProgressSlider.Value);
        }
        _seeking = false;
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_ready) MpvHostControl.Player.SetVolume(VolumeSlider.Value);
    }

    private void Speed_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready || SpeedBox.SelectedIndex < 0) return;
        var rates = new[] { 0.5, 0.75, 1.0, 1.25, 1.5, 2.0 };
        MpvHostControl.Player.SetSpeed(rates[SpeedBox.SelectedIndex]);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                PlayPause_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                break;
            case Key.Left:
                if (_ready) MpvHostControl.Player.Seek(MpvHostControl.Player.GetPosition() - 10);
                break;
            case Key.Right:
                if (_ready) MpvHostControl.Player.Seek(MpvHostControl.Player.GetPosition() + 10);
                break;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Cleanup()
    {
        _timer?.Stop();
        try
        {
            MpvHostControl.Player.Dispose();
        }
        catch
        {
            // 忽略
        }
    }
}
