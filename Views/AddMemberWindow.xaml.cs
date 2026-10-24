using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class AddMemberWindow : Window
{
    private readonly TextBox[] _fields = new TextBox[5];
    private readonly string[] _labels = { "نام *", "نام خانوادگی *", "کد ملی *", "تلفن *", "نشانی" };
    private readonly string[] _hints = { "مثال: علی", "مثال: محمدی", "۱۰ رقم", "۰۹۱۲...", "اختیاری" };

    public AddMemberWindow()
    {
        InitializeComponent();
        BuildForm();
        Loaded += (_, _) => { _fields[0].Focus(); _fields[0].SelectAll(); };
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                Save_Click(s, e);
            }
        };
    }

    private void BuildForm()
    {
        for (int i = 0; i < 5; i++)
        {
            FormGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = _labels[i],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 12, 8),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            };
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            FormGrid.Children.Add(label);

            var box = new TextBox
            {
                Margin = new Thickness(0, 6, 0, 6),
                Padding = new Thickness(10, 9, 10, 9),
                MinHeight = 38,
                ToolTip = _hints[i],
                MaxLength = i == 2 ? 10 : 100
            };
            if (i == 2)
            {
                box.PreviewTextInput += (s, e) => { e.Handled = !e.Text.All(char.IsDigit); };
                DataObject.AddPastingHandler(box, (s, e) =>
                {
                    if (e.DataObject.GetDataPresent(DataFormats.Text))
                    {
                        var text = (string)e.DataObject.GetData(DataFormats.Text);
                        if (!text.All(char.IsDigit)) e.CancelCommand();
                    }
                });
            }
            VH.EnableEnterAsTab(box);
            Grid.SetRow(box, i);
            Grid.SetColumn(box, 1);
            FormGrid.Children.Add(box);
            _fields[i] = box;
        }

        FormGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var info = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 16, 0, 0)
        };
        Grid.SetRow(info, 5);
        Grid.SetColumnSpan(info, 2);
        info.Child = new TextBlock
        {
            Text = $"پس از ثبت، حق عضویت ۱۲ ماهه به مبلغ {SettingsService.Fee:N0} تومان دریافت می‌شود.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70)),
            FontSize = 12
        };
        FormGrid.Children.Add(info);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrWhiteSpace(_fields[i].Text))
                {
                    MessageBox.Show($"لطفاً «{_labels[i].Replace(" *", "")}» را وارد کنید.", "فیلد الزامی", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _fields[i].Focus();
                    return;
                }
            }

            var nationalId = _fields[2].Text.Trim();
            if (nationalId.Length != 10 || !nationalId.All(char.IsDigit))
            {
                MessageBox.Show("کد ملی باید دقیقاً ۱۰ رقم باشد.", "اعتبارسنجی", MessageBoxButton.OK, MessageBoxImage.Warning);
                _fields[2].Focus();
                return;
            }

            if (!PaymentDialog.ShowPayment("حق عضویت ۱۲ ماهه", $"مبلغ: {SettingsService.Fee:N0} تومان"))
                return;

            var member = new Member
            {
                FirstName = _fields[0].Text.Trim(),
                LastName = _fields[1].Text.Trim(),
                NationalId = nationalId,
                PhoneNumber = _fields[3].Text.Trim(),
                Address = _fields[4].Text.Trim()
            };

            new MemberService().Register(member, 12, PaymentDialog.Method);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطا در ثبت", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
