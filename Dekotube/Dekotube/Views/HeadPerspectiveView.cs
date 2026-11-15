using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Dekotube.Views;

// A live 2D visual on one fixed 3D plane: images and eye transforms stay together.
// Geometry changes only with canvas/calibration; animation changes rotation alone.
public sealed class HeadPerspectiveView : Viewport3D
{
    private readonly Viewport2DVisual3D _plane = new();
    private readonly AxisAngleRotation3D _yaw = new(new Vector3D(0,1,0),0);
    private readonly RotateTransform3D _rotation;
    private readonly AxisAngleRotation3D _pitch = new(new Vector3D(1,0,0),0);
    private readonly RotateTransform3D _pitchRotation;
    private readonly Transform3DGroup _pose=new();
    public double MaximumAngle {get;set;}=18;
    private double _aspect = 1, _pivotX = .5, _pivotY = .65;
    public double Yaw { get => _yaw.Angle; set => _yaw.Angle = Math.Clamp(value,-MaximumAngle,MaximumAngle); }
    public double Pitch { get => _pitch.Angle; set => _pitch.Angle = Math.Clamp(value,-MaximumAngle,MaximumAngle); }
    public HeadPerspectiveView()
    {
        IsHitTestVisible=false; ClipToBounds=false;
        Camera=new PerspectiveCamera(new Point3D(0,0,2),new Vector3D(0,0,-1),new Vector3D(0,1,0),2*Math.Atan(.25)*180/Math.PI) { NearPlaneDistance=.1,FarPlaneDistance=10 };
        var material=new DiffuseMaterial(Brushes.White);
        Viewport2DVisual3D.SetIsVisualHostMaterial(material,true);
        _plane.Material=material;
        _rotation=new RotateTransform3D(_yaw);_pitchRotation=new RotateTransform3D(_pitch);
        _pose.Children.Add(_rotation);_pose.Children.Add(_pitchRotation);_plane.Transform=_pose;
        Children.Add(new ModelVisual3D { Content=new AmbientLight(Colors.White) }); Children.Add(_plane);
        Configure(1000,1000,.5,.65);
    }
    public void Attach(FrameworkElement visual) => _plane.Visual=visual;
    public void Detach() => _plane.Visual=null;
    public void Configure(double width,double height,double pivotX,double pivotY)
    {
        _aspect=height/width; _pivotX=Math.Clamp(pivotX,0,1); _pivotY=Math.Clamp(pivotY,0,1);
        var mesh=new MeshGeometry3D {
            Positions=new Point3DCollection { new(-.5,_aspect/2,0),new(-.5,-_aspect/2,0),new(.5,-_aspect/2,0),new(.5,_aspect/2,0) },
            TextureCoordinates=new PointCollection { new(0,0),new(0,1),new(1,1),new(1,0) },
            TriangleIndices=new Int32Collection {0,1,2,0,2,3}
        };
        mesh.Freeze(); _plane.Geometry=mesh;
        _rotation.CenterX=_pivotX-.5; _rotation.CenterY=(.5-_pivotY)*_aspect;
        _pitchRotation.CenterX=_rotation.CenterX;_pitchRotation.CenterY=_rotation.CenterY;
    }
    // Ray-plane inverse, so cursor following uses the same coordinate system as the eyes.
    public Point Unproject(Point normalized)
    {
        var inverse=_pose.Value; inverse.Invert();
        var origin=inverse.Transform(new Point3D(0,0,2));
        var direction=inverse.Transform(new Vector3D(normalized.X-.5,(.5-normalized.Y)*_aspect,-2));
        if(Math.Abs(direction.Z)<1e-8) return normalized;
        var hit=origin+direction*(-origin.Z/direction.Z);
        return new Point(hit.X+.5,.5-hit.Y/_aspect);
    }
}
