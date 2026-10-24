using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibraryManager.Services;

namespace LibraryManager.Views;

public partial class ReportsView : UserControl
{
    private readonly ReportService _reports = new();
    private StackPanel _content = null!;

    public ReportsView()
    {
        InitializeComponent();
        BuildUi();
    }

    private void BuildUi()
    {
        var page = VH.Page(Root, "گزارش‌ها — " + SettingsService.LibraryName);
        page.Children.Add(new TextBlock
        {
            Text = "خلاصه وضعیت و گزارش‌های تفصیلی کتابخانه",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(4, -6, 4, 12),
            FontSize = 12
        });

        // کارت‌های خلاصه
        try
        {
            var inv = _reports.Inventory();
            var lateCount = _reports.Late().Count;
            var unpaid = 0;
            using (var db = new Data.LibraryDbContext())
            {
                unpaid = db.Loans.Count(l => l.IsReturned && !l.FinePaid);
            }

            var cardsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            void Card(string title, string value, string colorHex)
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(colorHex)!;
                var b = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16),
                    Margin = new Thickness(4, 4, 4, 8),
                    MinWidth = 160
                };
                var s = new StackPanel();
                s.Children.Add(new TextBlock
                {
                    Text = title,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    FontSize = 12
                });
                s.Children.Add(new TextBlock
                {
                    Text = value,
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = brush,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                b.Child = s;
                cardsPanel.Children.Add(b);
            }

            Card("عنوان‌های کتاب", inv.TotalTitles.ToString("N0"), "#183B56");
            Card("نسخه کل", inv.TotalCopies.ToString("N0"), "#176B87");
            Card("موجود", inv.AvailableCopies.ToString("N0"), "#0F766E");
            Card("امانت‌رفته", inv.OnLoanCopies.ToString("N0"), "#B45309");
            Card("معوق", lateCount.ToString("N0"), "#B91C1C");
            Card("جریمه پرداخت‌نشده", unpaid.ToString("N0"), "#9F1239");
            page.Children.Add(cardsPanel);
        }
        catch (Exception ex)
        {
            page.Children.Add(new TextBlock { Text = "خطا در خلاصه: " + ex.Message, Foreground = Brushes.Red });
        }

        // دکمه‌های گزارش
        var btnPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        btnPanel.Children.Add(VH.Btn("امانت‌های فعال", (_, _) => ShowActiveLoans()));
        btnPanel.Children.Add(VH.Btn("کتاب‌های هر عضو", (_, _) => ShowMemberBooks(), false));
        btnPanel.Children.Add(VH.Btn("موجودی تفصیلی", (_, _) => ShowInventory(), false));
        btnPanel.Children.Add(VH.Btn("معوق‌ها", (_, _) => ShowOverdue(), false));
        btnPanel.Children.Add(VH.Btn("محبوب‌ترین‌ها", (_, _) => ShowPopular(), false));
        btnPanel.Children.Add(VH.Btn("آمار مالی", (_, _) => ShowFinance(), false));
        page.Children.Add(btnPanel);

        _content = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        page.Children.Add(_content);

