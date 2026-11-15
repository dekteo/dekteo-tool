using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dekotube.Views;

internal static class EditorControls
{
    public static StackPanel Section(Panel owner, string title, bool expanded = false)
    {
        var body = new StackPanel { Margin = new Thickness(0,6,0,0) };
        owner.Children.Add(new Border {
            Padding = new Thickness(10), Margin = new Thickness(0,5,0,5), CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(79,99,115)),
            Background = new SolidColorBrush(Color.FromRgb(25,33,43)),
            Child = new Expander { Header = new TextBlock { Text = title, Foreground = Brushes.Turquoise, FontWeight = FontWeights.SemiBold },
                Content = body, IsExpanded = expanded, HorizontalContentAlignment = HorizontalAlignment.Stretch }
        });
        return body;
    }
    public static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 6), Foreground = Brushes.LightGray };
    public static Slider Slider(Panel panel, string label, double value, double min, double max, Action<double> change, string format = "0.00")
    {
        var caption = Label(label + " · " + value.ToString(format));
        panel.Children.Add(caption);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 0, 0, 4) };
        slider.ValueChanged += (_, e) => { caption.Text = label + " · " + e.NewValue.ToString(format); change(e.NewValue); };
        panel.Children.Add(slider);
        return slider;
    }
    public static Button Button(Panel panel, string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 4, 0, 4), MinHeight = 32 };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
        return button;
    }
}
