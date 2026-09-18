using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BaiYunBox.Models;
using BaiYunBox.Services;

namespace BaiYunBox.UI.Pages;

public partial class LibraryPage : UserControl
{
    private bool _scanned;
    private List<MediaItem> _items = new();

    public LibraryPage()
    {
        InitializeComponent();
        Loaded += (_, _) => PopulateDirs();
    }

    private void PopulateDirs()
    {
        DirBox.ItemsSource = null;
        DirBox.ItemsSource = AppServices.Settings.MediaDirectories;
        if (DirBox.Items.Count > 0) DirBox.SelectedIndex = 0;
    }

    public void RefreshIfNeeded()
    {
        PopulateDirs();
        if (!_scanned && DirBox.Items.Count > 0)
        {
            Scan();
        }
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => Scan();

    private void Scan()
    {
        if (DirBox.SelectedItem is not string dir) return;
        StatusText.Text = "扫描中…";
        try
        {
            _items = AppServices.Media.ScanDirectory(dir);
            VideoList.ItemsSource = null;
            VideoList.ItemsSource = _items;
            _scanned = true;
            StatusText.Text = $"共 {_items.Count} 个视频";
        }
        catch (Exception ex)
        {
            StatusText.Text = "扫描失败";
            MessageBox.Show("扫描失败：" + ex.Message, "BaiYun Box", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void VideoList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VideoList.SelectedItem is MediaItem item)
        {
            Play(item);
        }
    }

    private void Play(MediaItem item)
    {
        var key = HistoryService.BuildKey(item.Path);
        var pos = AppServices.History.GetPosition(key);
        var win = Window.GetWindow(this) as MainWindow;
        win?.Play(item.Name, item.Path, pos, key);
    }
}
