using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using LibraryManager.Services;
using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace LibraryManager.Views;

public partial class ReportsView : UserControl
{
    private readonly ComboBox _reportChoices;
    private readonly DataGrid _grid;

    public ReportsView()
    {
        InitializeComponent();

        var panel = VH.Page(Root, "گزارش‌ها");
        var choices = new[]
        {
            "فهرست کامل کتاب‌ها",
            "کتاب‌های موجود و امانت‌رفته",
            "امانت‌های جاری",
            "اعضا",
            "پرتکرارترین کتاب‌ها"
        };

        _reportChoices = new ComboBox
        {
            ItemsSource = choices,
            SelectedIndex = 0,
            Margin = new Thickness(5)
        };
        panel.Children.Add(_reportChoices);

        _grid = VH.Table(panel, new ReportService().Books());
        panel.Children.Add(VH.Btn("نمایش", ShowReport));
        panel.Children.Add(VH.Btn("خروجی PDF", ExportPdf));
    }

    private void ShowReport(object? sender, RoutedEventArgs e)
    {
        var reports = new ReportService();
        IEnumerable rows;

        // Use a common non-generic IEnumerable type because each report returns
        // a different collection type (books, loans, members, or anonymous rows).
        switch (_reportChoices.SelectedIndex)
        {
            case 0:
                rows = reports.Books();
                break;
            case 1:
                rows = reports.Books()
                    .Select(book => new
                    {
                        عنوان = book.Title,
                        کل = book.TotalCopies,
                        موجود = book.AvailableCopies,
                        امانت = book.TotalCopies - book.AvailableCopies
                    })
                    .ToList();
                break;
            case 2:
                rows = reports.Current();
                break;
            case 3:
                rows = reports.Members();
                break;
            default:
                rows = reports.Popular();
                break;
        }

        _grid.ItemsSource = rows;
    }

    private void ExportPdf(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = "LibraryReport.pdf"
            };

            if (dialog.ShowDialog() != true)
                return;

            var rows = _grid.ItemsSource?.Cast<object>()
                .Select(item => item.ToString() ?? string.Empty)
                .ToList() ?? new List<string>();

            Document.Create(document => document.Page(page =>
            {
                page.Margin(25);
                page.DefaultTextStyle(style => style.FontFamily("Tahoma").FontSize(10));
                page.Header().AlignRight().Text("گزارش کتابخانه — " + _reportChoices.SelectedItem);
                page.Content().Column(column =>
                {
                    foreach (var row in rows)
                        column.Item().AlignRight().Text(row);
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("صفحه ");
                    text.CurrentPageNumber();
                });
            })).GeneratePdf(dialog.FileName);

            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            VH.Error(ex);
        }
    }
}
