using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VeadotubeMy.Views;

public partial class AvatarView
{
    private readonly Rectangle _leftDigitalTop = DigitalMask();
    private readonly Rectangle _leftDigitalBottom = DigitalMask();
    private readonly Rectangle _rightDigitalTop = DigitalMask();
    private readonly Rectangle _rightDigitalBottom = DigitalMask();
    private readonly Rectangle _leftDigitalLine = DigitalLine();
    private readonly Rectangle _rightDigitalLine = DigitalLine();

    private static Rectangle DigitalMask() => new() { Fill = new SolidColorBrush(Color.FromRgb(4, 12, 16)), IsHitTestVisible = false, Visibility = Visibility.Collapsed, RadiusX = 8, RadiusY = 8 };
    private static Rectangle DigitalLine() => new() { Fill = new SolidColorBrush(Color.FromRgb(70, 235, 255)), IsHitTestVisible = false, Visibility = Visibility.Collapsed, Height = 2 };

    private void InitializeDigitalBlink()
    {
        foreach (var item in new[] { _leftDigitalTop, _leftDigitalBottom, _leftDigitalLine }) Canvas.SetZIndex(item, 30);
        foreach (var item in new[] { _rightDigitalTop, _rightDigitalBottom, _rightDigitalLine }) Canvas.SetZIndex(item, 30);
        LeftEyeClip.Children.Add(_leftDigitalTop); LeftEyeClip.Children.Add(_leftDigitalBottom); LeftEyeClip.Children.Add(_leftDigitalLine);
        RightEyeClip.Children.Add(_rightDigitalTop); RightEyeClip.Children.Add(_rightDigitalBottom); RightEyeClip.Children.Add(_rightDigitalLine);
    }

    private void ApplyDigitalBlink(Canvas clip, Image pupil, int phase, double elapsed, int durationMs, bool left)
    {
        var p = _character?.Pupils;
        if (p == null) return;
        double width = clip.ActualWidth > 0 ? clip.ActualWidth : clip.Width;
        double height = clip.ActualHeight > 0 ? clip.ActualHeight : clip.Height;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        bool closing = _pupilEyesClosed;
        double closeSeconds = Math.Clamp(p.DigitalBlinkSpeedMs, 120, 1500) / 1000.0;
        double openSeconds = Math.Clamp(p.DigitalBlinkSpeedMs * .65, 90, 900) / 1000.0;
        double amount = closing ? Math.Clamp(elapsed / closeSeconds, 0, 1) : 1 - Math.Clamp(elapsed / openSeconds, 0, 1);
        amount = amount * amount * (3 - 2 * amount);
        var top = left ? _leftDigitalTop : _rightDigitalTop;
        var bottom = left ? _leftDigitalBottom : _rightDigitalBottom;
        var line = left ? _leftDigitalLine : _rightDigitalLine;
        var scale = left ? LeftPupilBlinkScale : RightPupilBlinkScale;
        scale.ScaleX = 1; scale.ScaleY = 1 - amount * Math.Clamp(p.DigitalBlinkCompression, .1, .95);
        pupil.Opacity = 1 - amount * .18;
        var sprite = pupil.Parent as Grid;
        double pupilWidth = sprite?.Width > 0 ? sprite.Width : width * .25;
        double pupilHeight = sprite?.Height > 0 ? sprite.Height : height * .45;
        double pupilLeft = sprite != null && double.IsFinite(Canvas.GetLeft(sprite)) ? Canvas.GetLeft(sprite) : (width - pupilWidth) / 2;
        double pupilTop = sprite != null && double.IsFinite(Canvas.GetTop(sprite)) ? Canvas.GetTop(sprite) : (height - pupilHeight) / 2;
        if (sprite?.RenderTransform is TransformGroup group)
        {
            var move = group.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (move != null) { pupilLeft += move.X; pupilTop += move.Y; }
        }
        double cover = pupilHeight * amount * .46;
        top.Width = bottom.Width = line.Width = pupilWidth;
        top.Height = bottom.Height = cover;
        top.RadiusX = bottom.RadiusX = pupilWidth / 2;
        top.RadiusY = bottom.RadiusY = Math.Max(1, cover / 2);
        Canvas.SetLeft(top, pupilLeft); Canvas.SetLeft(bottom, pupilLeft); Canvas.SetLeft(line, pupilLeft);
        Canvas.SetTop(top, pupilTop); Canvas.SetTop(bottom, pupilTop + pupilHeight - cover);
        Canvas.SetTop(line, pupilTop + pupilHeight / 2 - 1);
        // The screen style is intentionally pupil-only. Masks/line caused visible rectangular zones
        // on characters whose screen has gradients, so keep them hidden while the centered squash is used.
        top.Visibility = bottom.Visibility = line.Visibility = Visibility.Collapsed;
        line.Opacity = 0;
    }

    private void HideDigitalBlink()
    {
        foreach (var item in new[] { _leftDigitalTop, _leftDigitalBottom, _rightDigitalTop, _rightDigitalBottom, _leftDigitalLine, _rightDigitalLine }) item.Visibility = Visibility.Collapsed;
        LeftPupilBlinkScale.ScaleX = RightPupilBlinkScale.ScaleX = LeftPupilBlinkScale.ScaleY = RightPupilBlinkScale.ScaleY = 1;
        LeftPupilOverlay.Opacity = RightPupilOverlay.Opacity = 1;
    }
}
