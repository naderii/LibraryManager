using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace LibraryManager.Views;

/// <summary>
/// Helper مشترک برای ساخت سریع UI در Viewها با پشتیبانی RTL و کیبورد.
/// </summary>
public static class VH
{
    public static StackPanel Page(Grid root, string title)
    {
        root.Children.Clear();
        root.RowDefinitions.Clear();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(24, 59, 86)),
            Margin = new Thickness(4, 0, 4, 8)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 12) };
        scroll.Content = panel;
        return panel;
    }

    public static Button Btn(string content, RoutedEventHandler click, bool primary = true)
    {
        var b = new Button
        {
            Content = content,
            Margin = new Thickness(4),
            Padding = new Thickness(16, 10, 16, 10),
            MinWidth = 110,
            Cursor = Cursors.Hand,
            Style = (Style)Application.Current.FindResource(primary ? "PrimaryButton" : "SecondaryButton")
        };
        b.Click += click;
        return b;
    }

    public static TextBox Input(Panel parent, string placeholder)
    {
        var box = new TextBox
        {
            Margin = new Thickness(4, 4, 4, 8),
            Padding = new Thickness(10, 8, 10, 8),
            MinHeight = 38,
            Tag = placeholder
        };
        // Placeholder ساده
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
                box.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        };
        box.Text = placeholder;
        box.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        parent.Children.Add(box);
        return box;
    }

    public static DataGrid Table(Panel parent, object items)
    {
        var g = new DataGrid
        {
            ItemsSource = items as System.Collections.IEnumerable,
            AutoGenerateColumns = true,
            IsReadOnly = true,
            CanUserAddRows = false,
            Height = 380,
            Margin = new Thickness(4, 8, 4, 8)
        };
        parent.Children.Add(g);
        return g;
    }

    public static void Error(Exception ex)
    {
        MessageBox.Show(ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void Info(string msg)
    {
        MessageBox.Show(msg, "اطلاع", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void Success(string msg)
    {
        MessageBox.Show(msg, "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// حرکت فوکوس با Enter به کنترل بعدی (برای فرم‌های کیبوردمحور)
    /// </summary>
    public static void EnableEnterAsTab(Control control)
    {
        control.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && !(control is TextBox { AcceptsReturn: true }))
            {
                e.Handled = true;
                var req = new TraversalRequest(FocusNavigationDirection.Next);
                control.MoveFocus(req);
            }
        };
    }
}
