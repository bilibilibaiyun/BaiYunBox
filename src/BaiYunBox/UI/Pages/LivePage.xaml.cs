using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BaiYunBox.Models;
using BaiYunBox.Services;

namespace BaiYunBox.UI.Pages;

public partial class LivePage : UserControl
{
    private List<LiveGroup> _groups = new();

    public LivePage()
    {
        InitializeComponent();
        Loaded += (_, _) => PopulateSources();
    }

    private void PopulateSources()
    {
        SourceBox.ItemsSource = null;
        SourceBox.ItemsSource = AppServices.Settings.LiveSources;
        if (SourceBox.Items.Count > 0)
        {
            SourceBox.SelectedIndex = AppServices.Settings.ActiveLiveSource >= 0 &&
                                      AppServices.Settings.ActiveLiveSource < SourceBox.Items.Count
                ? AppServices.Settings.ActiveLiveSource : 0;
        }
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        if (SourceBox.SelectedItem is not string source) return;
        StatusText.Text = "加载中…";
        try
        {
            _groups = await AppServices.Live.LoadAsync(source);
            GroupList.ItemsSource = _groups;
            ChannelList.ItemsSource = null;
            StatusText.Text = $"共 {_groups.Sum(g => g.Channels.Count)} 个频道";
            if (_groups.Count > 0) GroupList.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = "加载失败";
            MessageBox.Show("加载直播源失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Group_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupList.SelectedItem is LiveGroup group)
        {
            ChannelList.ItemsSource = group.Channels;
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is LiveChannel ch)
        {
            Play(ch);
        }
    }

    private void ChannelList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ChannelList.SelectedItem is LiveChannel ch)
        {
            Play(ch);
        }
    }

    private void Play(LiveChannel ch)
    {
        var win = Window.GetWindow(this) as MainWindow;
        win?.Play(ch.Name, ch.Url);
    }
}
