using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Dekotube.Services;

namespace Dekotube.Views;

public partial class AvatarView
{
    private object? _screenGeometryKey;
    private void RenderScreenEffect(double time)
    {
        var s = _character?.ScreenEffect;
        if (s == null || _character == null || (!s.Enabled && !s.Scanlines && !s.Flicker)) { ScreenEffectCanvas.Visibility = Visibility.Collapsed; return; }
        ScreenEffectCanvas.Visibility = Visibility.Visible;
        ScreenEffectCanvas.Width = Artwork.Width; ScreenEffectCanvas.Height = Artwork.Height;
        double x = Math.Clamp(s.X, 0, .99) * Artwork.Width, y = Math.Clamp(s.Y, 0, .99) * Artwork.Height;
        double w = Math.Clamp(s.Width, .01, 1) * Artwork.Width, h = Math.Clamp(s.Height, .01, 1) * Artwork.Height;
        object geometryKey = (x,y,w,h,s.CornerRadius,s.CurveTop,s.CurveBottom,s.CurveLeft,s.CurveRight);
        if (!Equals(_screenGeometryKey,geometryKey))
        {
            ScreenEffectCanvas.Clip = ScreenZoneGeometry.Create(new Rect(x,y,w,h),s);
            _screenGeometryKey=geometryKey;
        }
        ScreenEffectCanvas.RenderTransform = new SkewTransform(Math.Clamp(s.SkewX, -12, 12), Math.Clamp(s.SkewY, -12, 12), x + w/2, y + h/2);
        double flicker = s.Flicker ? 1 - Math.Clamp(s.FlickerAmount, 0, .5) * (.5 + .5 * (Math.Sin(time * 7.0) * .5 + .5)) : 1;
        ScreenEffectCanvas.Opacity = 1;
        ScreenFlicker.Width=Artwork.Width;ScreenFlicker.Height=Artwork.Height;ScreenFlicker.Opacity=1-flicker;
        double strip = Math.Max(8, w * .16);
        ScreenReflection.Visibility = s.Enabled ? Visibility.Visible : Visibility.Collapsed;
        double padding = Math.Min(w,h)*.26;
        ScreenReflection.Width = strip; ScreenReflection.Height = h + padding*2;
        double travel = w + padding*2 + strip * 2;
        Canvas.SetLeft(ScreenReflection, x - padding - strip + ((time * Math.Clamp(s.Speed, .01, .5)) % 1.0) * travel);
        Canvas.SetTop(ScreenReflection, y - padding);
        ScreenReflection.Fill = new LinearGradientBrush(
            new GradientStopCollection { new GradientStop(Color.FromArgb(0,255,255,255),0), new GradientStop(Color.FromArgb((byte)(Math.Clamp(s.Opacity,0,.6)*255),255,255,255),.5), new GradientStop(Color.FromArgb(0,255,255,255),1) }, 0);
        ScreenGlow.Width=w; ScreenGlow.Height=h; Canvas.SetLeft(ScreenGlow,x); Canvas.SetTop(ScreenGlow,y); ScreenGlow.Opacity=0;
        ScreenScanlines.Width=w+padding*2; ScreenScanlines.Height=h+padding*2+8; Canvas.SetLeft(ScreenScanlines,x-padding); Canvas.SetTop(ScreenScanlines,y-padding-4 + ((time*s.ScanlineSpeed)%4)); ScreenScanlines.Opacity=s.Scanlines ? Math.Clamp(s.ScanlineOpacity,0,.5) : 0;
        var scanBrush = new DrawingBrush(new GeometryDrawing(Brushes.Transparent, new Pen(new SolidColorBrush(Color.FromArgb(220,0,0,0)), 1), new LineGeometry(new Point(0,0),new Point(10,0)))) { TileMode=TileMode.Tile, Viewport=new Rect(0,0,10,4), ViewportUnits=BrushMappingMode.Absolute, Stretch=Stretch.None };
        ScreenScanlines.Fill = scanBrush;
    }
}
