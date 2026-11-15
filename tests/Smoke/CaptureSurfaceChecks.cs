using System.Runtime.InteropServices;
using System.Windows.Media;
using Dekotube.Services;
internal static class CaptureSurfaceChecks
{
    [StructLayout(LayoutKind.Sequential)]private struct Info{public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int XPels,YPels;public uint Used,Important;}
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateDIBSection(IntPtr dc,ref Info info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll")]private static extern uint GetGuiResources(IntPtr process,uint flags);
    public static void Run()
    {
        IntPtr dc=CreateCompatibleDC(IntPtr.Zero),bitmap=IntPtr.Zero,old=IntPtr.Zero;
        try{
            var info=new Info{Size=(uint)Marshal.SizeOf<Info>(),Width=128,Height=-72,Planes=1,Bits=32};bitmap=CreateDIBSection(dc,ref info,0,out var bits,IntPtr.Zero,0);if(bitmap==IntPtr.Zero)throw new Exception("DIB creation failed");old=SelectObject(dc,bitmap);
            var data=new byte[128*72*4];for(int y=0;y<72;y++)for(int x=0;x<128;x++){int i=(y*128+x)*4;data[i+(x<64?2:0)]=200;data[i+3]=255;}Marshal.Copy(data,0,bits,data.Length);
            var method=typeof(ScreenLightCapture).GetMethod("CaptureSurface",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
            (Color Left,Color Right,double Ms) Sample() => ((Color,Color,double))method.Invoke(null,new object[]{dc,new ScreenLightOptions{Width=128,Height=72}})!;
            Sample();uint before=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);double time=0;
            for(int i=0;i<30;i++){var colors=Sample();if(colors.Left.R<190||colors.Left.B>10||colors.Right.B<190||colors.Right.R>10)throw new Exception("Native capture colors incorrect");time+=colors.Ms;}
            uint after=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);if(after>before+1)throw new Exception("GDI handle leak");
            Console.WriteLine($"Synthetic DC downsample: colors passed, GDI handles {before} → {after}, mean {time/30:0.00} ms. Not desktop/game performance.");
        }finally{if(old!=IntPtr.Zero)SelectObject(dc,old);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(dc!=IntPtr.Zero)DeleteDC(dc);}
    }
}
