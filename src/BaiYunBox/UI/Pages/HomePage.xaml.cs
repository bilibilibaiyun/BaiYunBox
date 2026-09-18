using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BaiYunBox.Models;
using BaiYunBox.Services;

namespace BaiYunBox.UI.Pages;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    public void Refresh()
    {
        HistoryList.ItemsSource = AppServices.History.GetRecent(50);
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchHistoryEntry entry)
        {
            PlayEntry(entry);
        }
    }

    private void HistoryList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is WatchHistoryEntry entry)
        {
            PlayEntry(entry);
        }
    }

    private void PlayEntry(WatchHistoryEntry entry)
    {
        var win = Window.GetWindow(this) as MainWindow;
        win?.Play(entry.Title, entry.Url, entry.Position, entry.Key);
    }
}
