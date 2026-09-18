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

    public VodPage()
    {
        InitializeComponent();
        Loaded += (_, _) => PopulateSites();
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

    private void Episode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VodEpisode ep)
        {
            PlayEpisode(ep);
        }
    }

    private void PlayEpisode(VodEpisode ep)
    {
        var title = $"{_currentDetail?.Name} {ep.Title}";
        var key = HistoryService.BuildKey(ep.Url);
        var pos = AppServices.History.GetPosition(key);
        var win = Window.GetWindow(this) as MainWindow;
        win?.Play(title, ep.Url, pos, key);
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
