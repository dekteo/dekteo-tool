using System.Runtime.InteropServices;
namespace Dekotube.Services;
// Read-only input snapshots. Never sends input, records text, or creates global hooks.
public sealed class HandsInputReader
{
    [StructLayout(LayoutKind.Sequential)] private struct Pad{public ushort Buttons;public byte LT,RT;public short LX,LY,RX,RY;}
    [StructLayout(LayoutKind.Sequential)] private struct State{public uint Packet;public Pad Pad;}
    [StructLayout(LayoutKind.Sequential)] private struct Point{public int X,Y;}
    [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] private static extern uint Read(uint id,out State state);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    public static double CursorAxis(double position,double origin,double size)=>Math.Clamp((position-origin)/Math.Max(1,size)*2-1,-1,1);
    private double _retry;private uint _index;private bool _connected,_missing;private Point? _previous;
    public static double Axis(short value){double v=value/32768.0;return Math.Abs(v)<.18?0:Math.Sign(v)*Math.Clamp((Math.Abs(v)-.18)/.82,0,1);}
    public void Reset(){_retry=0;_connected=false;_previous=null;}
    public HandsInput Poll(double dt,bool desk,bool followPosition=true)
    {
        if(desk){
            bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
            var move=0d;var vertical=0d;bool moved=false;
            if(GetCursorPos(out var p)){
                if(_previous is {} old){moved=p.X!=old.X||p.Y!=old.Y;move=Math.Clamp((p.X-old.X)/40d,-1,1);vertical=Math.Clamp((p.Y-old.Y)/40d,-1,1);}
                if(followPosition){move=CursorAxis(p.X,GetSystemMetrics(76),GetSystemMetrics(78));vertical=CursorAxis(p.Y,GetSystemMetrics(77),GetSystemMetrics(79));}
                _previous=p;
            }
            bool typing=false;for(int key=8;key<=0xfe&&!typing;key++)typing=Down(key);
            return new(0,0,move,vertical,typing?1:0,Down(1)||Down(2)?1:0,true,PointerMoved:moved);
        }
        _previous=null;if(_missing)return default;
        try{
            if(_connected&&Read(_index,out var current)==0)return Convert(current.Pad);
            _connected=false;_retry-=dt;if(_retry>0)return default;_retry=1;
            for(uint i=0;i<4;i++)if(Read(i,out var s)==0){_index=i;_connected=true;return Convert(s.Pad);}
        }catch(DllNotFoundException){_missing=true;}catch(EntryPointNotFoundException){_missing=true;}
        return default;
    }
    private static HandsInput Convert(Pad p)=>new(Axis(p.LX),-Axis(p.LY),Axis(p.RX),-Axis(p.RY),Math.Max(p.LT/255d,(p.Buttons&0x014f)!=0?1:0),Math.Max(p.RT/255d,(p.Buttons&0xf280)!=0?1:0),true,p.Buttons,p.LT/255d,p.RT/255d);
}
