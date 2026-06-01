using System.Windows;
using System.Windows.Input;

namespace FileFinder.Dialogs;

public partial class ModalDialog : Window
{
    private ModalDialog()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    /// <summary>Information / error popup with a single OK button.</summary>
    public static void Show(Window? owner, string title, string message)
    {
        var d = new ModalDialog { Owner = owner ?? Application.Current?.MainWindow };
        d.TitleText.Text = title;
        d.MessageText.Text = message;
        d.CancelButton.Visibility = Visibility.Collapsed;
        d.ShowDialog();
    }

    /// <summary>Confirmation popup. Returns true when the user clicks OK.</summary>
    public static bool Confirm(Window? owner, string title, string message, string okText = "OK")
    {
        var d = new ModalDialog { Owner = owner ?? Application.Current?.MainWindow };
        d.TitleText.Text = title;
        d.MessageText.Text = message;
        d.OkButton.Content = okText;
        d.CancelButton.Visibility = Visibility.Visible;
        return d.ShowDialog() == true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
