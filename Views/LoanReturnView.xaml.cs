using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class LoanReturnView : UserControl
{
    private readonly LoanService _svc = new();
    private DataGrid _grid = null!;
    private TextBox _search = null!;

    public LoanReturnView()
    {
        InitializeComponent();
        BuildUi();
        Refresh();
        Loaded += (_, _) => _search.Focus();
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.F5) { Refresh(); e.Handled = true; }
            if (e.Key == Key.Enter && _grid.SelectedItem != null) { ReturnSelected(); e.Handled = true; }
        };
    }

    private void BuildUi()
    {
        var page = VH.Page(Root, "بازگرداندن کتاب");
        page.Children.Add(new TextBlock
        {
            Text = "Enter بازگرداندن  ·  F5 تازه‌سازی",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(4, -4, 4, 10),
            FontSize = 12
        });

        _search = VH.Input(page, "جستجو با نام عضو، عنوان کتاب یا کد ملی...");
        _search.TextChanged += (_, _) => Refresh();

        var actions = new WrapPanel { Margin = new Thickness(4, 0, 4, 8) };
        actions.Children.Add(VH.Btn("بازگرداندن", (_, _) => ReturnSelected()));
        page.Children.Add(actions);

        _grid = new DataGrid
        {
            Height = 460, AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "عضو", Binding = new Binding("MemberDisplay"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "کتاب", Binding = new Binding("Book.Title"), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "تاریخ امانت", Binding = new Binding("LoanDate") { StringFormat = "yyyy/MM/dd" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "سررسید", Binding = new Binding("DueDate") { StringFormat = "yyyy/MM/dd" }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "جریمه تخمینی", Binding = new Binding("EstFine"), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) => ReturnSelected();
        page.Children.Add(_grid);
    }

    private void Refresh()
    {
        var active = _svc.Active();
        var q = (_search.Text == (string?)_search.Tag ? "" : _search.Text?.Trim()) ?? "";
        if (!string.IsNullOrWhiteSpace(q))
            active = active.Where(l =>
                (l.Member?.FirstName + " " + l.Member?.LastName).Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                (l.Member?.NationalId ?? "").Contains(q) ||
                (l.Book?.Title ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();

        _grid.ItemsSource = active.Select(l => new
        {
            Loan = l,
            MemberDisplay = $"{l.Member?.LastName}، {l.Member?.FirstName}",
            Book = l.Book,
            LoanDate = l.LoanDate,
            DueDate = l.DueDate,
            EstFine = _svc.CalculateFine(l, DateTime.Now).ToString("N0") + " تومان"
        }).ToList();
    }

    private void ReturnSelected()
    {
        if (_grid.SelectedItem == null) { MessageBox.Show("یک امانت را انتخاب کنید."); return; }
        dynamic row = _grid.SelectedItem;
        Loan loan = row.Loan;
        var fine = _svc.CalculateFine(loan, DateTime.Now);
        bool payNow = false;
        PaymentMethod? method = null;
        if (fine > 0)
        {
            var r = MessageBox.Show($"جریمه تأخیر: {fine:N0} تومان\nآیا اکنون پرداخت شود؟", "جریمه", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Cancel) return;
            if (r == MessageBoxResult.Yes)
            {
                if (!PaymentDialog.ShowPayment("پرداخت جریمه", $"مبلغ: {fine:N0} تومان")) return;
                payNow = true;
                method = PaymentDialog.Method;
            }
        }
        try
        {
            _svc.Return(loan.Id, method, payNow);
            VH.Success("کتاب با موفقیت بازگردانده شد.");
            Refresh();
        }
        catch (Exception ex) { VH.Error(ex); }
    }
}
