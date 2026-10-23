using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

            if (sender is Button button) { button.IsEnabled = false; button.Content = "در حال جست‌وجو..."; }
            var metadata = await _isbnLookup.LookupAsync(isbn);
            if (metadata is null)
            {
                MessageBox.Show(this, "اطلاعاتی برای این شابک پیدا نشد. اتصال اینترنت و صحت شابک را بررسی کنید.", "نتیجه جست‌وجو", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Preserve every field already entered by the user; only fill blanks.
            FillIfBlank(nameof(Book.Title), metadata.Title);
            FillIfBlank(nameof(Book.Author), metadata.Authors);
            FillIfBlank(nameof(Book.Publisher), metadata.Publisher);
            FillIfBlank(nameof(Book.Category), metadata.Categories);
            FillIfBlank(nameof(Book.Description), metadata.Description);
            FillIfBlank(nameof(Book.Language), metadata.Language);
            FillIfBlank(nameof(Book.CoverUrl), metadata.CoverUrl);
            FillIfBlank(nameof(Book.PageCount), metadata.PageCount > 0 ? metadata.PageCount.ToString() : "");
            FillIfBlank(nameof(Book.PublicationYear), ExtractYear(metadata.PublishedDate));
            MessageBox.Show(this, "اطلاعات پیدا‌شده به فیلدهای خالی اضافه شد. لطفاً قبل از ذخیره بررسی کنید؛ اطلاعات اینترنتی ممکن است ناقص باشد.", "تکمیل اطلاعات", MessageBoxButton.OK, MessageBoxImage.Information);
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
