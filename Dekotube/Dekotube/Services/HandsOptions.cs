using System.IO;
using System.Text.Json;
namespace Dekotube.Services;
public sealed class HandsRig
{
    public List<string> Layers {get;set;} = new();
    public List<string> NormalPaths {get;set;} = new();
    public Dictionary<string,ArmMotionMap> MotionMaps {get;set;}=new();
    [System.Text.Json.Serialization.JsonIgnore]
    public ArmMotionMap? MotionMap=>Layers.Count==1&&MotionMaps.TryGetValue(Layers[0],out var map)?map:null;
    public string NormalPath(int index)=>NormalPaths?.ElementAtOrDefault(index)??"";
    public double CanvasWidth {get;set;}=900;
    public double CanvasHeight {get;set;}=1100;
    public double X {get;set;}
    public double Y {get;set;}=-100.0/900;
    public double Scale {get;set;}=1;
    public double Rotation {get;set;}
    public bool WholeHands {get;set;}
    public bool LayeredArms {get;set;}
    public double Finger1X {get;set;}
    public double Finger1Y {get;set;}
    public double Finger2X {get;set;}
    public double Finger2Y {get;set;}
    public double FingerRadius {get;set;}=24;
    public bool ElbowRig {get;set;}
    public double Elbow1X {get;set;}
    public double Elbow2X {get;set;}
    public double ElbowY {get;set;}
    public double Amount {get;set;}=1;
    public HandsRig Copy(){var c=(HandsRig)MemberwiseClone();c.Layers=Layers.ToList();c.NormalPaths=NormalPaths?.ToList()??new();c.MotionMaps=MotionMaps.ToDictionary(p=>p.Key,p=>p.Value.Copy());return c;}
    public static HandsRig Import(string file)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(file));var root=doc.RootElement;
        var rig=new HandsRig{CanvasWidth=root.GetProperty("CanvasWidth").GetDouble(),CanvasHeight=root.GetProperty("CanvasHeight").GetDouble()};
        if(!double.IsFinite(rig.CanvasWidth)||!double.IsFinite(rig.CanvasHeight)||rig.CanvasWidth<1||rig.CanvasHeight<1||rig.CanvasWidth>16000||rig.CanvasHeight>16000)throw new InvalidDataException("Неверный размер холста.");
        if(root.TryGetProperty("Y",out var y)&&y.TryGetDouble(out var offset)&&double.IsFinite(offset))rig.Y=Math.Clamp(offset,-2,2);
        if(root.TryGetProperty("ElbowRig",out var elbow)&&elbow.ValueKind==JsonValueKind.True){
            rig.ElbowRig=true;
            double Coordinate(string key,double max){double v=root.GetProperty(key).GetDouble();if(!double.IsFinite(v)||v<0||v>max)throw new InvalidDataException("Неверная точка локтя.");return v;}
            rig.Elbow1X=Coordinate("Elbow1X",rig.CanvasWidth);rig.Elbow2X=Coordinate("Elbow2X",rig.CanvasWidth);rig.ElbowY=Coordinate("ElbowY",rig.CanvasHeight);
        }
        if(root.TryGetProperty("WholeHands",out var whole)&&whole.ValueKind==JsonValueKind.True){
            rig.WholeHands=true;
            double Read(string key,double max){double v=root.GetProperty(key).GetDouble();if(!double.IsFinite(v)||v<0||v>max)throw new InvalidDataException("Неверная область деформации.");return v;}
            rig.Finger1X=Read("Finger1X",rig.CanvasWidth);rig.Finger1Y=Read("Finger1Y",rig.CanvasHeight);
            rig.Finger2X=Read("Finger2X",rig.CanvasWidth);rig.Finger2Y=Read("Finger2Y",rig.CanvasHeight);
            rig.FingerRadius=Read("FingerRadius",150);if(rig.FingerRadius<2)throw new InvalidDataException("Слишком малая область деформации.");
        }
        var names=root.GetProperty("Layers").EnumerateArray().Select(x=>x.GetString()??"").ToList();
        rig.LayeredArms=root.TryGetProperty("LayeredArms",out var layered)&&layered.ValueKind==JsonValueKind.True;
        if(rig.LayeredArms?names.Count!=1:rig.WholeHands?names.Count!=3&&names.Count!=5:names.Count!=5)throw new InvalidDataException("Неверное число слоёв набора рук.");
        rig.Layers=names.Select(n=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!,n))).ToList();
        if(rig.Layers.Any(p=>!File.Exists(p)||!p.EndsWith(".png",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Не найдены PNG набора.");
        return rig;
    }
}
public sealed class HandsOptions
{
    public bool Enabled {get;set;}
    public bool DeskMode {get;set;}
    public bool AutoSwitch {get;set;}
    public bool MouseFollowsPosition {get;set;}=true;
    public double TransitionSeconds {get;set;}=.3;
    public HandsRig Gamepad {get;set;}=new();
    public HandsRig Desk {get;set;}=new();
    [System.Text.Json.Serialization.JsonIgnore]
    public HandsRig Current=>DeskMode?Desk:Gamepad;
    public HandsOptions Copy()=>new(){Enabled=Enabled,DeskMode=DeskMode,AutoSwitch=AutoSwitch,MouseFollowsPosition=MouseFollowsPosition,TransitionSeconds=TransitionSeconds,Gamepad=Gamepad.Copy(),Desk=Desk.Copy()};
}
public readonly record struct HandsInput(double LX,double LY,double RX,double RY,double LeftPress,double RightPress,bool Connected,ushort Buttons=0,double LT=0,double RT=0,bool? PointerMoved=null);
public sealed class HandsMotion
{
    public double LX,LY,RX,RY,LeftPress,RightPress;
    public void Reset()=>LX=LY=RX=RY=LeftPress=RightPress=0;
    public void Step(double dt,HandsInput input)
    {
        double a=1-Math.Exp(-Math.Clamp(double.IsFinite(dt)?dt:0,0,.1)/.075);
        double S(double old,double target)=>old+(Math.Clamp(double.IsFinite(target)?target:0,-1,1)-old)*a;
        LX=S(LX,input.LX);LY=S(LY,input.LY);RX=S(RX,input.RX);RY=S(RY,input.RY);LeftPress=S(LeftPress,input.LeftPress);RightPress=S(RightPress,input.RightPress);
    }
}
