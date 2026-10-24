using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class AddEditBookWindow : Window
{
    private readonly Book _original;
    private readonly Dictionary<string, TextBox> _fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly IsbnLookupService _isbnLookup = new();
    public Book Result { get; private set; }

    public AddEditBookWindow(Book? book)
    {
        InitializeComponent();
        _original = book is null ? new Book() : Copy(book);
        Heading.Text = book is null ? "ثبت کتاب جدید" : "ویرایش اطلاعات کتاب";
        BuildFields();
        Loaded += (_, _) =>
        {
            if (_fields.TryGetValue(nameof(Book.Title), out var titleBox))
            {
                titleBox.Focus();
                titleBox.SelectAll();
            }
        };
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.F4)
        {
            // دریافت اطلاعات با شابک
            LookupIsbn_Click(SaveButton, e);
            e.Handled = true;
        }
    }

    private void BuildFields()
    {
        var specs = new (string Key, string Label, string Value, bool Multiline)[]
        {
            (nameof(Book.Title), "عنوان کتاب *", _original.Title, false),
            (nameof(Book.Author), "نویسنده", _original.Author, false),
            (nameof(Book.Publisher), "ناشر", _original.Publisher, false),
            (nameof(Book.ISBN), "شابک (ISBN-10 / ISBN-13)", _original.ISBN, false),
            (nameof(Book.PublicationYear), "سال انتشار", _original.PublicationYear == 0 ? "" : _original.PublicationYear.ToString(), false),
            (nameof(Book.Category), "موضوع / دسته‌بندی", _original.Category, false),
            (nameof(Book.TotalCopies), "تعداد کل نسخه‌ها", _original.TotalCopies.ToString(), false),
            (nameof(Book.AvailableCopies), "تعداد نسخه‌های موجود", _original.AvailableCopies.ToString(), false),
            (nameof(Book.ShelfLocation), "محل قفسه", _original.ShelfLocation, false),
            (nameof(Book.Price), "قیمت", _original.Price == 0 ? "" : _original.Price.ToString(CultureInfo.CurrentCulture), false),
            (nameof(Book.PageCount), "تعداد صفحات", _original.PageCount == 0 ? "" : _original.PageCount.ToString(), false),
            (nameof(Book.Language), "زبان", _original.Language, false),
            (nameof(Book.Edition), "نوبت / شماره ویرایش", _original.Edition, false),
            (nameof(Book.CoverUrl), "آدرس تصویر جلد", _original.CoverUrl, false),
            (nameof(Book.Description), "توضیحات", _original.Description, true)
        };

        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        FieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var row = 0;
        foreach (var spec in specs)
        {
            FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = spec.Label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 8, 8, 8),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            FieldsGrid.Children.Add(label);

            var box = new TextBox
            {
                Text = spec.Value ?? string.Empty,
                Margin = new Thickness(4, 4, 4, 4),
                MinHeight = spec.Multiline ? 90 : 34,
                AcceptsReturn = spec.Multiline,
                TextWrapping = spec.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalScrollBarVisibility = spec.Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };
            if (!spec.Multiline)
            {
                box.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        // آخرین فیلد غیر چندخطی قبل از توضیحات → ذخیره
                        if (spec.Key == nameof(Book.Edition) || spec.Key == nameof(Book.CoverUrl))
                        {
                            // رفتن به فیلد بعدی
                            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        }
                        else
                        {
                            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        }
                    }
                };
            }
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            FieldsGrid.Children.Add(box);
            _fields[spec.Key] = box;
            row++;

            if (spec.Key == nameof(Book.ISBN))
            {
                FieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var lookup = new Button
                {
                    Content = "دریافت اطلاعات از اینترنت با شابک",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(4, 0, 4, 8),
                    Style = (Style)Application.Current.FindResource("SecondaryButton")
                };
                lookup.Click += LookupIsbn_Click;
                Grid.SetRow(lookup, row);
                Grid.SetColumn(lookup, 1);
                FieldsGrid.Children.Add(lookup);
                row++;
            }
        }
    }

    private async void LookupIsbn_Click(object sender, RoutedEventArgs e)
    {
        var isbn = _fields[nameof(Book.ISBN)].Text.Trim();
        try
        {
            if (string.IsNullOrWhiteSpace(isbn))
            {
                MessageBox.Show(this, "ابتدا شابک کتاب را وارد کنید.", "شابک", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // چک تکراری بودن شابک در سیستم
            var bookService = new BookService();
            if (bookService.IsbnExists(isbn, _original.Id))
            {
                var existing = bookService.FindByIsbn(isbn)!;
                MessageBox.Show(this,
                    "این شابک قبلاً در سیستم ثبت شده است.\n\n" +
                    $"عنوان: {existing.Title}\n" +
                    $"نویسنده: {existing.Author}\n" +
                    $"شناسه: {existing.Id}",
                    "شابک تکراری", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // چک اینترنت قبل از هر کوئری
            if (sender is Button button) { button.IsEnabled = false; button.Content = "بررسی اتصال..."; }
            if (!await IsbnLookupService.IsOnlineAsync())
            {
                MessageBox.Show(this,
                    "اتصال اینترنت برقرار نیست.\n\nبدون اینترنت امکان دریافت اطلاعات از منابع آنلاین وجود ندارد.\nلطفاً اتصال را بررسی کنید یا فیلدها را دستی پر کنید.",
                    "بدون اینترنت", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button btn2) btn2.Content = "در حال جست‌وجو...";
            var results = await _isbnLookup.LookupAllAsync(isbn);

            if (results.Count == 0)
            {
                MessageBox.Show(this,
                    "اطلاعاتی برای این شابک در منابع آنلاین پیدا نشد.\n\n" +
                    "• شابک را دوباره بررسی کنید\n" +
                    "• برخی کتاب‌های ایرانی در بانک‌های عمومی ثبت نشده‌اند؛ فیلدها را دستی پر کنید",
                    "نتیجه جست‌وجو", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BookMetadata? chosen = null;
            if (results.Count == 1)
            {
                chosen = results[0];
            }
            else
            {
                // چند منبع پیدا کرده — انتخاب کاربر
                chosen = ShowSourcePicker(results);
                if (chosen is null) return; // کاربر لغو کرد
            }

            // فقط فیلدهای خالی را پر کن
            FillIfBlank(nameof(Book.Title), chosen.Title);
            FillIfBlank(nameof(Book.Author), chosen.Authors);
            FillIfBlank(nameof(Book.Publisher), chosen.Publisher);
            FillIfBlank(nameof(Book.Category), chosen.Categories);
            FillIfBlank(nameof(Book.Description), chosen.Description);
            FillIfBlank(nameof(Book.Language), chosen.Language);
            FillIfBlank(nameof(Book.CoverUrl), chosen.CoverUrl);
            FillIfBlank(nameof(Book.PageCount), chosen.PageCount > 0 ? chosen.PageCount.ToString() : "");
            FillIfBlank(nameof(Book.PublicationYear), ExtractYear(chosen.PublishedDate));

            MessageBox.Show(this,
                $"اطلاعات از «{chosen.SourceName}» به فیلدهای خالی اضافه شد.\nلطفاً قبل از ذخیره بررسی کنید.",
                "تکمیل اطلاعات", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "دریافت اطلاعات کتاب ناموفق بود:\n" + ex.Message, "خطای جست‌وجو", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (sender is Button button) { button.IsEnabled = true; button.Content = "دریافت اطلاعات از اینترنت با شابک"; }
        }
    }

    /// <summary>
    /// پنجره ساده برای انتخاب یکی از نتایج چندمنبعی
    /// </summary>
    private BookMetadata? ShowSourcePicker(List<BookMetadata> results)
    {
        var win = new Window
        {
            Title = "انتخاب منبع اطلاعات",
            Width = 560,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = new FontFamily("Tahoma"),
            Background = new SolidColorBrush(Color.FromRgb(243, 246, 250))
        };

        var root = new DockPanel { Margin = new Thickness(16) };
        var header = new TextBlock
        {
            Text = $"برای این شابک {results.Count} منبع پیدا شد. یکی را انتخاب کنید:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var list = new ListBox
        {
            Margin = new Thickness(0, 0, 0, 12),
            DisplayMemberPath = "SummaryLine"
        };
        foreach (var r in results) list.Items.Add(r);
        list.SelectedIndex = 0;
        root.Children.Add(list);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        BookMetadata? selected = null;
        var ok = new Button
        {
            Content = "انتخاب و پر کردن فیلدها",
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(23, 107, 135)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        ok.Click += (_, _) =>
        {
            selected = list.SelectedItem as BookMetadata;
            win.DialogResult = true;
        };
        var cancel = new Button
        {
            Content = "انصراف",
            Padding = new Thickness(16, 8, 16, 8)
        };
        cancel.Click += (_, _) => win.DialogResult = false;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        // جزئیات منبع انتخاب‌شده
        var detail = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            FontSize = 12
        };
        DockPanel.SetDock(detail, Dock.Bottom);
        root.Children.Add(detail);

        void UpdateDetail()
        {
            if (list.SelectedItem is BookMetadata m)
            {
                detail.Text =
                    $"منبع: {m.SourceName}\n" +
                    $"عنوان: {m.Title}\n" +
                    $"نویسنده: {m.Authors}\n" +
                    $"ناشر: {m.Publisher}\n" +
                    $"سال: {m.PublishedDate}\n" +
                    $"صفحات: {(m.PageCount > 0 ? m.PageCount.ToString() : "—")}";
            }
        }
        list.SelectionChanged += (_, _) => UpdateDetail();
        UpdateDetail();

        // ترتیب: header, detail+buttons bottom, list fill
        // ساده‌تر: از Grid استفاده کنیم
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(header, 0);
        Grid.SetRow(list, 1);
        Grid.SetRow(detail, 2);
        Grid.SetRow(buttons, 3);
        // بازسازی
        var g = new Grid { Margin = new Thickness(16) };
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.Children.Add(header); Grid.SetRow(header, 0);
        g.Children.Add(list); Grid.SetRow(list, 1);
        g.Children.Add(detail); Grid.SetRow(detail, 2);
        g.Children.Add(buttons); Grid.SetRow(buttons, 3);
        win.Content = g;

        list.MouseDoubleClick += (_, _) =>
        {
            selected = list.SelectedItem as BookMetadata;
            win.DialogResult = true;
        };

        return win.ShowDialog() == true ? selected : null;
    }

    private void FillIfBlank(string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_fields[key].Text))
            _fields[key].Text = value;
    }

    private static string ExtractYear(string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(date, @"\b(13\d{2}|14\d{2}|1[5-9]\d{2}|20\d{2}|21\d{2})\b");
        return match.Success ? match.Value : string.Empty;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var title = _fields[nameof(Book.Title)].Text.Trim();
            if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("عنوان کتاب الزامی است.");

            var total = ParseInt(nameof(Book.TotalCopies), 0);
            var available = ParseInt(nameof(Book.AvailableCopies), total);
            if (total < 0 || available < 0 || available > total)
                throw new InvalidOperationException("تعداد موجود باید بین صفر و تعداد کل نسخه‌ها باشد.");

            Result = new Book
            {
                Id = _original.Id,
                Title = title,
                Author = Value(nameof(Book.Author)),
                Publisher = Value(nameof(Book.Publisher)),
                ISBN = Value(nameof(Book.ISBN)),
                PublicationYear = ParseInt(nameof(Book.PublicationYear), 0),
                Category = Value(nameof(Book.Category)),
                TotalCopies = total,
                AvailableCopies = available,
                ShelfLocation = Value(nameof(Book.ShelfLocation)),
                Price = ParseDecimal(nameof(Book.Price)),
                CreatedAt = _original.CreatedAt,
                ImagePath = _original.ImagePath,
                Description = Value(nameof(Book.Description)),
                PageCount = ParseInt(nameof(Book.PageCount), 0),
                Language = Value(nameof(Book.Language)),
                Edition = Value(nameof(Book.Edition)),
                CoverUrl = Value(nameof(Book.CoverUrl))
            };
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "بررسی اطلاعات", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private string Value(string key) => _fields[key].Text.Trim();
    private int ParseInt(string key, int fallback) => string.IsNullOrWhiteSpace(Value(key)) ? fallback : int.Parse(Value(key), CultureInfo.CurrentCulture);
    private decimal ParseDecimal(string key)
    {
        var text = Value(key);
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var current)) return current;
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)) return invariant;
        throw new FormatException("مقدار قیمت معتبر نیست.");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static Book Copy(Book b) => new()
    {
        Id = b.Id, Title = b.Title, Author = b.Author, Publisher = b.Publisher, ISBN = b.ISBN,
        PublicationYear = b.PublicationYear, Category = b.Category, TotalCopies = b.TotalCopies,
        AvailableCopies = b.AvailableCopies, ShelfLocation = b.ShelfLocation, Price = b.Price,
        CreatedAt = b.CreatedAt, ImagePath = b.ImagePath, Description = b.Description,
        PageCount = b.PageCount, Language = b.Language, Edition = b.Edition, CoverUrl = b.CoverUrl
    };
}
