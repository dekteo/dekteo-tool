using System.Windows;
namespace Dekotube.Services;
// Output is in camera order: character right = viewer left.
public sealed class LayeredFingerMotion
{
    public Point Press {get;private set;}
    public Point RightThumb {get;private set;}
    public Point LeftThumb {get;private set;}
    public void Reset(){Press=RightThumb=LeftThumb=new();}
    public static (Point Press,Point RightThumb,Point LeftThumb) Map(HandsInput p,bool desk)
    {
        bool B(int mask)=>(p.Buttons&mask)!=0;
        if(desk)return(new(p.RightPress*4,p.LeftPress*4),new(),new());
        return(new(4*Math.Max(p.RT,B(0x200)?1:0),4*Math.Max(p.LT,B(0x100)?1:0)),
            new(3*p.RX+(B(0x2000)?2:B(0x4000)?-2:0),2*p.RY+(B(0x1000)?3:B(0x8000)?-2:B(0x80)?2:0)),
            new(3*p.LX+(B(8)?2:B(4)?-2:0),2*p.LY+(B(2)?2:B(1)?-2:B(0x40)?2:0)));
    }
    public void Step(double dt,HandsInput input,bool desk)
    {
        var target=Map(input,desk);double k=1-Math.Exp(-Math.Clamp(dt,0,.1)/.045);
        Point Smooth(Point a,Point b)=>new(a.X+(b.X-a.X)*k,a.Y+(b.Y-a.Y)*k);
        Press=Smooth(Press,target.Press);RightThumb=Smooth(RightThumb,target.RightThumb);LeftThumb=Smooth(LeftThumb,target.LeftThumb);
    }
}
