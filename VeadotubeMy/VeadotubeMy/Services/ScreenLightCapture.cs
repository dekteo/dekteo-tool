using System.Runtime.InteropServices;
using System.Windows.Media;
namespace VeadotubeMy.Services;

// Captures only while explicitly enabled. No images are stored or sent anywhere.
public static class ScreenLightCapture
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Info {public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int XPels,YPels;public uint Used,Important;}
    private delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
    [DllImport("user32.dll")]private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")]private static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleBitmap(IntPtr dc,int w,int h);
    [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern int SetStretchBltMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll")]private static extern bool StretchBlt(IntPtr dest,int x,int y,int w,int h,IntPtr src,int sx,int sy,int sw,int sh,uint rop);
    [DllImport("gdi32.dll")]private static extern int GetDIBits(IntPtr dc,IntPtr bitmap,uint start,uint lines,byte[] data,ref Info info,uint usage);
    public static List<Rect> Monitors(){var list=new List<Rect>();EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr m,IntPtr d,ref Rect r,IntPtr p)=>{list.Add(r);return true;},IntPtr.Zero);return list;}
    public static (Color Left,Color Right) Average(byte[] bgra,int width,int height)
    {
        if(width<2||height<1||bgra.Length<width*height*4)throw new ArgumentException("Invalid pixels");
        Color Half(int start,int end){long r=0,g=0,b=0,n=0;for(int y=0;y<height;y++)for(int x=start;x<end;x++){int i=(y*width+x)*4;b+=bgra[i];g+=bgra[i+1];r+=bgra[i+2];n++;}return Color.FromRgb((byte)(r/n),(byte)(g/n),(byte)(b/n));}
        return(Half(0,width/2),Half(width/2,width));
    }
    public static (Color Left,Color Right,double Ms) Capture(ScreenLightOptions o)
    {
        if(o.Width<1||o.Height<1||o.Width>32768||o.Height>32768)throw new ArgumentException("Выберите область экрана.");
        IntPtr screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)throw new InvalidOperationException("Захват экрана недоступен.");
        try{return CaptureSurface(screen,o);}finally{ReleaseDC(IntPtr.Zero,screen);}
    }
    private static (Color Left,Color Right,double Ms) CaptureSurface(IntPtr screen,ScreenLightOptions o)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();IntPtr dc=IntPtr.Zero,bitmap=IntPtr.Zero,old=IntPtr.Zero;
        try{
            dc=CreateCompatibleDC(screen);bitmap=CreateCompatibleBitmap(screen,64,36);
            if(screen==IntPtr.Zero||dc==IntPtr.Zero||bitmap==IntPtr.Zero)throw new InvalidOperationException("Захват экрана недоступен.");
            old=SelectObject(dc,bitmap);SetStretchBltMode(dc,4);
            if(!StretchBlt(dc,0,0,64,36,screen,o.X,o.Y,o.Width,o.Height,0x00CC0020))throw new InvalidOperationException("Не удалось захватить область. Попробуйте оконный режим игры.");
            SelectObject(dc,old);old=IntPtr.Zero;
            var info=new Info{Size=(uint)Marshal.SizeOf<Info>(),Width=64,Height=-36,Planes=1,Bits=32};var pixels=new byte[64*36*4];
            if(GetDIBits(dc,bitmap,0,36,pixels,ref info,0)!=36)throw new InvalidOperationException("Не удалось прочитать цвета экрана.");
            var colors=Average(pixels,64,36);return(colors.Left,colors.Right,watch.Elapsed.TotalMilliseconds);
        }finally{if(old!=IntPtr.Zero&&dc!=IntPtr.Zero)SelectObject(dc,old);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(dc!=IntPtr.Zero)DeleteDC(dc);}
    }
}
