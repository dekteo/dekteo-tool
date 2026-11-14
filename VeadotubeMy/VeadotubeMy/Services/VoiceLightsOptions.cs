namespace VeadotubeMy.Services;
public sealed class VoiceLightPoint
{
    public double X {get;set;}
    public double Y {get;set;}
    public double Size {get;set;}=1;
    public string? Color {get;set;}
    public VoiceLightPoint Copy()=>(VoiceLightPoint)MemberwiseClone();
}
public sealed class VoiceLightsOptions
{
    public bool Enabled {get;set;}
    public double Diameter {get;set;}=.012;
    public double Gain {get;set;}=6;
    public double Brightness {get;set;}=1;
    public double Glow {get;set;}=10;
    public string Color {get;set;}="#20EFFF";
    public List<VoiceLightPoint> Points {get;set;}=Defaults();
    public static List<VoiceLightPoint> Defaults()=>new(){new(){X=.25,Y=.56},new(){X=.275,Y=.56},new(){X=.30,Y=.56},new(){X=.75,Y=.56},new(){X=.725,Y=.56},new(){X=.70,Y=.56}};
    public void Normalize(){if(Points==null||Points.Count!=6||Points.Any(p=>p==null))Points=Defaults();}
    public VoiceLightsOptions Copy(){var o=(VoiceLightsOptions)MemberwiseClone();o.Points=Points?.Select(p=>p.Copy()).ToList()??Defaults();return o;}
}
public sealed class VoiceLightsMotion
{
    public double[] Levels {get;}=new double[3];
    public double TestRemaining {get;private set;}
    public bool Moving=>TestRemaining>0||Levels.Any(x=>x>.0001);
    public void Test()=>TestRemaining=6;
    public void Reset(){TestRemaining=0;Array.Clear(Levels);}
    public void Step(double dt,VoiceLightsOptions o,bool talking,double level)
    {
        dt=double.IsFinite(dt)?Math.Clamp(dt,0,.1):0;
        double value=TestRemaining>0?(.5-.5*Math.Cos((6-TestRemaining)*Math.PI)):o.Enabled&&talking?Math.Clamp(level*Math.Clamp(o.Gain,.5,20),0,1):0;
        TestRemaining=Math.Max(0,TestRemaining-dt);
        for(int i=0;i<3;i++){double threshold=.08+i*.3;double target=Math.Clamp((value-threshold)/.12,0,1);Levels[i]+=(target-Levels[i])*(1-Math.Exp(-dt/(target>Levels[i]?.045:.2)));if(Levels[i]<.0001)Levels[i]=0;}
    }
}
