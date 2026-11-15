using System.Windows;
using System.Windows.Media;
namespace Dekotube.Views;

// Horizontal bands bend the flexible part; the entire pendant remains one rigid band.
public sealed class AccessorySprite : FrameworkElement
{
    private DrawingGroup _drawing=new();
    private DrawingGroup _flat=new();
    private readonly List<(double Y,MatrixTransform Transform)> _bands=new();
    private ImageSource? _source;
    private ImageBrush? _brush;
    public void SetFrame(ImageSource source){if(ReferenceEquals(_source,source))return;_source=source;if(_brush!=null)_brush.ImageSource=source;}
    private double _pinX=.5,_pinY=.02,_rigid=.65,_angle;
    public AccessorySprite(){SizeChanged+=(_,_)=>Rebuild();}
    public void Configure(ImageSource source,double pinX,double pinY,double rigid)
    {
        pinX=Math.Clamp(pinX,0,1);pinY=Math.Clamp(pinY,0,.95);rigid=Math.Clamp(rigid,pinY+.01,1);
        if(ReferenceEquals(source,_source)&&_pinX==pinX&&_pinY==pinY&&_rigid==rigid)return;
        _source=source;_pinX=pinX;_pinY=pinY;_rigid=rigid;Rebuild();
    }
    private void Rebuild()
    {
        _drawing=new();_flat=new();_bands.Clear();double w=ActualWidth,h=ActualHeight;
        if(_source==null||w<=0||h<=0)return;
        var brush=_brush=new ImageBrush(_source){Stretch=Stretch.Fill,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,w,h)};
        _flat.Children.Add(new GeometryDrawing(brush,null,new RectangleGeometry(new Rect(0,0,w,h))));
        void Band(double start,double end)
        {
            if(end<=start)return;
            var geometry=new RectangleGeometry(new Rect(0,start,w,end-start));geometry.Freeze();
            var transform=new MatrixTransform();var group=new DrawingGroup{Transform=transform};
            group.Children.Add(new GeometryDrawing(brush,null,geometry));_drawing.Children.Add(group);_bands.Add(((start+end)/2,transform));
        }
        Band(0,_pinY*h);
        for(int i=0;i<32;i++)Band((_pinY+(_rigid-_pinY)*i/32)*h,(_pinY+(_rigid-_pinY)*(i+1)/32)*h);
        Band(_rigid*h,h);Bend(_angle);InvalidateVisual();
    }
    public static Point Centerline(double distance,double length,double radians)
    {
        if(distance<=0)return new Point(0,distance);
        double d=Math.Min(distance,length);
        double k=length>0?radians/length:0;
        double x=Math.Abs(k)<1e-8?0:(1-Math.Cos(k*d))/k;
        double y=Math.Abs(k)<1e-8?d:Math.Sin(k*d)/k;
        double tail=Math.Max(0,distance-length);
        return new Point(x+Math.Sin(radians)*tail,y+Math.Cos(radians)*tail);
    }
    public void Bend(double degrees)
    {
        bool wasFlat=Math.Abs(_angle)<1e-6;
        _angle=degrees;
        if(wasFlat!=(Math.Abs(_angle)<1e-6))InvalidateVisual();
        double h=ActualHeight,px=_pinX*ActualWidth,py=_pinY*h,length=(_rigid-_pinY)*h,angle=degrees*Math.PI/180;
        foreach(var band in _bands)
        {
            double d=band.Y-py,theta=d<=0?0:angle*Math.Clamp(d/Math.Max(1e-8,length),0,1);
            var p=Centerline(d,length,angle);double c=Math.Cos(theta),s=Math.Sin(theta);
            band.Transform.Matrix=new Matrix(c,-s,s,c,px+p.X-c*px-s*band.Y,py+p.Y+s*px-c*band.Y);
        }
    }
    protected override void OnRender(DrawingContext dc)=>dc.DrawDrawing(Math.Abs(_angle)<1e-6?_flat:_drawing);
}
