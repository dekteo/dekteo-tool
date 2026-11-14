namespace VeadotubeMy.Services;
public sealed class RobotPartPlacement
{
    public double X {get;set;}=.5;
    public double Y {get;set;}=.5;
    public double Size {get;set;}=1;
    public RobotPartPlacement Copy()=>(RobotPartPlacement)MemberwiseClone();
}
public sealed class RobotScreenOptions
{
    public RobotPartPlacement DotsPlacement {get;set;}=new(){X=.5,Y=.83};
    public RobotPartPlacement BarsPlacement {get;set;}=new(){X=.87,Y=.57};
    public RobotPartPlacement GlitchPlacement {get;set;}=new();
    public RobotPartPlacement Placement(string key)=>key=="processing"?DotsPlacement:key=="bars"?BarsPlacement:GlitchPlacement;
    public bool Processing {get;set;}
    public bool Glitch {get;set;}
    public bool VoiceBars {get;set;}
    public double QuietSeconds {get;set;}=15;
    public double GlitchInterval {get;set;}=45;
    public double Strength {get;set;}=.5;
    public double BarsGain {get;set;}=2;
    public RobotScreenOptions Copy(){var o=(RobotScreenOptions)MemberwiseClone();o.DotsPlacement=DotsPlacement.Copy();o.BarsPlacement=BarsPlacement.Copy();o.GlitchPlacement=GlitchPlacement.Copy();return o;}
}
public sealed class RobotScreenMotion
{
    private readonly Random _random=new();
    private double _quiet,_processingTime,_glitchTime,_nextGlitch=-1,_testTime;
    private string? _test;
    public double Dots {get;private set;}
    public double Glitch {get;private set;}
    public double Voice {get;private set;}
    public bool Testing=>_testTime>0;
    public void Test(string kind){_test=kind;_testTime=kind=="glitch"?1:4;}
    public void Reset(){_quiet=_processingTime=_glitchTime=_testTime=Dots=Glitch=Voice=0;_nextGlitch=-1;_test=null;}
    public void Step(double dt,double time,RobotScreenOptions o,bool talking,double level)
    {
        dt=Math.Clamp(double.IsFinite(dt)?dt:0,0,.1);
        _testTime=Math.Max(0,_testTime-dt);if(_testTime==0)_test=null;
        _quiet=talking?0:_quiet+dt;
        if(o.Processing&&!talking&&_quiet>=Math.Clamp(o.QuietSeconds,5,120)&&_processingTime==0){_processingTime=3;_quiet=0;}
        if(talking||!o.Processing)_processingTime=0;
        _processingTime=Math.Max(0,_processingTime-dt);
        double dots=(_processingTime>0||_test=="processing")&&!talking?1:0;
        Dots+=(dots-Dots)*(1-Math.Exp(-dt/.2));if(Dots<.0001)Dots=0;
        if(o.Glitch){if(_nextGlitch<0)_nextGlitch=Math.Clamp(o.GlitchInterval,10,180)*(.7+_random.NextDouble()*.6);_nextGlitch-=dt;if(_nextGlitch<=0){_glitchTime=.22;_nextGlitch=Math.Clamp(o.GlitchInterval,10,180)*(.7+_random.NextDouble()*.6);}}
        else {_glitchTime=0;_nextGlitch=-1;}
        _glitchTime=Math.Max(0,_glitchTime-dt);
        Glitch=(_glitchTime>0||_test=="glitch")?Math.Clamp(o.Strength,0,1):0;
        double voice=_test=="bars"?.25+.55*(.5+.5*Math.Sin(time*5)):o.VoiceBars&&talking?Math.Clamp(level*Math.Clamp(o.BarsGain,.5,8),0,1):0;
        Voice+=(voice-Voice)*(1-Math.Exp(-dt/.08));if(Voice<.0001)Voice=0;
    }
}
