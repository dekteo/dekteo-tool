using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views
{
    public partial class ObsView : UserControl
    {
        public ObsView()
        {
            InitializeComponent();
            string hex = CurrentHex();
            TxtHex.Text = hex;
            UpdatePreview(hex);
            ShowRecent();
        }

        private static string CurrentHex()
        {
            if (Application.Current.Resources["SceneBackgroundBrush"] is SolidColorBrush b)
                return ToHex(b.Color);
            return "#00FF00";
        }

        private static string ToHex(Color c) =>
            c.A < 255 ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private void BtnQuickColor_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is string hex)
            {
                TxtHex.Text = hex;
                ApplyColor(hex);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e) => ApplyColor(TxtHex.Text);

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtHex.Text = "#00FF00";
            ApplyColor("#00FF00");
        }

        private void ApplyColor(string hex)
        {
            if (!ConfigurationService.TryParseColor(hex, out var color))
            {
                ShowHint("Неверный HEX-код", false);
                return;
            }

            // Change only the shared scene brush -> central scene repaints, PNG/panels stay untouched.
            Application.Current.Resources["SceneBackgroundBrush"] = new SolidColorBrush(color);

            string normalized = ToHex(color);
            TxtHex.Text = normalized;
            UpdatePreview(normalized);
            bool saved = ConfigurationService.UpdateDefaultProfile(p =>
            {
                p.SceneBackgroundColor = normalized;
                p.RecentBackgroundColors ??= new();
                p.RecentBackgroundColors.Remove(normalized);
                p.RecentBackgroundColors.Insert(0, normalized);
                p.RecentBackgroundColors = p.RecentBackgroundColors.Take(8).ToList();
            });
            ShowRecent();
            if (!saved) { ShowHint("Цвет изменён, но сохранить его не удалось.", false); return; }

            ShowHint("Цвет фона применён", true);
        }

        private void ShowRecent()
        {
            RecentColors.Children.Clear();
            foreach (var hex in ConfigurationService.LoadOrCreateDefault().RecentBackgroundColors ?? new())
            {
                if (!ConfigurationService.TryParseColor(hex, out var color)) continue;
                var button = new Button { Width = 28, Height = 28, Margin = new Thickness(0, 0, 6, 0), Background = new SolidColorBrush(color), ToolTip = hex, Style = (Style)FindResource("SwatchButtonStyle") };
                button.Click += (_, _) => ApplyColor(hex);
                RecentColors.Children.Add(button);
            }
        }
        private static Color Hsv(double hue, double saturation, double value)
        {
            double c = value * saturation, x = c * (1 - Math.Abs(hue / 60 % 2 - 1)), m = value - c;
            var (r, g, b) = hue switch
            {
                < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x),
                < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x)
            };
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
        private void Hue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Palette != null) Palette.Background = new SolidColorBrush(Hsv(e.NewValue, 1, 1));
        }
        private void PickPalette(System.Windows.Input.MouseEventArgs e)
        {
            var point = e.GetPosition(Palette);
            var color = Hsv(Hue.Value, Math.Clamp(point.X / Palette.ActualWidth, 0, 1), 1 - Math.Clamp(point.Y / Palette.ActualHeight, 0, 1));
            TxtHex.Text = ToHex(color); UpdatePreview(TxtHex.Text);
            Application.Current.Resources["SceneBackgroundBrush"] = new SolidColorBrush(color);
        }
        private void Palette_Down(object sender, System.Windows.Input.MouseButtonEventArgs e) { Palette.CaptureMouse(); PickPalette(e); }
        private void Palette_Move(object sender, System.Windows.Input.MouseEventArgs e) { if (Palette.IsMouseCaptured) PickPalette(e); }
        private void Palette_Up(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (!Palette.IsMouseCaptured) return; PickPalette(e); Palette.ReleaseMouseCapture(); ApplyColor(TxtHex.Text); }

        private void UpdatePreview(string hex)
        {
            if (ConfigurationService.TryParseColor(hex, out var color))
                PreviewSwatch.Background = new SolidColorBrush(color);
        }

        private void ShowHint(string text, bool success)
        {
            TxtHint.Text = text;
            TxtHint.Foreground = success
                ? new SolidColorBrush(Color.FromRgb(6, 226, 235))
                : new SolidColorBrush(Color.FromRgb(231, 111, 81));
        }
    }
}
