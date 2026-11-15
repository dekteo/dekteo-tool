using System.Windows;
namespace Dekotube.Services;

public sealed class ClothStroke
{
    public List<Point> Points {get;set;}=new();
    public double Size {get;set;}=.04;
    public bool Erase {get;set;}
    public ClothStroke Copy()=>new(){Points=Points.ToList(),Size=Size,Erase=Erase};
}
public sealed class ClothLayer
{
    public List<ClothStroke> Mask {get;set;}=new();
    public bool WholeLayer {get;set;}
    public string PrintPath {get;set;}="";
    public double PrintX {get;set;}=.5;
    public double PrintY {get;set;}=.6;
    public double PrintWidth {get;set;}=.2;
    public double PrintAngle {get;set;}
    public ClothLayer Copy()=>new(){Mask=Mask.Select(s=>s.Copy()).ToList(),WholeLayer=WholeLayer,PrintPath=PrintPath,PrintX=PrintX,PrintY=PrintY,PrintWidth=PrintWidth,PrintAngle=PrintAngle};
}
public sealed class ClothingLook
{
    public string Name {get;set;}="Комплект";
    public string Color {get;set;}="#8C394B";
    public double Brightness {get;set;}=1.6;
    public Dictionary<string,ClothLayer> Layers {get;set;}=new();
    public ClothingLook Copy()=>new(){Name=Name,Color=Color,Brightness=Brightness,Layers=Layers.ToDictionary(k=>k.Key,k=>k.Value.Copy())};
}
public sealed class ClothingOptions
{
    public bool Enabled {get;set;}
    public ClothingLook Current {get;set;}=new();
    public List<ClothingLook> Looks {get;set;}=new();
    public ClothingOptions Copy()=>new(){Enabled=Enabled,Current=Current.Copy(),Looks=Looks.Select(l=>l.Copy()).ToList()};
}
