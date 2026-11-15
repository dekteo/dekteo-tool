using System.Windows;
using System.Windows.Media;
using Dekotube.Services;
namespace Dekotube.Views;
// Geometry is built only when the screen region changes. Frames change transforms/opacity.
internal sealed class RobotScreenVisual : FrameworkElement
{
    private DrawingGroup _drawing=new();
    private readonly DrawingGroup[] _dots=new DrawingGroup[3],_bars=new DrawingGroup[5],_glitches=new DrawingGroup[3];
    private readonly ScaleTransform[] _barScales=new ScaleTransform[5];
    private readonly TranslateTransform[] _glitchMoves=new TranslateTransform[3];
    private object? _key;
    public void Configure(double aw,double ah,ScreenEffectOptions s,ImageSource? source=null,RobotScreenOptions? options=null,Brush? faceBrush=null)
    {
        options??=new();var d=options.DotsPlacement;var b=options.BarsPlacement;var g=options.GlitchPlacement;
        object key=(aw,ah,s.X,s.Y,s.Width,s.Height,s.CornerRadius,s.CurveTop,s.CurveBottom,s.CurveLeft,s.CurveRight,s.SkewX,s.SkewY,source,d.X,d.Y,d.Size,b.X,b.Y,b.Size,g.X,g.Y,g.Size,faceBrush);
        if(Equals(key,_key))return;_key=key;
        double x=Math.Clamp(s.X,0,.99)*aw,y=Math.Clamp(s.Y,0,.99)*ah,w=Math.Clamp(s.Width,.01,1)*aw,h=Math.Clamp(s.Height,.01,1)*ah;
        var zone=new Rect(x,y,w,h);_drawing=new DrawingGroup{ClipGeometry=ScreenZoneGeometry.Create(zone,s),Transform=new SkewTransform(s.SkewX,s.SkewY,x+w/2,y+h/2)};
        DrawingGroup Part(RobotPartPlacement p,double cx,double cy){var part=new DrawingGroup{Transform=new TransformGroup{Children=new TransformCollection{new ScaleTransform(p.Size,p.Size,x+w*cx,y+h*cy),new TranslateTransform((p.X-cx)*w,(p.Y-cy)*h)}}};_drawing.Children.Add(part);return part;}
        var dotsPart=Part(d,.5,.83);var barsPart=Part(b,.87,.57);var glitchPart=Part(g,.5,.5);
        DrawingGroup Shape(DrawingGroup parent,Brush brush,Geometry geometry){geometry.Freeze();var group=new DrawingGroup{Opacity=0};group.Children.Add(new GeometryDrawing(brush,null,geometry));parent.Children.Add(group);return group;}
        for(int i=0;i<3;i++)_dots[i]=Shape(dotsPart,Brushes.Turquoise,new EllipseGeometry(new Point(x+w*(.44+i*.06),y+h*.83),w*.012,w*.012));
        for(int i=0;i<5;i++){double bx=x+w*(.82+i*.021),bottom=y+h*.65;_bars[i]=Shape(barsPart,Brushes.Cyan,new RectangleGeometry(new Rect(bx,bottom-h*.16,w*.013,h*.16),w*.004,w*.004));_bars[i].Transform=_barScales[i]=new ScaleTransform(1,1,bx,bottom);}
        for(int i=0;i<3;i++){_glitches[i]=Shape(glitchPart,i==1?Brushes.Magenta:Brushes.Cyan,new RectangleGeometry(new Rect(x+w*.12,y+h*(.27+i*.22),w*.65,h*.012)));_glitches[i].Transform=_glitchMoves[i]=new TranslateTransform();}
        if(source!=null||faceBrush!=null)
        {
            Brush brush=faceBrush??new ImageBrush(source){ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,aw,ah),Stretch=Stretch.Uniform};if(brush.CanFreeze)brush.Freeze();
            for(int i=0;i<3;i++){var strip=new RectangleGeometry(new Rect(x,y+h*(.27+i*.22),w,h*.035));strip.Freeze();_glitches[i].Children.Insert(0,new GeometryDrawing(brush,null,strip));}
        }
        InvalidateVisual();
    }
    public void Animate(RobotScreenMotion m,double time)
    {
        if(_dots[0]==null)return;
        for(int i=0;i<3;i++){_dots[i].Opacity=m.Dots*(.25+.75*Math.Pow(.5+.5*Math.Sin(time*5-i*1.5),2));_glitches[i].Opacity=m.Glitch*.55;_glitchMoves[i].X=Math.Sin(time*53+i*2)*m.Glitch*10;}
        for(int i=0;i<5;i++){_bars[i].Opacity=.8*Math.Clamp(m.Voice*8,0,1);_barScales[i].ScaleY=m.Voice*(.35+.65*(.5+.5*Math.Sin(time*8+i*1.9)));}
    }
    protected override void OnRender(DrawingContext dc)=>dc.DrawDrawing(_drawing);
}
