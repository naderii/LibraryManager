using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class BooksView : UserControl
{
    private readonly BookService _service = new();
    private DataGrid _grid = null!;
    private TextBox _search = null!;
    private const string SearchPlaceholder = "جستجو با عنوان، نویسنده، ناشر یا شابک…";

    public BooksView()
    {
        InitializeComponent();
        BuildUi();
        Refresh();
        Loaded += (_, _) => _search.Focus();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void BuildUi()
    {
        var page = VH.Page(Root, "فهرست کتاب‌ها");

        page.Children.Add(new TextBlock
        {
            Text = "+ افزودن   ·   Enter ویرایش   ·   - حذف   ·   F5 تازه‌سازی",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(4, -4, 4, 10),
            FontSize = 12
        });

        // نوار جستجو + دکمه‌ها
        var bar = new Grid { Margin = new Thickness(4, 0, 4, 10) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _search = new TextBox
        {
            Padding = new Thickness(12, 9, 12, 9),
            MinHeight = 40,
            Tag = SearchPlaceholder,
            Margin = new Thickness(0, 0, 10, 0)
        };
        ApplyPlaceholder(_search, SearchPlaceholder);
        _search.TextChanged += (_, _) => Refresh();
        Grid.SetColumn(_search, 0);
        bar.Children.Add(_search);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(VH.Btn("افزودن", (_, _) => Edit(null)));
        actions.Children.Add(VH.Btn("ویرایش", (_, _) => EditSelected(), false));
        actions.Children.Add(VH.Btn("حذف", (_, _) => DoDelete(), false));
        actions.Children.Add(VH.Btn("ورود Excel", ImportExcel, false));
        Grid.SetColumn(actions, 1);
        bar.Children.Add(actions);
        page.Children.Add(bar);

        _grid = new DataGrid
        {
            Height = 480,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single
        };
        _grid.Columns.Add(TextColumn("عنوان", nameof(Book.Title), 2));
        _grid.Columns.Add(TextColumn("نویسنده", nameof(Book.Author), 1.3));
        _grid.Columns.Add(TextColumn("ناشر", nameof(Book.Publisher), 1.1));
        _grid.Columns.Add(TextColumn("شابک", nameof(Book.ISBN), 1.1));
        _grid.Columns.Add(TextColumn("سال", nameof(Book.PublicationYear), 0.5));
        _grid.Columns.Add(TextColumn("موضوع", nameof(Book.Category), 0.9));
        _grid.Columns.Add(TextColumn("کل", nameof(Book.TotalCopies), 0.45));
        _grid.Columns.Add(TextColumn("موجود", nameof(Book.AvailableCopies), 0.55));
        _grid.MouseDoubleClick += (_, _) => EditSelected();
        _grid.KeyDown += Grid_KeyDown;
        page.Children.Add(_grid);
    }

    private static DataGridTextColumn TextColumn(string title, string path, double star) => new()
    {
        Header = title,
        Binding = new Binding(path),
        Width = new DataGridLength(star, DataGridLengthUnitType.Star)
    };

    private static void ApplyPlaceholder(TextBox box, string ph)
    {
        box.Text = ph;
        box.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        box.GotFocus += (s, e) =>
        {
            if (box.Text == ph) { box.Text = ""; box.Foreground = Brushes.Black; }
        };
        box.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = ph;
                box.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        };
    }

    private string GetSearchQuery()
    {
        var t = _search.Text?.Trim() ?? "";
        return t == SearchPlaceholder || t == (string)_search.Tag ? "" : t;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // + یا = برای افزودن (روی کیبورد فارسی/انگلیسی)
        if (e.Key == Key.OemPlus || e.Key == Key.Add ||
            (e.Key == Key.OemMinus && Keyboard.Modifiers == ModifierKeys.Shift)) // بعضی کیبوردها
        {
            // فقط وقتی روی TextBox نیستیم که + تایپ نشود
            if (Keyboard.FocusedElement is not TextBox)
            {
                Edit(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
        {
            if (Keyboard.FocusedElement is not TextBox)
            {
                DoDelete();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F5)
        {
            Refresh();
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Edit(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _search.Focus();
            if (_search.Text != SearchPlaceholder) _search.SelectAll();
            e.Handled = true;
        }
    }

    private void Grid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            EditSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete || e.Key == Key.OemMinus || e.Key == Key.Subtract)
        {
            DoDelete();
            e.Handled = true;
        }
        else if (e.Key == Key.OemPlus || e.Key == Key.Add)
        {
            Edit(null);
            e.Handled = true;
        }
    }

    private void EditSelected()
    {
        if (_grid.SelectedItem is Book book)
            Edit(book);
        else
            MessageBox.Show("ابتدا یک کتاب را انتخاب کنید.");
    }

    private void Edit(Book book)
    {
        var window = new AddEditBookWindow(book) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            try
            {
                _service.Save(window.Result);
                // پاک کردن جستجو تا کتاب جدید حتماً دیده شود
                ApplyPlaceholder(_search, SearchPlaceholder);
                Refresh();
                // انتخاب کتاب تازه‌ذخیره‌شده
                var list = _grid.ItemsSource as System.Collections.IEnumerable;
                if (list != null)
                {
                    foreach (Book b in list)
                    {
                        if (b.Id == window.Result.Id ||
                            (window.Result.Id == 0 && b.Title == window.Result.Title && b.ISBN == window.Result.ISBN))
                        {
                            _grid.SelectedItem = b;
                            _grid.ScrollIntoView(b);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) { VH.Error(ex); }
        }
    }

    private void ImportExcel(object sender, RoutedEventArgs e)
    {
        var window = new BookImportWindow { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            ApplyPlaceholder(_search, SearchPlaceholder);
            Refresh();
            MessageBox.Show(Window.GetWindow(this),
                $"ورود گروهی انجام شد. تعداد: {window.ImportedCount}",
                "ورود از Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void DeleteSelected(object sender, RoutedEventArgs e) => DoDelete();

    private void DoDelete()
    {
        try
        {
            if (_grid.SelectedItem is not Book book)
            {
                MessageBox.Show("ابتدا یک کتاب را انتخاب کنید.");
                return;
            }
            if (MessageBox.Show($"کتاب «{book.Title}» حذف شود؟", "تأیید حذف",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _service.Delete(book.Id);
            Refresh();
        }
        catch (Exception ex) { VH.Error(ex); }
    }

    private void Refresh()
    {
        var all = _service.All();
        var q = GetSearchQuery();
        if (!string.IsNullOrWhiteSpace(q))
        {
            all = all.Where(x =>
                Contains(x.Title, q) || Contains(x.Author, q) ||
                Contains(x.Publisher, q) || Contains(x.ISBN, q)).ToList();
        }
        _grid.ItemsSource = all;
    }

    private static bool Contains(string value, string q) =>
        (value ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase);
}
