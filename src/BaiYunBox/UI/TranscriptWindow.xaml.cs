using System.Windows;

namespace BaiYunBox.UI;

public partial class TranscriptWindow : Window
{
    public TranscriptWindow(string title, string text)
    {
        InitializeComponent();
        TitleText.Text = title;
        TextBox.Text = text;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TextBox.Text);
        }
        catch
        {
            // 忽略剪贴板异常
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "转录文本.txt",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, TextBox.Text, System.Text.Encoding.UTF8);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