        // پیش‌فرض: امانت‌های فعال
        ShowActiveLoans();
    }

    private void ClearContent(string title)
    {
        _content.Children.Clear();
        _content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 59, 86)),
            Margin = new Thickness(4, 0, 4, 10)
        });
    }

    private DataGrid MakeGrid()
    {
        return new DataGrid
        {
            AutoGenerateColumns = true,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4, 0, 4, 8),
            MaxHeight = 420,
            FontSize = 12
        };
    }

    private void ShowActiveLoans()
    {
        ClearContent("امانت‌های فعال — کتاب الان پیش کیست؟");
        try
        {
            var rows = _reports.ActiveLoansDetailed();
            if (rows.Count == 0)
            {
                _content.Children.Add(new TextBlock { Text = "هیچ امانت فعالی وجود ندارد.", Margin = new Thickness(4) });
                return;
            }

            var grid = MakeGrid();
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "عنوان کتاب", Binding = new System.Windows.Data.Binding("BookTitle"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "نویسنده", Binding = new System.Windows.Data.Binding("BookAuthor"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "عضو", Binding = new System.Windows.Data.Binding("MemberName"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "تلفن", Binding = new System.Windows.Data.Binding("MemberPhone"), Width = 100 });
            grid.Columns.Add(new DataGridTextColumn { Header = "تاریخ امانت", Binding = new System.Windows.Data.Binding("LoanDate") { StringFormat = "yyyy/MM/dd" }, Width = 90 });
            grid.Columns.Add(new DataGridTextColumn { Header = "سررسید", Binding = new System.Windows.Data.Binding("DueDate") { StringFormat = "yyyy/MM/dd" }, Width = 90 });
            grid.Columns.Add(new DataGridTextColumn { Header = "روز تأخیر", Binding = new System.Windows.Data.Binding("DaysOverdue"), Width = 70 });
            grid.Columns.Add(new DataGridTextColumn { Header = "جریمه تقریبی", Binding = new System.Windows.Data.Binding("EstimatedFine") { StringFormat = "N0" }, Width = 90 });
            grid.ItemsSource = rows;
            _content.Children.Add(grid);
            _content.Children.Add(new TextBlock
            {
                Text = $"جمع: {rows.Count} امانت فعال — از این تعداد {rows.Count(r => r.DaysOverdue > 0)} مورد معوق است.",
                Margin = new Thickness(4, 6, 4, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });
        }
        catch (Exception ex)
        {
            _content.Children.Add(new TextBlock { Text = "خطا: " + ex.Message, Foreground = Brushes.Red });
        }
    }

    private void ShowMemberBooks()
    {
        ClearContent("کتاب‌های امانت‌گرفته هر عضو");
        try
        {
            var rows = _reports.MembersWithBooks();
            if (rows.Count == 0)
            {
                _content.Children.Add(new TextBlock { Text = "هیچ عضوی کتاب امانت‌گرفته ندارد.", Margin = new Thickness(4) });
                return;
            }

            var grid = MakeGrid();
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "نام عضو", Binding = new System.Windows.Data.Binding("MemberName"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "تلفن", Binding = new System.Windows.Data.Binding("MemberPhone"), Width = 110 });
            grid.Columns.Add(new DataGridTextColumn { Header = "تعداد", Binding = new System.Windows.Data.Binding("BookCount"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "کتاب‌ها", Binding = new System.Windows.Data.Binding("Books"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "نزدیک‌ترین سررسید", Binding = new System.Windows.Data.Binding("EarliestDue") { StringFormat = "yyyy/MM/dd" }, Width = 110 });
            grid.ItemsSource = rows;
            _content.Children.Add(grid);
        }
        catch (Exception ex)
        {
            _content.Children.Add(new TextBlock { Text = "خطا: " + ex.Message, Foreground = Brushes.Red });
        }
    }

    private void ShowInventory()
    {
        ClearContent("موجودی تفصیلی کتاب‌ها");
        try
        {
            var inv = _reports.Inventory();
            var books = _reports.Books();

            var summary = new TextBlock
            {
                Text = $"عنوان‌ها: {inv.TotalTitles:N0}  |  نسخه کل: {inv.TotalCopies:N0}  |  موجود: {inv.AvailableCopies:N0}  |  امانت: {inv.OnLoanCopies:N0}  |  بدون موجودی: {inv.ZeroAvailable:N0}",
                Margin = new Thickness(4, 0, 4, 12),
                FontWeight = FontWeights.SemiBold
            };
            _content.Children.Add(summary);

            var grid = MakeGrid();
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "عنوان", Binding = new System.Windows.Data.Binding("Title"), Width = new DataGridLength(2.5, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "نویسنده", Binding = new System.Windows.Data.Binding("Author"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "شابک", Binding = new System.Windows.Data.Binding("ISBN"), Width = 120 });
            grid.Columns.Add(new DataGridTextColumn { Header = "کل", Binding = new System.Windows.Data.Binding("TotalCopies"), Width = 50 });
            grid.Columns.Add(new DataGridTextColumn { Header = "موجود", Binding = new System.Windows.Data.Binding("AvailableCopies"), Width = 55 });
            grid.Columns.Add(new DataGridTextColumn { Header = "قفسه", Binding = new System.Windows.Data.Binding("ShelfLocation"), Width = 80 });
            grid.ItemsSource = books.OrderBy(b => b.AvailableCopies).ThenBy(b => b.Title).ToList();
            _content.Children.Add(grid);
        }
        catch (Exception ex)
        {
            _content.Children.Add(new TextBlock { Text = "خطا: " + ex.Message, Foreground = Brushes.Red });
        }
    }

    private void ShowOverdue()
    {
        ClearContent("امانت‌های معوق (دیرکرد)");
        try
        {
            var rows = _reports.ActiveLoansDetailed().Where(r => r.DaysOverdue > 0).ToList();
            if (rows.Count == 0)
            {
                _content.Children.Add(new TextBlock { Text = "هیچ امانت معوقی وجود ندارد. عالی!", Margin = new Thickness(4), Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)) });
                return;
            }

            var grid = MakeGrid();
            grid.AutoGenerateColumns = false;
            grid.Columns.Add(new DataGridTextColumn { Header = "عنوان کتاب", Binding = new System.Windows.Data.Binding("BookTitle"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "عضو", Binding = new System.Windows.Data.Binding("MemberName"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "تلفن", Binding = new System.Windows.Data.Binding("MemberPhone"), Width = 100 });
            grid.Columns.Add(new DataGridTextColumn { Header = "سررسید", Binding = new System.Windows.Data.Binding("DueDate") { StringFormat = "yyyy/MM/dd" }, Width = 90 });
            grid.Columns.Add(new DataGridTextColumn { Header = "روز تأخیر", Binding = new System.Windows.Data.Binding("DaysOverdue"), Width = 75 });
            grid.Columns.Add(new DataGridTextColumn { Header = "جریمه تقریبی", Binding = new System.Windows.Data.Binding("EstimatedFine") { StringFormat = "N0" }, Width = 100 });
            grid.ItemsSource = rows;
            _content.Children.Add(grid);

            var totalFine = rows.Sum(r => r.EstimatedFine);
            _content.Children.Add(new TextBlock
            {
                Text = $"جمع جریمه تقریبی: {totalFine:N0} تومان",
                Margin = new Thickness(4, 8, 4, 0),
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28))
            });
        }
        catch (Exception ex)
        {
            _content.Children.Add(new TextBlock { Text = "خطا: " + ex.Message, Foreground = Brushes.Red });
        }
    }

    private void ShowPopular()
    {
        ClearContent("محبوب‌ترین کتاب‌ها (بر اساس تعداد امانت)");
        try
        {
            var data = _reports.Popular().Cast<object>().ToList();
            if (data.Count == 0)
            {
                _content.Children.Add(new TextBlock { Text = "هنوز امانتی ثبت نشده.", Margin = new Thickness(4) });
                return;
            }

            var grid = MakeGrid();
            grid.ItemsSource = data;
            _content.Children.Add(grid);
        }
        catch (Exception ex)
        {
            _content.Children.Add(new TextBlock { Text = "خطا: " + ex.Message, Foreground = Brushes.Red });
        }
    }

    private void ShowFinance()
    {
        ClearContent("آمار مالی");
        try
        {
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var yearStart = new DateTime(now.Year, 1, 1);

            var (cashMonth, cardMonth) = _reports.Finance(monthStart, now);
            var (cashYear, cardYear) = _reports.Finance(yearStart, now);
            var (cashAll, cardAll) = _reports.Finance(DateTime.MinValue, now);

            void Row(string period, decimal cash, decimal card)
            {
                var total = cash + card;
                _content.Children.Add(new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14),
                    Margin = new Thickness(4, 4, 4, 6),
                    Child = new TextBlock
                    {
                        Text = $"{period}\nنقد: {cash:N0} تومان  |  کارت: {card:N0} تومان  |  جمع: {total:N0} تومان",
                        FontSize = 13,
                        LineHeight = 22
                    }
                });
            }

            Row("این ماه", cashMonth, cardMonth);
            Row("امسال", cashYear, cardYear);
            Row("کل دوره", cashAll, cardAll);
        }
        catch (Exception ex)
        {
            _content.Children.Add(new TextBlock { Text = "خطا: " + ex.Message, Foreground = Brushes.Red });
        }
    }
}
