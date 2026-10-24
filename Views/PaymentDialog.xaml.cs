using System.Windows;
using System.Windows.Input;
using LibraryManager.Models;

namespace LibraryManager.Views;

public partial class PaymentDialog : Window
{
    public static PaymentMethod Method { get; private set; } = PaymentMethod.Cash;

    public PaymentDialog(string title, string amountInfo = "")
    {
        InitializeComponent();
        TitleText.Text = title;
        AmountText.Text = amountInfo;
        Loaded += (_, _) => CashRadio.Focus();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { CashRadio.IsChecked = true; e.Handled = true; }
        else if (e.Key == Key.F2) { CardRadio.IsChecked = true; e.Handled = true; }
        else if (e.Key == Key.Escape) { DialogResult = false; }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Method = CashRadio.IsChecked == true ? PaymentMethod.Cash : PaymentMethod.Card;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// نمایش دیالوگ پرداخت. true اگر کاربر تأیید کرد.
    /// </summary>
    public static bool ShowPayment(string title, string amountInfo = "")
    {
        var dlg = new PaymentDialog(title, amountInfo) { Owner = Application.Current.MainWindow };
        return dlg.ShowDialog() == true;
    }
}
