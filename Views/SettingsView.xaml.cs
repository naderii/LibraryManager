using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class SettingsView : UserControl
{
    private TextBox _libraryName = null!, _fee = null!, _fine = null!, _days = null!, _max = null!;

    public SettingsView()
    {
        InitializeComponent();
        BuildUi();
        Loaded += (_, _) => _libraryName.Focus();
    }

    private void BuildUi()
    {
        var page = VH.Page(Root, "تنظیمات");
        page.Children.Add(new TextBlock
        {
            Text = "Ctrl+S ذخیره",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(4, -6, 4, 12), FontSize = 12
        });

        void AddField(string label, out TextBox box, string value)
        {
            page.Children.Add(new TextBlock { Text = label, Margin = new Thickness(4, 8, 4, 2), FontWeight = FontWeights.SemiBold });
            box = new TextBox { Text = value, Margin = new Thickness(4, 0, 4, 4), Padding = new Thickness(10, 8, 10, 8), MinHeight = 36 };
            VH.EnableEnterAsTab(box);
            page.Children.Add(box);
        }

        AddField("نام کتابخانه", out _libraryName, SettingsService.LibraryName);
        AddField("حق عضویت (تومان)", out _fee, SettingsService.Fee.ToString("0"));
        AddField("جریمه روزانه (تومان)", out _fine, SettingsService.FinePerDay.ToString("0"));
        AddField("مدت امانت (روز)", out _days, SettingsService.LoanDays.ToString());
        AddField("حداکثر امانت فعال هر عضو", out _max, SettingsService.MaxLoans.ToString());

        var btn = VH.Btn("ذخیره تنظیمات (Ctrl+S)", Save);
        page.Children.Add(btn);

        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Save(s, e);
                e.Handled = true;
            }
        };
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = _libraryName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("نام کتابخانه نمی‌تواند خالی باشد.");

            SettingsService.Save("LibraryName", name);
            SettingsService.Save("MembershipFeeAmount", _fee.Text.Trim());
            SettingsService.Save("FineAmountPerDay", _fine.Text.Trim());
            SettingsService.Save("LoanPeriodDays", _days.Text.Trim());
            SettingsService.Save("MaxActiveLoansPerMember", _max.Text.Trim());

            // به‌روزرسانی عنوان پنجره اصلی
            if (Window.GetWindow(this) is MainWindow main)
                main.ApplyLibraryName();

            VH.Success("تنظیمات ذخیره شد.");
        }
        catch (Exception ex) { VH.Error(ex); }
    }
}
