using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class MembersView : UserControl
{
    private readonly MemberService _svc = new();
    private DataGrid _grid = null!;
    private TextBox _search = null!;

    public MembersView()
    {
        InitializeComponent();
        BuildUi();
        Refresh();
        Loaded += (_, _) => _search.Focus();
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { AddMember(); e.Handled = true; }
            if (e.Key == Key.F5) { Refresh(); e.Handled = true; }
        };
    }

    private void BuildUi()
    {
        var page = VH.Page(Root, "اعضا");

        var bar = new Grid { Margin = new Thickness(4, 0, 4, 10) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _search = new TextBox
        {
            Padding = new Thickness(12, 9, 12, 9),
            MinHeight = 40,
            Tag = "جستجو با نام، کد ملی یا تلفن…",
            Margin = new Thickness(0, 0, 10, 0)
        };
        ApplyPh(_search);
        _search.TextChanged += (_, _) => Refresh();
        Grid.SetColumn(_search, 0);
        bar.Children.Add(_search);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(VH.Btn("عضو جدید", (_, _) => AddMember()));
        actions.Children.Add(VH.Btn("تمدید", (_, _) => RenewSelected(), false));
        actions.Children.Add(VH.Btn("حذف", (_, _) => DeleteSelected(), false));
        Grid.SetColumn(actions, 1);
        bar.Children.Add(actions);
        page.Children.Add(bar);

        page.Children.Add(new TextBlock
        {
            Text = "Ctrl+N عضو جدید  ·  Enter تمدید  ·  Delete حذف  ·  F5 تازه‌سازی",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(4, 0, 4, 8)
        });

        _grid = new DataGrid
        {
            Height = 480,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single
        };
        _grid.Columns.Add(Col("نام", nameof(Member.FirstName), 1));
        _grid.Columns.Add(Col("نام خانوادگی", nameof(Member.LastName), 1.2));
        _grid.Columns.Add(Col("کد ملی", nameof(Member.NationalId), 1));
        _grid.Columns.Add(Col("تلفن", nameof(Member.PhoneNumber), 1));
        _grid.Columns.Add(Col("انقضای عضویت", nameof(Member.MembershipExpiryDate), 1.1));
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "وضعیت",
            Binding = new Binding(nameof(Member.IsActive)) { Converter = new ActiveConverter() },
            Width = new DataGridLength(0.7, DataGridLengthUnitType.Star)
        });
        _grid.MouseDoubleClick += (_, _) => RenewSelected();
        _grid.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { RenewSelected(); e.Handled = true; }
            if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
        };
        page.Children.Add(_grid);
    }

    private static DataGridTextColumn Col(string h, string path, double star) => new()
    {
        Header = h,
        Binding = new Binding(path) { StringFormat = path.Contains("Date") ? "yyyy/MM/dd" : null },
        Width = new DataGridLength(star, DataGridLengthUnitType.Star)
    };

    private static void ApplyPh(TextBox box)
    {
        var ph = (string)box.Tag;
        box.Text = ph;
        box.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        box.GotFocus += (s, e) => { if (box.Text == ph) { box.Text = ""; box.Foreground = Brushes.Black; } };
        box.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(box.Text)) { box.Text = ph; box.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); } };
    }

    private void Refresh()
    {
        var all = _svc.All();
        var q = _search.Text == (string)_search.Tag ? "" : (_search.Text?.Trim() ?? "");
        if (!string.IsNullOrWhiteSpace(q))
            all = all.Where(m =>
                Has(m.FirstName, q) || Has(m.LastName, q) || Has(m.NationalId, q) || Has(m.PhoneNumber, q)).ToList();
        _grid.ItemsSource = all;
    }

    private static bool Has(string v, string q) => (v ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase);

    private void AddMember()
    {
        var w = new AddMemberWindow { Owner = Window.GetWindow(this) };
        if (w.ShowDialog() == true) Refresh();
    }

    private void RenewSelected()
    {
        if (_grid.SelectedItem is not Member m) { MessageBox.Show("یک عضو را انتخاب کنید."); return; }
        if (!PaymentDialog.ShowPayment("تمدید عضویت ۱۲ ماهه",
                $"{m.FirstName} {m.LastName}\nمبلغ: {SettingsService.Fee:N0} تومان")) return;
        try
        {
            _svc.Renew(m.Id, 12, PaymentDialog.Method);
            Refresh();
            MessageBox.Show("عضویت تمدید شد.", "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { VH.Error(ex); }
    }

    private void DeleteSelected()
    {
        if (_grid.SelectedItem is not Member m) { MessageBox.Show("یک عضو را انتخاب کنید."); return; }
        if (MessageBox.Show($"«{m.FirstName} {m.LastName}» حذف شود؟", "تأیید", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try { _svc.Delete(m.Id); Refresh(); }
        catch (Exception ex) { VH.Error(ex); }
    }

    private class ActiveConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => value is true ? "فعال" : "منقضی";
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotImplementedException();
    }
}
