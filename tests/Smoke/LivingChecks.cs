using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Dekotube.Services;
using Dekotube.Views;

internal static class LivingChecks
{
    public static void Run(Action<bool,string> assert)
    {
        var original=new AvatarEmotion {Head=new HeadOptions { Enabled=true,PerspectiveEnabled=true,PerspectiveMode="auto",PerspectiveAngle=12,BreathingEnabled=true,BreathingAmount=2,BreathingRate=10 }};
        var saved=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(original))!;
        var copy=original.Duplicate(); copy.Head.PerspectiveAngle=3;
        assert(saved.Head.PerspectiveEnabled && saved.Head.PerspectiveMode=="auto" && saved.Head.BreathingRate==10 && saved.Head.BreathingAmount==2 && original.Head.PerspectiveAngle==12,"Perspective and breathing persist and copy independently");
        assert(!new HeadOptions().PerspectiveEnabled && !new HeadOptions().BreathingEnabled,"Old models keep perspective and breathing disabled");
        bool bounded=true; for(double phase=0;phase<100;phase+=.01) {double b=NaturalMotion.Breath(phase,3);bounded &= b>=0 && b<=3;}
        assert(bounded && NaturalMotion.Breath(0,3)==0 && NaturalMotion.Breath(1,0)==0,"Breathing starts neutral and remains within strength limits");
        var projection=new HeadPerspectiveView();projection.Configure(1000,700,.43,.61);
        var point=new Point(.3,.7);assert((projection.Unproject(point)-point).Length<1e-9,"Neutral perspective preserves cursor coordinates on non-square canvas");
        projection.Yaw=18;
        var rotation=new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,1,0),18),-.07,(.5-.61)*.7,0);
        var world=rotation.Transform(new Point3D(point.X-.5,(.5-point.Y)*.7,0));
        var screen=new Point(.5+world.X*2/(2-world.Z),.5-world.Y*2/(2-world.Z)/.7);
        assert((projection.Unproject(screen)-point).Length<1e-9,"Perspective cursor inverse matches projected location");
        var raw=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{255,255,255,255},4);raw.Freeze();
        var view=new AvatarView();view.ConfigureCharacter(original);
        var body=(Image)view.FindName("BodyImage");body.Source=raw;
        var render=typeof(AvatarView).GetMethod("RenderFrame",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var clock=(Stopwatch)typeof(AvatarView).GetField("_clock",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(view)!;
        typeof(AvatarView).GetField("_lastFrame",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(view,clock.Elapsed.TotalSeconds-.1);
        render.Invoke(view,null);
        view.Measure(new Size(1000,1000));view.Arrange(new Rect(0,0,1000,1000));view.UpdateLayout();
        var surface=(Grid)view.FindName("HeadSurface");var composite=(Grid)view.FindName("Composite");
        assert(surface.Children.Count==1 && surface.Children[0] is HeadPerspectiveView && composite.ActualWidth==1000,"Perspective hosts existing head with its canonical canvas size");
        var clip=(Canvas)view.FindName("LeftEyeClip");var mask=clip.Clip;
        for(int i=0;i<100;i++) render.Invoke(view,null);
        // A detached UserControl has no presentation layout in this headless probe;
        // the important invariant is that the cached eye geometry remains unchanged.
        assert(ReferenceEquals(mask,clip.Clip),"Perspective and breathing preserve eye masks");
        view.EditEyeRegion(0);assert(surface.Children.Contains(composite) && ((ScaleTransform)view.FindName("BreathScale")).ScaleY==1,"Calibration returns head to flat view and pauses breathing");
        view.EditEyeRegion(null); assert(surface.Children[0] is HeadPerspectiveView,"Leaving calibration restores perspective");
        original.Head.PerspectiveEnabled=false;original.Head.BreathingEnabled=false;view.RefreshVisualSettings();
        assert(surface.Children.Contains(composite) && double.IsNaN(composite.Width) && ((ScaleTransform)view.FindName("BreathScale")).ScaleY==1,"Disabling options restores original 2D layout and neutral scale");
    }
    public static void RenderProbe(string output)
    {
        var row=new Grid { Width=1200,Height=400,Background=new SolidColorBrush(Color.FromRgb(27,32,42)) };
        for(int i=0;i<3;i++) {
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var face=new Grid { Width=400,Height=400 };
            face.Children.Add(new Border { Width=220,Height=260,CornerRadius=new CornerRadius(30),Background=Brushes.Turquoise,BorderBrush=Brushes.White,BorderThickness=new Thickness(4) });
            face.Children.Add(new Rectangle { Width=28,Height=45,Fill=Brushes.MidnightBlue,Margin=new Thickness(-90,-45,0,0) });
            face.Children.Add(new Rectangle { Width=28,Height=45,Fill=Brushes.MidnightBlue,Margin=new Thickness(90,-45,0,0) });
            face.Children.Add(new Rectangle { Width=55,Height=10,Fill=Brushes.MidnightBlue,Margin=new Thickness(0,95,0,0) });
            var projection=new HeadPerspectiveView();projection.Configure(400,400,.5,.65);projection.Attach(face);projection.Yaw=(i-1)*18;
            Grid.SetColumn(projection,i);row.Children.Add(projection);
        }
        row.Measure(new Size(1200,400));row.Arrange(new Rect(0,0,1200,400));row.UpdateLayout();
        var bitmap=new RenderTargetBitmap(1200,400,96,96,PixelFormats.Pbgra32);bitmap.Render(row);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(output);encoder.Save(file);
    }
}
