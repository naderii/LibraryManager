using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NPOI.SS.UserModel;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

/// <summary>Excel importer supporting both legacy .xls and modern .xlsx files.</summary>
public sealed class BookImportWindow : Window
{
    private readonly DataGrid _preview = new();
    private readonly TextBlock _status = new();
    private readonly BookService _books = new();
    private readonly List<ImportRow> _rows = new();
    public int ImportedCount { get; private set; }

    public BookImportWindow()
    {
        Title = "ورود گروهی کتاب از Excel";
        Width = 1120;
        Height = 720;
        MinWidth = 850;
        MinHeight = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = FlowDirection.RightToLeft;

        var root = new DockPanel { Margin = new Thickness(18) };
        Content = root;
        var top = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "ورود گروهی کتاب‌ها", FontSize = 23, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.DarkSlateBlue, Margin = new Thickness(4, 0, 4, 5) });
        top.Children.Add(new TextBlock { Text = "فایل XLS یا XLSX را انتخاب کنید. اطلاعات را بررسی کنید و سپس ردیف‌های معتبر را وارد کنید.", Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(4, 0, 4, 8) });
        var toolbar = new WrapPanel();
        top.Children.Add(toolbar);
        toolbar.Children.Add(MakeButton("انتخاب فایل Excel", SelectFile));
        toolbar.Children.Add(MakeButton("ثبت کتاب‌های معتبر", ImportRows));
        toolbar.Children.Add(MakeButton("بستن", (_, _) => Close(), secondary: true));

        _preview.AutoGenerateColumns = true;
        _preview.IsReadOnly = true;
        _preview.CanUserAddRows = false;
        _preview.Margin = new Thickness(4);
        root.Children.Add(_preview);

        DockPanel.SetDock(_status, Dock.Bottom);
        _status.Margin = new Thickness(5, 10, 5, 0);
        _status.Foreground = System.Windows.Media.Brushes.SlateGray;
        root.Children.Add(_status);
        _status.Text = "هنوز فایلی انتخاب نشده است.";
    }

    private static Button MakeButton(string text, RoutedEventHandler handler, bool secondary = false)
    {
        var button = new Button { Content = text, Margin = new Thickness(4), Padding = new Thickness(14, 9, 14, 9) };
        if (secondary) { button.Background = System.Windows.Media.Brushes.LightGray; button.Foreground = System.Windows.Media.Brushes.Black; }
        button.Click += handler;
        return button;
    }

    private void SelectFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel files (*.xlsx;*.xls)|*.xlsx;*.xls", Title = "انتخاب فایل فهرست کتاب‌ها" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _rows.Clear();
            using var stream = File.OpenRead(dialog.FileName);
            var workbook = WorkbookFactory.Create(stream);
            var sheet = workbook.GetSheetAt(0);
            if (sheet is null || sheet.LastRowNum < 0) throw new InvalidDataException("فایل یا برگه اول خالی است.");

            var formatter = new DataFormatter(CultureInfo.InvariantCulture);
            var first = sheet.GetRow(sheet.FirstRowNum);
            var headerMap = BuildHeaderMap(first, formatter);
            var hasHeader = headerMap.Values.Any(index => index >= 0);
            var firstDataRow = hasHeader ? sheet.FirstRowNum + 1 : sheet.FirstRowNum;
            var existing = _books.All();
            var seenIsbns = new HashSet<string>(existing.Select(x => IsbnLookupService.NormalizeIsbn(x.ISBN)).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase);
            var seenTitles = new HashSet<string>(existing.Select(TitleKey), StringComparer.OrdinalIgnoreCase);

            for (var rowIndex = firstDataRow; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row is null) continue;
                var book = ReadBook(row, headerMap, hasHeader, formatter);
                if (string.IsNullOrWhiteSpace(book.Title)) continue;

                var issue = "آماده ورود";
                var isbn = IsbnLookupService.NormalizeIsbn(book.ISBN);
                if (isbn.Length > 0 && !seenIsbns.Add(isbn)) issue = "تکراری: شابک";
                else if (!seenTitles.Add(TitleKey(book))) issue = "احتمال تکراری: عنوان و نویسنده";
                _rows.Add(new ImportRow { ExcelRow = rowIndex + 1, Title = book.Title, Author = book.Author, Publisher = book.Publisher, ISBN = book.ISBN, Year = book.PublicationYear, Category = book.Category, TotalCopies = book.TotalCopies, AvailableCopies = book.AvailableCopies, Price = book.Price, PageCount = book.PageCount, Language = book.Language, Description = book.Description, Edition = book.Edition, CoverUrl = book.CoverUrl, Book = book, Status = issue });
            }

            _preview.ItemsSource = null;
            _preview.ItemsSource = _rows.Select(r => new { ردیف = r.ExcelRow, عنوان = r.Title, نویسنده = r.Author, ناشر = r.Publisher, شابک = r.ISBN, سال = r.Year, تعداد = r.TotalCopies, وضعیت = r.Status }).ToList();
            _status.Text = $"فایل: {Path.GetFileName(dialog.FileName)} | ردیف‌های قابل بررسی: {_rows.Count} | برای ثبت، دکمه «ثبت کتاب‌های معتبر» را بزنید.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "خواندن فایل Excel ناموفق بود:\n" + ex.Message, "خطای فایل", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportRows(object sender, RoutedEventArgs e)
    {
        var candidates = _rows.Where(r => r.Status == "آماده ورود" && !string.IsNullOrWhiteSpace(r.Book.Title)).ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(this, "کتاب معتبری برای ورود وجود ندارد. ردیف‌های تکراری یا فاقد عنوان وارد نمی‌شوند.", "ورود گروهی", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this, $"تعداد {candidates.Count} کتاب ثبت شود؟", "تأیید ورود", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var imported = 0;
        var errors = 0;
        foreach (var row in candidates)
        {
            try { _books.Save(row.Book); row.Status = "ثبت شد"; imported++; }
            catch (Exception ex) { row.Status = "خطا: " + ex.Message; errors++; }
        }
        ImportedCount = imported;
        _preview.ItemsSource = null;
        _preview.ItemsSource = _rows.Select(r => new { ردیف = r.ExcelRow, عنوان = r.Title, نویسنده = r.Author, ناشر = r.Publisher, شابک = r.ISBN, سال = r.Year, تعداد = r.TotalCopies, وضعیت = r.Status }).ToList();
        _status.Text = $"ثبت موفق: {imported} | خطا: {errors} | تکراری یا نامعتبر: {_rows.Count - candidates.Count}";
        if (errors == 0) DialogResult = true;
        else MessageBox.Show(this, $"{imported} کتاب ثبت شد و {errors} ردیف خطا داشت. وضعیت هر ردیف در پیش‌نمایش نمایش داده شده است.", "نتیجه ورود", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static Dictionary<string, int> BuildHeaderMap(IRow? row, DataFormatter formatter)
    {
        var aliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Book.Title)] = new[] { "title", "booktitle", "عنوان", "عنوانکتاب", "نامکتاب", "نامکتاب" },
            [nameof(Book.Author)] = new[] { "author", "authors", "نویسنده", "نویسندگان", "مولف", "مؤلف" },
            [nameof(Book.Publisher)] = new[] { "publisher", "ناشر", "انتشارات" },
            [nameof(Book.ISBN)] = new[] { "isbn", "شابک", "isbn10", "isbn13" },
            [nameof(Book.PublicationYear)] = new[] { "year", "publicationyear", "سال", "سالانتشار", "سالچاپ" },
            [nameof(Book.Category)] = new[] { "category", "subject", "موضوع", "دسته", "دستهبندی" },
            [nameof(Book.TotalCopies)] = new[] { "totalcopies", "copies", "quantity", "تعداد", "تعدادکل", "نسخهها" },
            [nameof(Book.AvailableCopies)] = new[] { "availablecopies", "available", "تعدادموجود", "موجود" },
            [nameof(Book.ShelfLocation)] = new[] { "shelf", "shelflocation", "قفسه", "محلقفسه" },
            [nameof(Book.Price)] = new[] { "price", "قیمت", "بها" },
            [nameof(Book.PageCount)] = new[] { "pages", "pagecount", "تعدادصفحات", "صفحات" },
            [nameof(Book.Language)] = new[] { "language", "زبان" },
            [nameof(Book.Edition)] = new[] { "edition", "ویرایش", "نوبتچاپ" },
            [nameof(Book.Description)] = new[] { "description", "توضیحات", "شرح" },
            [nameof(Book.CoverUrl)] = new[] { "coverurl", "cover", "جلد", "تصویرجلد" }
        };
        var result = aliases.Keys.ToDictionary(k => k, _ => -1, StringComparer.OrdinalIgnoreCase);
        if (row is null) return result;
        for (var i = 0; i < row.LastCellNum; i++)
        {
            var header = NormalizeHeader(formatter.FormatCellValue(row.GetCell(i)));
            foreach (var pair in aliases)
                if (pair.Value.Select(NormalizeHeader).Contains(header, StringComparer.OrdinalIgnoreCase)) result[pair.Key] = i;
        }
        return result;
    }

    private static string NormalizeHeader(string value) => (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace("ي", "ی").Replace("ك", "ک");

    private static Book ReadBook(IRow row, Dictionary<string, int> map, bool hasHeader, DataFormatter formatter)
    {
        string Cell(string key, int fallback)
        {
            var index = hasHeader ? map.GetValueOrDefault(key, -1) : fallback;
            return index < 0 ? string.Empty : formatter.FormatCellValue(row.GetCell(index)).Trim();
        }
        int Int(string key, int fallback, int defaultValue = 0) => int.TryParse(Cell(key, fallback), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : defaultValue;
        decimal Dec(string key, int fallback) => decimal.TryParse(Cell(key, fallback), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : decimal.TryParse(Cell(key, fallback), NumberStyles.Number, CultureInfo.CurrentCulture, out n) ? n : 0;

        var total = Math.Max(0, Int(nameof(Book.TotalCopies), 6));
        var available = Math.Clamp(Int(nameof(Book.AvailableCopies), 7, total), 0, total);
        return new Book
        {
            Title = Cell(nameof(Book.Title), 0), Author = Cell(nameof(Book.Author), 1), Publisher = Cell(nameof(Book.Publisher), 2),
            ISBN = Cell(nameof(Book.ISBN), 3), PublicationYear = Int(nameof(Book.PublicationYear), 4), Category = Cell(nameof(Book.Category), 5),
            TotalCopies = total, AvailableCopies = available, ShelfLocation = Cell(nameof(Book.ShelfLocation), 8), Price = Dec(nameof(Book.Price), 9),
            PageCount = Math.Max(0, Int(nameof(Book.PageCount), 10)), Language = Cell(nameof(Book.Language), 11), Edition = Cell(nameof(Book.Edition), 12),
            Description = Cell(nameof(Book.Description), 13), CoverUrl = Cell(nameof(Book.CoverUrl), 14), CreatedAt = DateTime.Now
        };
    }

    private static string TitleKey(Book book) => (book.Title.Trim() + "|" + book.Author.Trim()).ToLowerInvariant();

    private sealed class ImportRow
    {
        public int ExcelRow { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Category { get; set; } = string.Empty;
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public decimal Price { get; set; }
        public int PageCount { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Book Book { get; set; } = new();
    }
}
