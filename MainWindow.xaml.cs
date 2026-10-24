using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Services;
using LibraryManager.Views;

namespace LibraryManager;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, Button> _navButtons = new();
    private TextBlock? _sidebarLibraryName;

    public MainWindow()
    {
        InitializeComponent();
        CollectNavButtons();
        FindSidebarLibraryName();
        ApplyLibraryName();
        NavigateTo("Books");

        PreviewKeyDown += (s, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            var map = new Dictionary<Key, string>
            {
                [Key.D1] = "Books", [Key.NumPad1] = "Books",
                [Key.D2] = "Members", [Key.NumPad2] = "Members",
                [Key.D3] = "Loans", [Key.NumPad3] = "Loans",
                [Key.D4] = "Returns", [Key.NumPad4] = "Returns",
                [Key.D5] = "Fines", [Key.NumPad5] = "Fines",
                [Key.D6] = "Reports", [Key.NumPad6] = "Reports",
                [Key.D7] = "Backup", [Key.NumPad7] = "Backup",
                [Key.D8] = "Settings", [Key.NumPad8] = "Settings"
            };
            if (map.TryGetValue(e.Key, out var tag))
            {
                NavigateTo(tag);
                e.Handled = true;
            }
        };
    }

    public void ApplyLibraryName()
    {
        var name = SettingsService.LibraryName;
        Title = name + " — مدیریت کتابخانه";
        if (_sidebarLibraryName != null)
            _sidebarLibraryName.Text = name;
    }

    private void FindSidebarLibraryName()
    {
        // پیدا کردن TextBlock نام کتابخانه در سایدبار (اولین TextBlock با FontSize=18)
        foreach (var tb in FindVisualChildren<TextBlock>(this))
        {
            if (tb.FontSize == 18 && tb.FontWeight == FontWeights.Bold)
            {
                _sidebarLibraryName = tb;
                break;
            }
        }
    }

    private void CollectNavButtons()
    {
        foreach (var btn in FindVisualChildren<Button>(this))
        {
            if (btn.Tag is string tag && !string.IsNullOrEmpty(tag))
                _navButtons[tag] = btn;
        }
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        Host.Content = tag switch
        {
            "Books" => new BooksView(),
            "Members" => new MembersView(),
            "Loans" => new LoanRegisterView(),
            "Returns" => new LoanReturnView(),
            "Fines" => new UnpaidFinesView(),
            "Reports" => new ReportsView(),
            "Backup" => new BackupView(),
            _ => new SettingsView()
        };

        PageTitle.Text = tag switch
        {
            "Books" => "فهرست کتاب‌ها",
            "Members" => "اعضا",
            "Loans" => "ثبت امانت",
            "Returns" => "بازگرداندن کتاب",
            "Fines" => "جریمه‌های پرداخت‌نشده",
            "Reports" => "گزارش‌ها",
            "Backup" => "پشتیبان‌گیری",
            _ => "تنظیمات"
        };

        var activeBg = new SolidColorBrush(Color.FromRgb(23, 107, 135));
        foreach (var kv in _navButtons)
        {
            kv.Value.Background = kv.Key == tag ? activeBg : Brushes.Transparent;
            kv.Value.Foreground = kv.Key == tag
                ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(226, 232, 240));
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T : DependencyObject
    {
        if (dep == null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
        {
            var child = VisualTreeHelper.GetChild(dep, i);
            if (child is T t) yield return t;
            foreach (var c in FindVisualChildren<T>(child)) yield return c;
        }
    }
}
