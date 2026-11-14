namespace VeadotubeMy.Services;
public sealed class AccessoryOptions
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Подвеска";
    public string Path { get; set; } = "";
    public List<string> Frames { get; set; } = new();
    public double FramesPerSecond { get; set; } = 8;
    public bool Intermittent { get; set; }
    public double IntervalMin { get; set; } = 30;
    public double IntervalMax { get; set; } = 90;
    public bool HideBetween { get; set; } = true;
    public bool Visible { get; set; } = true;
    public bool Motion { get; set; } = true;
    public bool PulseGlow {get;set;}
    public double PulseStrength {get;set;}=.6;
    public double PulseSeconds {get;set;}=3;
    public string PulseColor {get;set;}="#20EFFF";
    public bool Metallic { get; set; }
    public double Shine { get; set; } = .4;
    public bool ShineRegion { get; set; }
    public double ShineX { get; set; }
    public double ShineY { get; set; }
    public double ShineWidth { get; set; } = 1;
    public double ShineHeight { get; set; } = 1;
    public bool Head { get; set; }
    public bool Behind { get; set; }
    public double X { get; set; } = .5;
    public double Y { get; set; } = .6;
    public double Width { get; set; } = .12;
    public double HeightScale {get;set;}=1;
    public double Rotation {get;set;}
    public double Skew {get;set;}
    public double Bend {get;set;}
    public double PinX { get; set; } = .5;
    public double PinY { get; set; } = .02;
    public double RigidFrom { get; set; } = .65;
    public double Amount { get; set; } = 8;
    public double Speed { get; set; } = 1;
    public double Damping { get; set; } = .65;
    public static AccessoryOptions AttachedPng(string path, bool head, bool fullCanvas) => new()
    {
        Path=path, Name=System.IO.Path.GetFileNameWithoutExtension(path), Head=head, Motion=false,
        Width=fullCanvas?1:.3, X=fullCanvas?0:.5, Y=fullCanvas?0:(head?.2:.6),
        PinX=fullCanvas?0:.5, PinY=0
    };
    public AccessoryOptions Copy() {var copy=(AccessoryOptions)MemberwiseClone();copy.Frames=Frames?.ToList()??new();return copy;}
}

public sealed class AccessoryAnimation
{
    private double _time,_delay;
    private bool _playing,_preview;
    private readonly Random _random;
    public AccessoryAnimation(int? seed=null){_random=seed.HasValue?new Random(seed.Value):new Random();_delay=-1;}
    public void Preview(){_playing=true;_preview=true;_time=0;}
    public int Step(double dt,int count,AccessoryOptions o)
    {
        if(count<2)return 0;
        double min=Math.Clamp(o.IntervalMin,1,600),max=Math.Clamp(o.IntervalMax,min,600);
        double Delay()=>min+_random.NextDouble()*(max-min);
        if(_delay<0)_delay=Delay();
        dt=double.IsFinite(dt)?Math.Clamp(dt,0,.25):0;
        if(!o.Intermittent&&!_preview){_time+=dt;return (int)(_time*Math.Clamp(o.FramesPerSecond,1,30))%count;}
        if(!_playing){_delay-=dt;if(_delay<=0){_playing=true;_time=0;}else return o.HideBetween?-1:0;}
        _time+=dt;int index=(int)(_time*Math.Clamp(o.FramesPerSecond,1,30));
        if(index<count)return index;
        _time=0;_playing=false;_preview=false;_delay=Delay();return o.HideBetween&&o.Intermittent?-1:0;
    }
}

public sealed class AccessorySwing
{
    public double Angle { get; private set; }
    public bool Moving => Math.Abs(Angle)>.001 || Math.Abs(_velocity)>.001;
    private double _velocity, _time;
    public void Kick(double amount) => _velocity += Math.Clamp(amount,-40,40);
    public void Step(double dt, AccessoryOptions o)
    {
        dt=double.IsFinite(dt)?Math.Clamp(dt,0,.1):0;
        int steps=Math.Max(1,(int)Math.Ceiling(dt/.008));double h=dt/steps;
        for(int i=0;i<steps;i++)
        {
            _time+=h;
            double speed=Math.Clamp(o.Speed,.25,2),w=3*speed;
            double target=o.Motion?Math.Sin(_time*speed*.9)*Math.Clamp(o.Amount,0,25)*.35:0;
            _velocity+=(w*w*(target-Angle)-2*Math.Clamp(o.Damping,.15,1.5)*w*_velocity)*h;
            Angle=Math.Clamp(Angle+_velocity*h,-35,35);
        }
    }
}
