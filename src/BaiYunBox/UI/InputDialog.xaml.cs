using System.Windows;

namespace BaiYunBox.UI;

public partial class InputDialog : Window
{
    public string Result { get; private set; } = "";

    public InputDialog(string prompt, string title = "输入", string defaultText = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultText;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    public static string? Show(Window? owner, string prompt, string title = "输入", string defaultText = "")
    {
        var dlg = new InputDialog(prompt, title, defaultText) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
