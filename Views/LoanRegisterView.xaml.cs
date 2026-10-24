using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LibraryManager.Data;
using Microsoft.EntityFrameworkCore;
using LibraryManager.Models;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class LoanRegisterView : UserControl
{
    private readonly LoanService _loanSvc = new();
    private readonly MemberService _memberSvc = new();

    private TextBox _memberSearch = null!;
    private TextBox _bookSearch = null!;
    private ListBox _memberList = null!;
    private ListBox _bookList = null!;
    private TextBlock _memberInfo = null!;
    private TextBlock _bookInfo = null!;
    private TextBlock _statusLine = null!;
    private Button _registerBtn = null!;
    private Button _renewBtn = null!;

    private Member _selectedMember;
    private Book _selectedBook;
    private List<Member> _allMembers = new();
    private List<Book> _allBooks = new();

    public LoanRegisterView()
    {
        InitializeComponent();
        BuildUi();
        LoadData();
        Loaded += (_, _) =>
        {
            _memberSearch.Focus();
            SelectAllIfPlaceholder(_memberSearch);
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void BuildUi()
    {
        Root.Children.Clear();
        Root.RowDefinitions.Clear();
        Root.ColumnDefinitions.Clear();

        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel { Margin = new Thickness(4, 0, 4, 14) };
        header.Children.Add(new TextBlock
        {
            Text = "ثبت امانت",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#183B56")
        });
        header.Children.Add(BuildShortcutBar());
        Grid.SetColumnSpan(header, 3);
        Grid.SetRow(header, 0);
        Root.Children.Add(header);

        var memberCard = BuildCard("عضو", "F2", "کد ملی، نام یا تلفن…",
            out _memberSearch, out _memberList, out _memberInfo);
        Grid.SetRow(memberCard, 1);
        Grid.SetColumn(memberCard, 0);
        Root.Children.Add(memberCard);

        var bookCard = BuildCard("کتاب", "F3", "شابک، عنوان یا نویسنده…",
            out _bookSearch, out _bookList, out _bookInfo);
        Grid.SetRow(bookCard, 1);
        Grid.SetColumn(bookCard, 2);
        Root.Children.Add(bookCard);

        var bottom = new Border
        {
            Background = Brush("#F8FAFC"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(bottom, 2);
        Grid.SetColumnSpan(bottom, 3);

        var bottomGrid = new Grid();
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _statusLine = new TextBlock
        {
            Text = "عضو و کتاب را انتخاب کنید.",
            FontSize = 13,
            Foreground = Brush("#475569"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(_statusLine, 0);
        bottomGrid.Children.Add(_statusLine);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _renewBtn = MakeButton("تمدید عضویت", RenewMembership, false);
        _renewBtn.IsEnabled = false;
        _renewBtn.ToolTip = "F5";
        actions.Children.Add(_renewBtn);

        var clearBtn = MakeButton("پاک کردن", (_, _) => ClearForm(), false);
        clearBtn.ToolTip = "Esc";
        actions.Children.Add(clearBtn);

        _registerBtn = MakeButton("ثبت امانت", (_, _) => TryRegister(), true);
        _registerBtn.IsEnabled = false;
        _registerBtn.MinWidth = 140;
        _registerBtn.ToolTip = "Enter";
        actions.Children.Add(_registerBtn);

        Grid.SetColumn(actions, 1);
        bottomGrid.Children.Add(actions);
        bottom.Child = bottomGrid;
        Root.Children.Add(bottom);

        _memberSearch.TextChanged += (_, _) => FilterMembers();
        _bookSearch.TextChanged += (_, _) => FilterBooks();
        _memberList.SelectionChanged += (_, _) => OnMemberSelected();
        _bookList.SelectionChanged += (_, _) => OnBookSelected();

        _memberSearch.PreviewKeyDown += MemberSearch_PreviewKeyDown;
        _bookSearch.PreviewKeyDown += BookSearch_PreviewKeyDown;
        _memberList.PreviewKeyDown += MemberList_PreviewKeyDown;
        _bookList.PreviewKeyDown += BookList_PreviewKeyDown;
    }

    private UIElement BuildShortcutBar()
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        void Chip(string key, string label)
        {
            var border = new Border
            {
                Background = Brush("#EEF2FF"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 8, 4)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = key,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = Brush("#3730A3"),
                Margin = new Thickness(0, 0, 6, 0)
            });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Brush("#475569") });
            border.Child = sp;
            panel.Children.Add(border);
        }
        Chip("F2", "عضو");
        Chip("F3", "کتاب");
        Chip("Enter", "انتخاب / ثبت");
        Chip("F5", "تمدید");
        Chip("Esc", "پاک کردن");
        return panel;
    }

    private Border BuildCard(string title, string shortcutHint, string placeholder,
        out TextBox searchBox, out ListBox list, out TextBlock info)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14)
        };

        var stack = new StackPanel();

        var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#183B56"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var keyBadge = new Border
        {
            Background = Brush("#F1F5F9"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2)
        };
        keyBadge.Child = new TextBlock
        {
            Text = shortcutHint,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B")
        };
        Grid.SetColumn(keyBadge, 1);
        titleRow.Children.Add(keyBadge);
        stack.Children.Add(titleRow);

        var box = new TextBox
        {
            Padding = new Thickness(12, 10, 12, 10),
            MinHeight = 42,
            FontSize = 14,
            Tag = placeholder,
            Margin = new Thickness(0, 0, 0, 8)
        };
        ApplyPlaceholder(box, placeholder);
        stack.Children.Add(box);
        searchBox = box;

        list = new ListBox
        {
            Height = 240,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
            DisplayMemberPath = "Display"
        };
        stack.Children.Add(list);

        info = new TextBlock
        {
            Text = "هنوز انتخاب نشده",
            FontSize = 12,
            Foreground = Brush("#94A3B8"),
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 56,
            LineHeight = 18
        };
        stack.Children.Add(info);

        card.Child = stack;
        return card;
    }

    private static Button MakeButton(string text, RoutedEventHandler handler, bool primary)
    {
        var b = VH.Btn(text, handler, primary);
        b.Margin = new Thickness(6, 0, 0, 0);
        b.Padding = new Thickness(14, 9, 14, 9);
        return b;
    }

    private static SolidColorBrush Brush(string hex) =>
        (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;

    private static void ApplyPlaceholder(TextBox box, string placeholder)
    {
        box.Text = placeholder;
        box.Foreground = Brush("#94A3B8");
        box.GotFocus += (s, e) =>
        {
            if (box.Text == placeholder)
            {
                box.Text = "";
                box.Foreground = Brushes.Black;
            }
        };
        box.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.Text = placeholder;
                box.Foreground = Brush("#94A3B8");
            }
        };
    }

    private static void SelectAllIfPlaceholder(TextBox box)
    {
        if (box.Text != (string)box.Tag)
            box.SelectAll();
    }

    private void LoadData()
    {
        using var db = new LibraryDbContext();
        _allMembers = db.Members.AsNoTracking().OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToList();
        // همیشه تازه از دیتابیس — شامل AvailableCopies بعد از ثبت امانت
        _allBooks = db.Books.AsNoTracking()
            .Where(b => b.AvailableCopies > 0)
            .OrderBy(x => x.Title)
            .ToList();
        FilterMembers();
        FilterBooks();
    }

    private void FilterMembers()
    {
        var q = GetSearchText(_memberSearch);
        var filtered = string.IsNullOrWhiteSpace(q)
            ? _allMembers.Take(50).ToList()
            : _allMembers.Where(m =>
                Has(m.NationalId, q) || Has(m.FirstName, q) || Has(m.LastName, q) ||
                Has(m.PhoneNumber, q) || Has(m.FirstName + " " + m.LastName, q)).Take(50).ToList();
        _memberList.ItemsSource = filtered.Select(m => new MemberItem(m)).ToList();

        // اگر فقط یک نتیجه یا تطابق دقیق کد ملی → خودکار انتخاب نشود تا Enter بزند؛ فقط لیست را آماده کن
        if (filtered.Count == 1 && !string.IsNullOrWhiteSpace(q))
            _memberList.SelectedIndex = 0;
    }

    private void FilterBooks()
    {
        var q = GetSearchText(_bookSearch);
        var qNorm = NormalizeIsbn(q);

        List<Book> filtered;
        if (string.IsNullOrWhiteSpace(q))
        {
            filtered = _allBooks.Take(50).ToList();
        }
        else
        {
            filtered = _allBooks.Where(b =>
                Has(b.Title, q) ||
                Has(b.Author, q) ||
                Has(b.ISBN, q) ||
                (!string.IsNullOrEmpty(qNorm) && NormalizeIsbn(b.ISBN) == qNorm) ||
                (!string.IsNullOrEmpty(qNorm) && NormalizeIsbn(b.ISBN).Contains(qNorm))
            ).Take(50).ToList();

            // اولویت: تطابق دقیق شابک
            var exact = filtered.FirstOrDefault(b => NormalizeIsbn(b.ISBN) == qNorm && qNorm.Length >= 10);
            if (exact != null)
                filtered = new List<Book> { exact }.Concat(filtered.Where(b => b.Id != exact.Id)).ToList();
        }

        _bookList.ItemsSource = filtered.Select(b => new BookItem(b)).ToList();

        // یک نتیجه یا شابک دقیق → انتخاب خودکار در لیست (بدون ثبت)
        if (filtered.Count == 1 && !string.IsNullOrWhiteSpace(q))
            _bookList.SelectedIndex = 0;
        else if (!string.IsNullOrEmpty(qNorm) && qNorm.Length >= 10)
        {
            var exactIdx = filtered.FindIndex(b => NormalizeIsbn(b.ISBN) == qNorm);
            if (exactIdx >= 0)
                _bookList.SelectedIndex = exactIdx;
        }
    }

    private static string NormalizeIsbn(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static string GetSearchText(TextBox box)
    {
        var t = box.Text?.Trim() ?? "";
        return t == (string)box.Tag ? "" : t;
    }

    private static bool Has(string value, string q) =>
        (value ?? "").Contains(q, StringComparison.CurrentCultureIgnoreCase);

    private void OnMemberSelected()
    {
        if (_memberList.SelectedItem is not MemberItem item)
        {
            _selectedMember = null;
            _memberInfo.Text = "هنوز انتخاب نشده";
            _memberInfo.Foreground = Brush("#94A3B8");
            _renewBtn.IsEnabled = false;
            UpdateStatus();
            return;
        }

        _selectedMember = item.Member;
        var can = _loanSvc.CanBorrow(_selectedMember.Id);
        var active = new LibraryDbContext().Loans.Count(x => x.MemberId == _selectedMember.Id && !x.IsReturned);
        var ok = string.IsNullOrEmpty(can);

        _memberInfo.Text =
            $"{_selectedMember.FirstName} {_selectedMember.LastName}\n" +
            $"کد ملی: {_selectedMember.NationalId}   |   امانت فعال: {active}\n" +
            $"انقضا: {_selectedMember.MembershipExpiryDate:yyyy/MM/dd}   |   " +
            (ok ? "مجاز ✓" : can);
        _memberInfo.Foreground = Brush(ok ? "#047857" : "#B91C1C");
        _renewBtn.IsEnabled = true;
        UpdateStatus();
    }

    private void OnBookSelected()
    {
        if (_bookList.SelectedItem is not BookItem item)
        {
            _selectedBook = null;
            _bookInfo.Text = "هنوز انتخاب نشده";
            _bookInfo.Foreground = Brush("#94A3B8");
            UpdateStatus();
            return;
        }

        _selectedBook = item.Book;
        _bookInfo.Text =
            $"{_selectedBook.Title}\n" +
            $"نویسنده: {_selectedBook.Author}\n" +
            $"شابک: {_selectedBook.ISBN}   |   موجود: {_selectedBook.AvailableCopies} از {_selectedBook.TotalCopies}";
        _bookInfo.Foreground = Brush("#047857");
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var canBorrow = _selectedMember != null && string.IsNullOrEmpty(_loanSvc.CanBorrow(_selectedMember.Id));
        var ready = canBorrow && _selectedBook != null;
        _registerBtn.IsEnabled = ready;

        if (ready)
            _statusLine.Text = $"آماده ثبت: «{_selectedBook.Title}» برای {_selectedMember.FirstName} {_selectedMember.LastName} — Enter بزنید";
        else if (_selectedMember != null && !canBorrow)
            _statusLine.Text = "این عضو مجاز به امانت نیست. ابتدا عضویت را تمدید کنید یا جریمه را بپردازید.";
        else if (_selectedMember != null && _selectedBook == null)
            _statusLine.Text = "عضو انتخاب شد. کتاب را با F3 یا شابک پیدا کنید.";
        else if (_selectedBook != null && _selectedMember == null)
            _statusLine.Text = "کتاب انتخاب شد. عضو را با F2 پیدا کنید.";
        else
            _statusLine.Text = "عضو و کتاب را انتخاب کنید.";
    }

    private bool TryRegister()
    {
        if (_selectedMember == null || _selectedBook == null) return false;
        if (!string.IsNullOrEmpty(_loanSvc.CanBorrow(_selectedMember.Id))) return false;

        try
        {
            var memberName = $"{_selectedMember.FirstName} {_selectedMember.LastName}";
            var bookTitle = _selectedBook.Title;
            var loan = _loanSvc.Register(_selectedMember.Id, _selectedBook.Id);

            // رفرش اجباری از دیتابیس تا لیست موجودی به‌روز شود
            ClearForm();
            LoadData();

            _statusLine.Text = $"ثبت شد: «{bookTitle}» برای {memberName} — سررسید {loan.DueDate:yyyy/MM/dd}";
            _statusLine.Foreground = Brush("#047857");

            _memberSearch.Focus();
            return true;
        }
        catch (Exception ex)
        {
            VH.Error(ex);
            LoadData(); // حتی در خطا لیست را تازه کن
            return false;
        }
    }

    private void RenewMembership(object sender, RoutedEventArgs e)
    {
        if (_selectedMember == null) return;
        if (!PaymentDialog.ShowPayment(
                "تمدید عضویت ۱۲ ماهه",
                $"{_selectedMember.FirstName} {_selectedMember.LastName}\nمبلغ: {SettingsService.Fee:N0} تومان"))
            return;
        try
        {
            var id = _selectedMember.Id;
            _memberSvc.Renew(id, 12, PaymentDialog.Method);
            LoadData();
            // انتخاب مجدد همان عضو
            var item = _memberList.Items.Cast<MemberItem>().FirstOrDefault(x => x.Member.Id == id);
            if (item != null)
            {
                _memberList.SelectedItem = item;
                OnMemberSelected();
            }
            _statusLine.Text = "عضویت تمدید شد. اکنون می‌توانید امانت ثبت کنید.";
            _statusLine.Foreground = Brush("#047857");
        }
        catch (Exception ex) { VH.Error(ex); }
    }

    private void ClearForm()
    {
        _selectedMember = null;
        _selectedBook = null;
        _memberList.SelectedItem = null;
        _bookList.SelectedItem = null;
        _memberInfo.Text = "هنوز انتخاب نشده";
        _memberInfo.Foreground = Brush("#94A3B8");
        _bookInfo.Text = "هنوز انتخاب نشده";
        _bookInfo.Foreground = Brush("#94A3B8");
        ApplyPlaceholder(_memberSearch, (string)_memberSearch.Tag);
        ApplyPlaceholder(_bookSearch, (string)_bookSearch.Tag);
        FilterMembers();
        FilterBooks();
        _renewBtn.IsEnabled = false;
        _statusLine.Foreground = Brush("#475569");
        UpdateStatus();
    }

    // ——— کیبورد سراسری ———
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            _memberSearch.Focus();
            SelectAllIfPlaceholder(_memberSearch);
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            _bookSearch.Focus();
            SelectAllIfPlaceholder(_bookSearch);
            e.Handled = true;
        }
        else if (e.Key == Key.F5 && _renewBtn.IsEnabled)
        {
            RenewMembership(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ClearForm();
            _memberSearch.Focus();
            e.Handled = true;
        }
    }

    // ——— جستجوی عضو: Enter = انتخاب و رفتن به کتاب ———
    private void MemberSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (_memberList.Items.Count > 0)
            {
                _memberList.Focus();
                if (_memberList.SelectedIndex < 0) _memberList.SelectedIndex = 0;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            EnsureMemberSelectedFromSearch();
            if (_selectedMember != null)
            {
                _bookSearch.Focus();
                SelectAllIfPlaceholder(_bookSearch);
            }
        }
    }

    private void MemberList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnMemberSelected();
            if (_selectedMember != null)
            {
                _bookSearch.Focus();
                SelectAllIfPlaceholder(_bookSearch);
            }
        }
    }

    // ——— جستجوی کتاب: Enter = انتخاب و در صورت آمادگی ثبت ———
    private void BookSearch_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (_bookList.Items.Count > 0)
            {
                _bookList.Focus();
                if (_bookList.SelectedIndex < 0) _bookList.SelectedIndex = 0;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            EnsureBookSelectedFromSearch();
            if (_selectedBook != null)
            {
                // اگر عضو هم آماده است → ثبت مستقیم (مناسب بارکدخوان)
                if (_registerBtn.IsEnabled)
                    TryRegister();
                else if (_selectedMember == null)
                {
                    _memberSearch.Focus();
                    SelectAllIfPlaceholder(_memberSearch);
                }
            }
        }
    }

    private void BookList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnBookSelected();
            if (_registerBtn.IsEnabled)
                TryRegister();
            else if (_selectedMember == null)
            {
                _memberSearch.Focus();
                SelectAllIfPlaceholder(_memberSearch);
            }
        }
    }

    /// <summary>اگر چیزی انتخاب نشده، اولین/دقیق‌ترین نتیجه را انتخاب کن.</summary>
    private void EnsureMemberSelectedFromSearch()
    {
        if (_memberList.SelectedItem == null && _memberList.Items.Count > 0)
            _memberList.SelectedIndex = 0;
        OnMemberSelected();
    }

    private void EnsureBookSelectedFromSearch()
    {
        if (_bookList.SelectedItem == null && _bookList.Items.Count > 0)
            _bookList.SelectedIndex = 0;
        OnBookSelected();
    }

    private sealed class MemberItem
    {
        public Member Member { get; }
        public string Display => $"{Member.LastName}، {Member.FirstName}  —  {Member.NationalId}";
        public MemberItem(Member m) => Member = m;
    }

    private sealed class BookItem
    {
        public Book Book { get; }
        public string Display => $"{Book.Title}  —  {Book.Author}  ({Book.AvailableCopies} موجود)";
        public BookItem(Book b) => Book = b;
    }
}
