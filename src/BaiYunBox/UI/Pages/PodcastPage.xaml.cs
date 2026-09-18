using System.Windows;
using System.Windows.Controls;
using BaiYunBox.Models;
using BaiYunBox.Services;
using BaiYunBox.UI;

namespace BaiYunBox.UI.Pages;

public partial class PodcastPage : UserControl
{
    private PodcastShow? _currentShow;

    public PodcastPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshIfNeeded();
    }

    public void RefreshIfNeeded()
    {
        SubList.ItemsSource = null;
        SubList.ItemsSource = AppServices.Settings.PodcastSubscriptions;
    }

    // ---------- 订阅 ----------

    private async void ImportRss_Click(object sender, RoutedEventArgs e)
    {
        var url = RssBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var show = await AppServices.Podcast.ParseFeedAsync(url);
            AddSubscription(show);
            RssBox.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show("导入订阅失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddSubscription(PodcastShow show)
    {
        var existing = AppServices.Settings.PodcastSubscriptions.FirstOrDefault(s => s.FeedUrl == show.FeedUrl);
        if (existing == null)
        {
            AppServices.Settings.PodcastSubscriptions.Add(new PodcastSubscription
            {
                Title = show.Title,
                FeedUrl = show.FeedUrl,
            });
            AppServices.SaveSettings();
        }
        RefreshIfNeeded();
        _currentShow = show;
        ShowEpisodes(show);
    }

    private async void Sub_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SubList.SelectedItem is not PodcastSubscription sub) return;
        try
        {
            ListTitle.Text = sub.Title;
            _currentShow = await AppServices.Podcast.ParseFeedAsync(sub.FeedUrl);
            EpisodeItems.ItemsSource = null;
            EpisodeItems.ItemsSource = _currentShow.Episodes;
        }
        catch (Exception ex)
        {
            MessageBox.Show("加载剧集失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowEpisodes(PodcastShow show)
    {
        ListTitle.Text = show.Title;
        EpisodeItems.ItemsSource = null;
        EpisodeItems.ItemsSource = show.Episodes;
    }

    private void RemoveSub_Click(object sender, RoutedEventArgs e)
    {
        if (SubList.SelectedItem is not PodcastSubscription sub) return;
        AppServices.Settings.PodcastSubscriptions.RemoveAll(s => s.FeedUrl == sub.FeedUrl);
        AppServices.SaveSettings();
        RefreshIfNeeded();
        EpisodeItems.ItemsSource = null;
        ListTitle.Text = "请选择订阅或搜索节目";
    }

    // ---------- 搜索 ----------

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        var keyword = RssBox.Text.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            keyword = InputDialog.Show(Window.GetWindow(this), "输入要搜索的节目关键词：", "搜索播客") ?? "";
        }
        if (string.IsNullOrEmpty(keyword)) return;

        try
        {
            ListTitle.Text = $"搜索「{keyword}」…";
            var shows = await AppServices.Podcast.SearchAppleAsync(keyword);
            if (shows.Count == 0)
            {
                MessageBox.Show("未找到相关节目。", "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 结果转成 PodcastEpisode 显示（带 SubscribeFeedUrl 供订阅）
            var results = shows.Select(s => new PodcastEpisode
            {
                Title = $"{s.Title}（{s.Author}）",
                Description = s.FeedUrl,
                SubscribeFeedUrl = s.FeedUrl,
            }).ToList();

            ListTitle.Text = $"搜索结果（{shows.Count}）";
            EpisodeItems.ItemsSource = null;
            EpisodeItems.ItemsSource = results;
        }
        catch (Exception ex)
        {
            MessageBox.Show("搜索失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SubscribeResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PodcastEpisode ep && ep.SubscribeFeedUrl != null)
        {
            try
            {
                var show = await AppServices.Podcast.ParseFeedAsync(ep.SubscribeFeedUrl);
                AddSubscription(show);
                MessageBox.Show($"已订阅「{show.Title}」。", "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("订阅失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // ---------- 播放 / 下载 / 转录 ----------

    private void PlayEpisode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PodcastEpisode ep)
        {
            var url = ep.LocalPath ?? ep.AudioUrl;
            if (string.IsNullOrEmpty(url)) return;
            var win = Window.GetWindow(this) as MainWindow;
            win?.Play(ep.Title, url);
        }
    }

    private async void DownloadEpisode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PodcastEpisode ep) return;
        if (string.IsNullOrEmpty(ep.AudioUrl)) return;

        var btnText = btn.Content;
        btn.Content = "下载中…";
        btn.IsEnabled = false;
        try
        {
            var dir = BaiYunBox.Core.AppPaths.PodcastDirectory;
            var progress = new Progress<double>(p => { });
            var path = await AppServices.Podcast.DownloadAsync(ep.AudioUrl, dir, ep.Title, progress);
            ep.LocalPath = path;
            MessageBox.Show($"已下载到：\n{path}", "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("下载失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            btn.Content = btnText;
            btn.IsEnabled = true;
        }
    }
}
