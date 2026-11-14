using System.Windows;
using System.Windows.Media;

namespace VeadotubeMy.Services;

public static class ScreenZoneGeometry
{
    // Positive values bow outward. Quadratic control displacement is twice the midpoint displacement.
    public static Geometry Create(Rect b, ScreenEffectOptions s)
    {
        double x=b.X,y=b.Y,w=b.Width,h=b.Height;
        double r=Math.Min(Math.Clamp(s.CornerRadius,0,100),Math.Min(w,h)/2);
        double Bend(double value)=>Math.Clamp(double.IsFinite(value)?value:0,-.25,.25)*Math.Min(w,h)*2;
        var g=new StreamGeometry();
        using(var c=g.Open())
        {
            c.BeginFigure(new Point(x+r,y),true,true);
            c.QuadraticBezierTo(new Point(x+w/2,y-Bend(s.CurveTop)),new Point(x+w-r,y),true,false);
            c.QuadraticBezierTo(new Point(x+w,y),new Point(x+w,y+r),true,false);
            c.QuadraticBezierTo(new Point(x+w+Bend(s.CurveRight),y+h/2),new Point(x+w,y+h-r),true,false);
            c.QuadraticBezierTo(new Point(x+w,y+h),new Point(x+w-r,y+h),true,false);
            c.QuadraticBezierTo(new Point(x+w/2,y+h+Bend(s.CurveBottom)),new Point(x+r,y+h),true,false);
            c.QuadraticBezierTo(new Point(x,y+h),new Point(x,y+h-r),true,false);
            c.QuadraticBezierTo(new Point(x-Bend(s.CurveLeft),y+h/2),new Point(x,y+r),true,false);
            c.QuadraticBezierTo(new Point(x,y),new Point(x+r,y),true,false);
        }
        g.Freeze(); return g;
    }
}
