using System.Windows;
using System.Windows.Media;

namespace VeadotubeMy.Views;

// Build the rounded strip only when its size/appearance changes. During speech
// small slices bend through affine transforms; layout and clip geometry stay fixed.
internal sealed class ProceduralMouthStrip : FrameworkElement
{
    private const int Count = 48;
    private DrawingGroup _drawing = new();
    private readonly MatrixTransform[] _transforms = new MatrixTransform[Count];
    private Color _color = Colors.Cyan;
    private double _roundness = .5, _bend;
    private ImageSource? _texture;
    private bool _highlight;
    private double[] _curve = [0,0,.5,0,1,0];
    public void SetSurface(ImageSource? texture, bool highlight)
    {
        if (ReferenceEquals(_texture, texture) && _highlight == highlight) return;
        _texture = texture; _highlight = highlight; Rebuild();
    }
    public ProceduralMouthStrip() { SizeChanged += (_, _) => Rebuild(); }
    public void Configure(Color color, double roundness)
    {
        if (_color == color && _roundness == roundness) return;
        _color = color; _roundness = roundness; Rebuild();
    }
    private void Rebuild()
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var shape = new RectangleGeometry(new Rect(0, 0, w, h), _texture == null ? Math.Min(w,h)*_roundness : 0, _texture == null ? Math.Min(w,h)*_roundness : 0);
        Brush brush = _texture != null ? new ImageBrush(_texture) { Stretch = Stretch.Fill, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0,0,w,h) }
            : _highlight ? new LinearGradientBrush(new GradientStopCollection { new(_color,0), new(Colors.White,.45), new(_color,1) }, new Point(0,0), new Point(0,1))
            : new SolidColorBrush(_color);
        brush.Freeze();
        _drawing = new DrawingGroup();
        for (int i = 0; i < Count; i++)
        {
            var clip = new RectangleGeometry(new Rect(i*w/Count-.15, -1, w/Count+.3, h+2));
            var geometry = Geometry.Combine(shape, clip, GeometryCombineMode.Intersect, null);
            geometry.Freeze();
            var group = new DrawingGroup { Transform = _transforms[i] = new MatrixTransform() };
            group.Children.Add(new GeometryDrawing(brush, null, geometry));
            _drawing.Children.Add(group);
        }
        Bend(_bend); InvalidateVisual();
    }
    public void Bend(double amount)
    {
        _bend = amount;
        double w = ActualWidth;
        if (w <= 0) return;
        for (int i = 0; i < Count; i++)
        {
            if (_transforms[i] == null) continue;
            double x = (i+.5)*w/Count, t = x/w;
            double ax = 2*(_curve[0]+_curve[4]-2*_curve[2]), bx = 4*_curve[2]-3*_curve[0]-_curve[4];
            double ay = 2*(_curve[1]+_curve[5]-2*_curve[3]), by = 4*_curve[3]-3*_curve[1]-_curve[5];
            double slopeX = 2*ax*t+bx;
            double slopeY = ((2*ay*t+by)*ActualHeight + 4*amount*(1-2*t))/w;
            double newX = (ax*t*t+bx*t+_curve[0])*w;
            double shiftY = (ay*t*t+by*t+_curve[1])*ActualHeight+4*amount*t*(1-t);
            _transforms[i].Matrix = new Matrix(slopeX, slopeY, 0, 1, newX-slopeX*x, shiftY-slopeY*x);
        }
    }
    public void SetCurve(double[] curve) { _curve = curve; }
    protected override void OnRender(DrawingContext dc) => dc.DrawDrawing(_drawing);
}
