
using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LibraryManager.Views
{
    public static class VH
    {
        public static StackPanel Page(Grid g, string title)
        {
            var p = new StackPanel();

            g.Children.Add(p);

            p.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 24,
                Foreground = Brushes.DarkSlateBlue,
                Margin = new Thickness(8)
            });

            return p;
        }

        public static Button Btn(string text, RoutedEventHandler act)
        {
            var b = new Button
            {
                Content = text,
                Style = (Style)Application.Current.FindResource(
                    "ModernButtonStyle")
            };

            b.Click += act;

            return b;
        }

        public static DataGrid Table(Panel p, IEnumerable data)
        {
            var d = new DataGrid
            {
                ItemsSource = data,
                Height = 470
            };

            p.Children.Add(d);

            return d;
        }

        public static TextBox Input(
            Panel p,
            string label,
            string value = "")
        {
            p.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(5)
            });

            var t = new TextBox
            {
                Text = value
            };

            p.Children.Add(t);

            return t;
        }

        public static void Error(Exception e)
        {
            MessageBox.Show(
                e.Message,
                "خطا",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
