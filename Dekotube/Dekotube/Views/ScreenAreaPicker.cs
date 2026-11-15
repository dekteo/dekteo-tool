using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Dekotube.Services;
namespace Dekotube.Views;
public sealed class ScreenAreaPicker : Window
{
    [StructLayout(LayoutKind.Sequential)] private struct PixelPoint{public int X,Y;}
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out PixelPoint point);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    public ScreenLightCapture.Rect? Selection {get;private set;}
    public ScreenAreaPicker()
    {
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(40,0,0,0));Topmost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;
        var monitors=ScreenLightCapture.Monitors();int x=monitors.Min(m=>m.Left),y=monitors.Min(m=>m.Top),r=monitors.Max(m=>m.Right),b=monitors.Max(m=>m.Bottom);
        var canvas=new Canvas();Content=canvas;var box=new Rectangle{Stroke=Brushes.Turquoise,StrokeThickness=3,Fill=new SolidColorBrush(Color.FromArgb(35,0,255,255)),IsHitTestVisible=false};canvas.Children.Add(box);
        var hint=new TextBlock {Text="Обведите область игры · Esc — отмена",Background=Brushes.Black,Foreground=Brushes.White,Padding=new Thickness(12),IsHitTestVisible=false};canvas.Children.Add(hint);Canvas.SetLeft(hint,24);Canvas.SetTop(hint,24);
        SourceInitialized+=(_,_)=>SetWindowPos(new WindowInteropHelper(this).Handle,IntPtr.Zero,x,y,r-x,b-y,0x0040);
        PixelPoint? start=null;
        MouseLeftButtonDown+=(_,_)=>{GetCursorPos(out var p);start=p;CaptureMouse();};
        MouseMove+=(_,_)=>{if(start is not {} s)return;GetCursorPos(out var p);var transform=PresentationSource.FromVisual(this)!.CompositionTarget.TransformFromDevice;
            var p1=transform.Transform(new Point(Math.Min(s.X,p.X)-x,Math.Min(s.Y,p.Y)-y));var p2=transform.Transform(new Point(Math.Max(s.X,p.X)-x,Math.Max(s.Y,p.Y)-y));Canvas.SetLeft(box,p1.X);Canvas.SetTop(box,p1.Y);box.Width=p2.X-p1.X;box.Height=p2.Y-p1.Y;};
        MouseLeftButtonUp+=(_,_)=>{if(start is not {} s)return;GetCursorPos(out var p);ReleaseMouseCapture();if(Math.Abs(p.X-s.X)<10||Math.Abs(p.Y-s.Y)<10){start=null;return;}Selection=new(){Left=Math.Min(s.X,p.X),Top=Math.Min(s.Y,p.Y),Right=Math.Max(s.X,p.X),Bottom=Math.Max(s.Y,p.Y)};DialogResult=true;};
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){ReleaseMouseCapture();DialogResult=false;}};
    }
}
