using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class UnpaidFinesView : UserControl
{
    private readonly LoanService _loanSvc = new();
    private readonly PaymentService _paySvc = new();
    private DataGrid _grid = null!;

    public UnpaidFinesView()
    {
        InitializeComponent();
        BuildUi();
        Refresh();
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && _grid.SelectedItem != null) { PaySelected(); e.Handled = true; }
            if (e.Key == Key.F5) { Refresh(); e.Handled = true; }
        };
    }

    private void BuildUi()
    {
        var page = VH.Page(Root, "جریمه‌های پرداخت‌نشده");
        page.Children.Add(new TextBlock
        {
            Text = "Enter پرداخت جریمه  ·  F5 تازه‌سازی",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(4, -4, 4, 10),
            FontSize = 12
        });

        var actions = new WrapPanel { Margin = new Thickness(4, 0, 4, 8) };
        actions.Children.Add(VH.Btn("پرداخت جریمه", (_, _) => PaySelected()));
        page.Children.Add(actions);

        _grid = new DataGrid
        {
            Height = 480, AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "عضو", Binding = new Binding("MemberName"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "کتاب", Binding = new Binding("BookTitle"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "تاریخ بازگشت", Binding = new Binding("ReturnDate") { StringFormat = "yyyy/MM/dd" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "مبلغ جریمه", Binding = new Binding("Fine"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) => PaySelected();
        page.Children.Add(_grid);
    }

    private void Refresh()
    {
        var list = _loanSvc.Unpaid().Select(l => new
        {
            LoanId = l.Id,
            MemberName = $"{l.Member?.LastName}، {l.Member?.FirstName}",
            BookTitle = l.Book?.Title,
            ReturnDate = l.ReturnDate,
            Fine = l.FineAmount.ToString("N0") + " تومان",
            Amount = l.FineAmount
        }).ToList();
        _grid.ItemsSource = list;
    }

    private void PaySelected()
    {
        if (_grid.SelectedItem == null) { MessageBox.Show("یک مورد را انتخاب کنید."); return; }
        dynamic row = _grid.SelectedItem;
        int id = row.LoanId;
        decimal amount = row.Amount;
        if (!PaymentDialog.ShowPayment("پرداخت جریمه", $"مبلغ: {amount:N0} تومان")) return;
        try
        {
            _paySvc.PayFine(id, PaymentDialog.Method);
            VH.Success("جریمه پرداخت شد.");
            Refresh();
        }
        catch (Exception ex) { VH.Error(ex); }
    }
}
