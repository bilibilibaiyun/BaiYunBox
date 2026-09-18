using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BaiYunBox.Models;
using BaiYunBox.Services;

namespace BaiYunBox.UI.Pages;

public partial class VodPage : UserControl
{
    private TvBoxSite? _currentSite;
    private string _currentTypeId = "";
    private int _currentPage = 1;
    private int _pageCount = 1;
    private bool _searchMode;
    private VodItem? _currentDetail;
    private List<VodPlayLine> _lines = new();
    private List<VodEpisode> _currentEpisodes = new();

    private int _loadedSiteCount = -1;

    public VodPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshIfNeeded();
    }

    /// <summary>切到点播页时刷新站点列表，站点变化时自动加载第一个站点。</summary>
    public void RefreshIfNeeded()
    {
        var count = AppServices.Vod.Sites.Count;
        if (count != _loadedSiteCount || SiteBox.Items.Count == 0)
        {
            PopulateSites();
            _loadedSiteCount = count;
        }
    }

    private void PopulateSites()
    {
        SiteBox.ItemsSource = null;
        SiteBox.ItemsSource = AppServices.Vod.Sites;
        if (SiteBox.Items.Count > 0) SiteBox.SelectedIndex = 0;
    }

    private async void Site_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SiteBox.SelectedItem is not TvBoxSite site) return;
        _currentSite = site;
        _searchMode = false;
        SearchBox.Clear();
        try
        {
            var cats = await AppServices.Vod.GetCategoriesAsync(site);
            CategoryList.ItemsSource = cats;
            if (cats.Count > 0) CategoryList.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("加载分类失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is not VodCategory cat) return;
        if (_searchMode) return;
        _currentTypeId = cat.TypeId;
        _currentPage = 1;
        await LoadList();
    }

    private async Task LoadList()
    {
        if (_currentSite == null) return;
        try
        {
            var (items, pageCount) = await AppServices.Vod.GetListAsync(_currentSite, _currentTypeId, _currentPage);
            _pageCount = Math.Max(1, pageCount);
            PageText.Text = $"{_currentPage} / {_pageCount}";
            VodList.ItemsSource = null;
            VodList.ItemsSource = items;
        }
        catch (Exception ex)
        {
            MessageBox.Show("加载列表失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await DoSearch();

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await DoSearch();
    }

    private async Task DoSearch()
    {
        var kw = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(kw) || _currentSite == null) return;
        try
        {
            _searchMode = true;
            var items = await AppServices.Vod.SearchAsync(_currentSite, kw);
            VodList.ItemsSource = null;
            VodList.ItemsSource = items;
            PageText.Text = $"搜索：{kw}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("搜索失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void VodList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VodList.SelectedItem is VodItem item)
        {
            await OpenDetail(item);
        }
    }

    private async Task OpenDetail(VodItem item)
    {
        if (_currentSite == null) return;
        try
        {
            var detail = await AppServices.Vod.GetDetailAsync(_currentSite, item.VodId);
            if (detail == null)
            {
                MessageBox.Show("获取详情失败。", "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _currentDetail = detail;
            DetailTitle.Text = detail.Name;
            DetailInfo.Text = BuildInfo(detail);

            _lines = VodService.ParsePlayLines(detail.PlayFrom, detail.PlayUrl);
            LineBox.ItemsSource = null;
            LineBox.ItemsSource = _lines.Select(l => l.Name).ToList();
            if (_lines.Count > 0) LineBox.SelectedIndex = 0;

            BrowseView.Visibility = Visibility.Collapsed;
            DetailView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show("获取详情失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string BuildInfo(VodItem detail)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(detail.Year)) parts.Add(detail.Year);
        if (!string.IsNullOrWhiteSpace(detail.Area)) parts.Add(detail.Area);
        if (!string.IsNullOrWhiteSpace(detail.TypeName)) parts.Add(detail.TypeName);
        if (!string.IsNullOrWhiteSpace(detail.Remarks)) parts.Add(detail.Remarks);
        var head = parts.Count > 0 ? string.Join(" · ", parts) + "\n\n" : "";
        var content = detail.Content ?? "";
        if (content.Length > 500) content = content[..500] + "…";
        return head + content;
    }

    private void Line_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LineBox.SelectedIndex >= 0 && LineBox.SelectedIndex < _lines.Count)
        {
            _currentEpisodes = _lines[LineBox.SelectedIndex].Episodes;
            EpisodeList.ItemsSource = null;
            EpisodeList.ItemsSource = _currentEpisodes;
        }
    }

    private async void Episode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VodEpisode ep)
        {
            await PlayEpisodeAsync(ep);
        }
    }

    private async Task PlayEpisodeAsync(VodEpisode ep)
    {
        var title = $"{_currentDetail?.Name} {ep.Title}";
        var url = ep.Url;

        // js 爬虫源：先解析播放地址（flag=线路名，id=播放标识）
        if (_currentSite is { Type: 3 })
        {
            try
            {
                var lineName = LineBox.SelectedIndex >= 0 && LineBox.SelectedIndex < _lines.Count
                    ? _lines[LineBox.SelectedIndex].Name : "";
                url = await AppServices.Vod.ResolvePlayUrlAsync(_currentSite, lineName, ep.Url);
                if (string.IsNullOrEmpty(url)) url = ep.Url;
            }
            catch (Exception ex)
            {
                MessageBox.Show("解析播放地址失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var key = HistoryService.BuildKey(url);
        var pos = AppServices.History.GetPosition(key);
        var win = Window.GetWindow(this) as MainWindow;
        win?.Play(title, url, pos, key);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        DetailView.Visibility = Visibility.Collapsed;
        BrowseView.Visibility = Visibility.Visible;
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_searchMode || _currentPage <= 1) return;
        _currentPage--;
        await LoadList();
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_searchMode || _currentPage >= _pageCount) return;
        _currentPage++;
        await LoadList();
    }
}
