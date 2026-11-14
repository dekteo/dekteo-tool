namespace VeadotubeMy.Services;
public sealed class HandsAutoSwitch
{
    public bool DeskMode {get;private set;}
    private bool? _candidate;private double _stable,_cooldown,_padRecent,_deskRecent;
    public void Reset(bool desk){DeskMode=desk;_candidate=null;_stable=0;_cooldown=0;_padRecent=_deskRecent=0;}
    public static bool Active(HandsInput i)=>Math.Abs(i.LX)>.2||Math.Abs(i.LY)>.2||(i.PointerMoved??(Math.Abs(i.RX)>.12||Math.Abs(i.RY)>.12))||i.LeftPress>.15||i.RightPress>.15;
    public bool Step(double dt,HandsInput pad,HandsInput desk,bool padReady,bool deskReady)
    {
        dt=Math.Clamp(double.IsFinite(dt)?dt:0,0,.1);_cooldown=Math.Max(0,_cooldown-dt);
        _padRecent=padReady&&pad.Connected&&Active(pad)?.22:Math.Max(0,_padRecent-dt);
        _deskRecent=deskReady&&Active(desk)?.22:Math.Max(0,_deskRecent-dt);
        bool p=padReady&&_padRecent>0,d=deskReady&&_deskRecent>0;
        // Hold the present pose on simultaneous input, connection alone, and inactivity.
        bool? next=p==d?null:d;
        if(next==null||next==DeskMode||_cooldown>0){_candidate=null;_stable=0;return false;}
        if(_candidate!=next){_candidate=next;_stable=0;}_stable+=dt;
        if(_stable<.12)return false;
        DeskMode=next.Value;_candidate=null;_stable=0;_cooldown=.7;return true;
    }
}
