using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class BooksView : UserControl
{
    private readonly BookService _service = new();
    private readonly DataGrid _grid;
    private readonly TextBox _search;

    public BooksView()
    {
        InitializeComponent();
        var page = VH.Page(Root, "فهرست کتاب‌ها");
        page.Children.Add(new TextBlock
        {
            Text = "مدیریت مشخصات کتاب‌ها، ورود گروهی از Excel و تکمیل اطلاعات با شابک",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(10, -3, 10, 10)
        });

        _search = VH.Input(page, "جست‌وجو بر اساس عنوان، نویسنده، ناشر یا شابک");
        _search.TextChanged += (_, _) => Refresh();

        var actions = new WrapPanel { Margin = new Thickness(4, 0, 4, 4) };
        actions.Children.Add(VH.Btn("＋ افزودن کتاب", (_, _) => Edit(null)));
        actions.Children.Add(VH.Btn("✎ ویرایش انتخاب‌شده", (_, _) => Edit(_grid.SelectedItem as Book)));
        actions.Children.Add(VH.Btn("⇧ ورود گروهی Excel", ImportExcel));
        actions.Children.Add(VH.Btn("حذف کتاب", DeleteSelected));
        page.Children.Add(actions);

        _grid = new DataGrid { Height = 490, AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single };
        _grid.Columns.Add(TextColumn("عنوان کتاب", nameof(Book.Title), 2));
        _grid.Columns.Add(TextColumn("نویسنده", nameof(Book.Author), 1.4));
        _grid.Columns.Add(TextColumn("ناشر", nameof(Book.Publisher), 1.2));
        _grid.Columns.Add(TextColumn("شابک", nameof(Book.ISBN), 1.2));
        _grid.Columns.Add(TextColumn("سال", nameof(Book.PublicationYear), 0.6));
        _grid.Columns.Add(TextColumn("موضوع", nameof(Book.Category), 1));
        _grid.Columns.Add(TextColumn("کل", nameof(Book.TotalCopies), 0.5));
        _grid.Columns.Add(TextColumn("موجود", nameof(Book.AvailableCopies), 0.6));
        page.Children.Add(_grid);
        Refresh();
    }

    private static DataGridTextColumn TextColumn(string title, string path, double star) => new()
    {
        Header = title,
        Binding = new Binding(path),
        Width = new DataGridLength(star, DataGridLengthUnitType.Star)
    };

    private void Edit(Book? book)
    {
        var window = new AddEditBookWindow(book) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            try { _service.Save(window.Result); Refresh(); }
            catch (Exception ex) { VH.Error(ex); }
        }
    }

    private void ImportExcel(object sender, RoutedEventArgs e)
    {
        var window = new BookImportWindow { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            Refresh();
            MessageBox.Show(Window.GetWindow(this), $"ورود گروهی انجام شد. تعداد کتاب‌های ثبت‌شده: {window.ImportedCount}", "ورود از Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DeleteSelected(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_grid.SelectedItem is not Book book) { MessageBox.Show("ابتدا یک کتاب را انتخاب کنید."); return; }
            if (MessageBox.Show($"کتاب «{book.Title}» حذف شود؟", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _service.Delete(book.Id);
            Refresh();
        }
        catch (Exception ex) { VH.Error(ex); }
    }

    private void Refresh()
    {
        var all = _service.All();
        var q = _search?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(q))
            all = all.Where(x => Contains(x.Title, q) || Contains(x.Author, q) || Contains(x.Publisher, q) || Contains(x.ISBN, q)).ToList();
        _grid.ItemsSource = all;
    }

    private static bool Contains(string? value, string q) => (value ?? string.Empty).Contains(q, StringComparison.CurrentCultureIgnoreCase);
}
