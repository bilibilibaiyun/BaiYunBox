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

        // 转录模型
        foreach (var m in TranscriptionService.AvailableModels)
        {
            ModelBox.Items.Add(new ComboBoxItem { Content = m.Desc, Tag = m.File });
        }

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

        // 模型选择
        for (int i = 0; i < ModelBox.Items.Count; i++)
        {
            if (ModelBox.Items[i] is ComboBoxItem item && (string)item.Tag == s.WhisperModel)
            {
                ModelBox.SelectedIndex = i;
                break;
            }
        }
        if (ModelBox.SelectedIndex < 0) ModelBox.SelectedIndex = 0;

        // 语言选择
        for (int i = 0; i < LangBox.Items.Count; i++)
        {
            if (LangBox.Items[i] is ComboBoxItem item && (string)item.Tag == s.TranscriptLanguage)
            {
                LangBox.SelectedIndex = i;
                break;
            }
        }
        if (LangBox.SelectedIndex < 0) LangBox.SelectedIndex = 0;

        // 主题
        ThemeBox.SelectedIndex = s.Theme == "dark" ? 1 : 0;

        UpdateModelStatus();
        _loaded = true;
    }

    private void UpdateModelStatus()
    {
        var model = AppServices.Settings.WhisperModel;
        var ready = AppServices.Transcription.IsModelReady(model);
        var engine = AppServices.Transcription.IsEngineReady();
        ModelStatusText.Text = engine
            ? (ready ? $"模型 {model} 已就绪。" : $"模型 {model} 未下载，转录时会自动下载。")
            : "转录引擎 whisper-cli.exe 缺失（安装包损坏？）。";
    }

    // ---------- 点播源 ----------

    private async void AddVodSource_Click(object sender, RoutedEventArgs e)
    {
        var url = VodSourceBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (!AppServices.Settings.VodSources.Contains(url))
        {
            AppServices.Settings.VodSources.Add(url);
            VodSourceBox.Clear();
            AppServices.SaveSettings();
            VodSourceList.ItemsSource = null;
            VodSourceList.ItemsSource = AppServices.Settings.VodSources;

            // 立即加载
            try
            {
                await AppServices.Vod.LoadSourceAsync(url);
                AppServices.Settings.ActiveVodSource = AppServices.Settings.VodSources.Count - 1;
                AppServices.SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载点播源失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

    private void AddLiveSource_Click(object sender, RoutedEventArgs e)
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

    // ---------- 转录设置 ----------

    private void TranscriptSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (ModelBox.SelectedItem is ComboBoxItem modelItem)
            AppServices.Settings.WhisperModel = (string)modelItem.Tag;
        if (LangBox.SelectedItem is ComboBoxItem langItem)
            AppServices.Settings.TranscriptLanguage = (string)langItem.Tag;
        AppServices.SaveSettings();
        UpdateModelStatus();
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
