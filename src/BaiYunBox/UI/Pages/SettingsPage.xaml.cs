using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BaiYunBox.Services;

namespace BaiYunBox.UI.Pages;

public partial class SettingsPage : UserControl
{
    private bool _loaded;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        _loaded = false;
        var s = AppServices.Settings;

        VodSourceList.ItemsSource = null;
        VodSourceList.ItemsSource = s.VodSources;
        LiveSourceList.ItemsSource = null;
        LiveSourceList.ItemsSource = s.LiveSources;
        MediaDirList.ItemsSource = null;
        MediaDirList.ItemsSource = s.MediaDirectories;

        AutoNextBox.IsChecked = s.AutoNextEpisode;
        AutoSwitchBox.IsChecked = s.AutoSwitchSource;
        PreloadBox.IsChecked = s.PreloadNext;

        // 主题
        ThemeBox.SelectedIndex = s.Theme == "dark" ? 1 : 0;

        // 版本号
        VersionText.Text = $"当前版本：v{BaiYunBox.Core.AppPaths.AppVersion}";

        _loaded = true;
    }

    // ---------- 检查更新 ----------

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "检查中…";
        var updater = new UpdateService();
        var info = await updater.CheckAsync();

        if (info == null)
        {
            UpdateStatusText.Text = "已是最新版本。";
            return;
        }

        UpdateStatusText.Text = $"发现新版本 v{info.Version}";
        var result = MessageBox.Show(
            $"发现新版本：{info.Title}\n\n{TrimNotes(info.Notes)}\n\n是否前往下载？",
            "检查更新", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
        {
            OpenUrl(info.Url);
        }
    }

    private static string TrimNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "";
        if (notes.Length > 400) notes = notes[..400] + "…";
        return notes;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("打开链接失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------- 点播源 ----------

    private async void AddVodSource_Click(object sender, RoutedEventArgs e)
    {
        var url = VodSourceBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (AppServices.Settings.VodSources.Contains(url))
        {
            MessageBox.Show("该点播源已存在。", "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 先加载验证，成功才保存
        try
        {
            await AppServices.Vod.LoadSourceAsync(url);
            AppServices.Settings.VodSources.Add(url);
            AppServices.Settings.ActiveVodSource = AppServices.Settings.VodSources.Count - 1;
            AppServices.SaveSettings();
            VodSourceBox.Clear();
            VodSourceList.ItemsSource = null;
            VodSourceList.ItemsSource = AppServices.Settings.VodSources;
            MessageBox.Show($"点播源加载成功，共 {AppServices.Vod.Sites.Count} 个站点。",
                "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("加载点播源失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveVodSource_Click(object sender, RoutedEventArgs e)
    {
        if (VodSourceList.SelectedIndex >= 0)
        {
            AppServices.Settings.VodSources.RemoveAt(VodSourceList.SelectedIndex);
            AppServices.SaveSettings();
            VodSourceList.ItemsSource = null;
            VodSourceList.ItemsSource = AppServices.Settings.VodSources;
        }
    }

    private void VodSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || VodSourceList.SelectedIndex < 0) return;
        AppServices.Settings.ActiveVodSource = VodSourceList.SelectedIndex;
        AppServices.SaveSettings();
    }

    private async void ReloadVod_Click(object sender, RoutedEventArgs e)
    {
        if (AppServices.Settings.VodSources.Count == 0) return;
        try
        {
            var idx = AppServices.Settings.ActiveVodSource >= 0
                ? AppServices.Settings.ActiveVodSource : 0;
            await AppServices.Vod.LoadSourceAsync(AppServices.Settings.VodSources[idx]);
            MessageBox.Show("点播源已重新加载。", "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("加载失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---------- 直播源 ----------

    private async void AddLiveSource_Click(object sender, RoutedEventArgs e)
    {
        var url = LiveSourceBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (!AppServices.Settings.LiveSources.Contains(url))
        {
            AppServices.Settings.LiveSources.Add(url);
            LiveSourceBox.Clear();
            AppServices.SaveSettings();
            LiveSourceList.ItemsSource = null;
            LiveSourceList.ItemsSource = AppServices.Settings.LiveSources;

            // 立即验证并设为当前源
            try
            {
                var groups = await AppServices.Live.LoadAsync(url);
                AppServices.Settings.ActiveLiveSource = AppServices.Settings.LiveSources.Count - 1;
                AppServices.SaveSettings();
                MessageBox.Show($"直播源加载成功，共 {groups.Sum(g => g.Channels.Count)} 个频道。",
                    "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载直播源失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void RemoveLiveSource_Click(object sender, RoutedEventArgs e)
    {
        if (LiveSourceList.SelectedIndex >= 0)
        {
            AppServices.Settings.LiveSources.RemoveAt(LiveSourceList.SelectedIndex);
            AppServices.SaveSettings();
            LiveSourceList.ItemsSource = null;
            LiveSourceList.ItemsSource = AppServices.Settings.LiveSources;
        }
    }

    private void LiveSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || LiveSourceList.SelectedIndex < 0) return;
        AppServices.Settings.ActiveLiveSource = LiveSourceList.SelectedIndex;
        AppServices.SaveSettings();
    }

    // ---------- 媒体目录 ----------

    private void AddMediaDir_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择媒体目录",
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            if (!AppServices.Settings.MediaDirectories.Contains(dlg.SelectedPath))
            {
                AppServices.Settings.MediaDirectories.Add(dlg.SelectedPath);
                AppServices.SaveSettings();
                MediaDirList.ItemsSource = null;
                MediaDirList.ItemsSource = AppServices.Settings.MediaDirectories;
            }
        }
    }

    private void RemoveMediaDir_Click(object sender, RoutedEventArgs e)
    {
        if (MediaDirList.SelectedIndex >= 0)
        {
            AppServices.Settings.MediaDirectories.RemoveAt(MediaDirList.SelectedIndex);
            AppServices.SaveSettings();
            MediaDirList.ItemsSource = null;
            MediaDirList.ItemsSource = AppServices.Settings.MediaDirectories;
        }
    }

    // ---------- 播放设置 ----------

    private void PlaySetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        AppServices.Settings.AutoNextEpisode = AutoNextBox.IsChecked == true;
        AppServices.Settings.AutoSwitchSource = AutoSwitchBox.IsChecked == true;
        AppServices.Settings.PreloadNext = PreloadBox.IsChecked == true;
        AppServices.SaveSettings();
    }

    // ---------- 主题 ----------

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded || ThemeBox.SelectedItem is not ComboBoxItem item) return;
        var theme = (string)item.Tag;
        AppServices.Settings.Theme = theme;
        AppServices.SaveSettings();
        ApplyTheme(theme);
    }

    private static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (theme == "dark")
        {
            SetBrush(app, "WindowBackground", "#121417");
            SetBrush(app, "PanelBackground", "#1E2126");
            SetBrush(app, "ForegroundBrush", "#E8EAED");
            SetBrush(app, "SecondaryForegroundBrush", "#9AA0A6");
            SetBrush(app, "BorderBrushColor", "#30343A");
            SetBrush(app, "AccentBrush", "#3B82F6");
        }
        else
        {
            SetBrush(app, "WindowBackground", "#F5F6F8");
            SetBrush(app, "PanelBackground", "#FFFFFF");
            SetBrush(app, "ForegroundBrush", "#1B1F24");
            SetBrush(app, "SecondaryForegroundBrush", "#6B7280");
            SetBrush(app, "BorderBrushColor", "#E5E7EB");
            SetBrush(app, "AccentBrush", "#2563EB");
        }
    }

    private static void SetBrush(Application app, string key, string colorHex)
    {
        app.Resources[key] = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex));
    }
}
