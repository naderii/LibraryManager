using System.Windows;

namespace LibraryManager.Views;

public partial class ReturnConfirmDialog : Window
{
    public ReturnConfirmDialog(string message)
    {
        InitializeComponent();
        Msg.Text = message;
    }
    private void Ok(object sender, RoutedEventArgs e) => DialogResult = true;
}
