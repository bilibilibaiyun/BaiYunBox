using System.Windows;
using System.Windows.Controls;
using BaiYunBox.Services;
using BaiYunBox.UI;

namespace BaiYunBox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavList.SelectedIndex = 0;
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item) return;
        var tag = item.Tag as string ?? "0";

        VodPageControl.Visibility = tag == "0" ? Visibility.Visible : Visibility.Collapsed;
        LivePageControl.Visibility = tag == "1" ? Visibility.Visible : Visibility.Collapsed;
        LibraryPageControl.Visibility = tag == "2" ? Visibility.Visible : Visibility.Collapsed;
        PodcastPageControl.Visibility = tag == "3" ? Visibility.Visible : Visibility.Collapsed;
        HomePageControl.Visibility = tag == "4" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPageControl.Visibility = tag == "5" ? Visibility.Visible : Visibility.Collapsed;

        // 切换到对应页时触发刷新
        switch (tag)
        {
            case "0": VodPageControl.RefreshIfNeeded(); break;
            case "1": LivePageControl.RefreshIfNeeded(); break;
            case "2": LibraryPageControl.RefreshIfNeeded(); break;
            case "3": PodcastPageControl.RefreshIfNeeded(); break;
            case "4": HomePageControl.Refresh(); break;
        }
    }

    /// <summary>打开播放器（供各页面调用）。</summary>
    public void Play(string title, string url, double startPos = 0, string? historyKey = null)
    {
        var win = new PlayerWindow(title, url, startPos, historyKey)
        {
            Owner = this,
        };
        win.Show();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        AppServices.SaveSettings();
    }
}
