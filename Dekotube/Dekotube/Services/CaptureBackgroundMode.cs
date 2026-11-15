using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Dekotube.Services;

// Keep the existing capture HWND alive; never resize, hide or move it off-screen.
public sealed class CaptureBackgroundMode : IDisposable
{
    private readonly Window _window;
    private readonly Action _prepare;
    private HwndSource? _source;
    public bool Enabled { get; set; }
    public CaptureBackgroundMode(Window window,Action prepare)
    {
        _window=window;_prepare=prepare;
        window.SourceInitialized+=Initialized;
        if(new WindowInteropHelper(window).Handle!=IntPtr.Zero)Attach();
    }
    private void Initialized(object? sender,EventArgs e)=>Attach();
    private void Attach(){_source=HwndSource.FromHwnd(new WindowInteropHelper(_window).Handle);_source?.AddHook(Hook);}
    public static bool IsMinimizeCommand(int message,IntPtr command)=>message==0x0112 && (command.ToInt64()&0xFFF0)==0xF020;
    private IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(Enabled&&IsMinimizeCommand(message,wParam)){handled=true;_window.Dispatcher.BeginInvoke(new Action(SendBehind));}
        return IntPtr.Zero;
    }
    public void SendBehind()
    {
        if(!Enabled)return;
        _prepare();
        var hwnd=new WindowInteropHelper(_window).Handle;
        if(hwnd==IntPtr.Zero)return;
        // HWND_BOTTOM also removes topmost status. No activation, movement or resizing.
        SetWindowPos(hwnd,new IntPtr(1),0,0,0,0,0x0001|0x0002|0x0010);
    }
    public void Dispose(){_window.SourceInitialized-=Initialized;_source?.RemoveHook(Hook);_source=null;}
    [DllImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
}
