using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

internal static class HeadChecks
{
    public static void Run(Action<bool,string> assert)
    {
        var character = new AvatarEmotion { Head = new HeadOptions { Enabled=true,BodyImagePath="body.png",PivotX=.4,Angle=7 } };
        var saved=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        var copy=character.Duplicate(); copy.Head.Angle=2;
        assert(saved.Head.BodyImagePath=="body.png" && saved.Head.PivotX==.4 && character.Head.Angle==7,"Head settings persist and duplicate independently");
        assert(!JsonSerializer.Deserialize<AvatarEmotion>("{}")!.Head.Enabled,"Old characters keep separate head disabled");
        var view=new AvatarView(); view.ConfigureCharacter(character);
        var rotation=(RotateTransform)view.FindName("HeadRotation");
        assert(rotation.Angle==0,"Missing body disables head motion");
        var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{255,255,255,255},4); bitmap.Freeze();
        var body=(Image)view.FindName("BodyImage"); body.Source=bitmap;
        var render=typeof(AvatarView).GetMethod("RenderFrame",BindingFlags.NonPublic|BindingFlags.Instance)!;
        render.Invoke(view,null);
        var composite=(Grid)view.FindName("Composite");
        assert(rotation.Angle!=0 && Math.Abs(rotation.Angle)<=7 && composite.IsAncestorOf((Grid)view.FindName("CurrentStateLayer")) && composite.IsAncestorOf((Canvas)view.FindName("PupilCanvas")) && !composite.IsAncestorOf(body),"Head and eyes share motion while body remains outside head transform");
        var clip=(Canvas)view.FindName("LeftEyeClip"); var mask=clip.Clip;
        for(int i=0;i<100;i++) render.Invoke(view,null);
        assert(ReferenceEquals(mask,clip.Clip),"Head animation preserves eye mask cache");
        character.Head.Angle=0; character.Head.MouseNodEnabled=true;
        character.Head.MouseNodAngle=8;
        assert(HeadNodMotion.Target(0,character.Head)==-8 && HeadNodMotion.Target(.5,character.Head)==0 && HeadNodMotion.Target(1,character.Head)==8,"Mouse vertical position maps to clockwise/counterclockwise angles");
        var nod=typeof(AvatarView).GetField("_mouseNod",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var perspective=(HeadPerspectiveView)typeof(AvatarView).GetField("_perspective",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;
        foreach(var angle in new[]{8d,-8d}){
            nod.SetValue(view,angle);render.Invoke(view,null);
            assert(Math.Sign(rotation.Angle)==Math.Sign(angle) && perspective.Pitch==0,"Mouse tilt rotates head in 2D without perspective pitch");
        }
        character.Head.MouseNodEnabled=false;render.Invoke(view,null);
        assert(rotation.Angle==0,"Disabling mouse tilt clears residual rotation");
        character.HeadScale=.77;character.HeadX=.13;character.HeadY=-.07;
        view.RefreshPlacement();view.Measure(new Size(900,800));view.Arrange(new Rect(0,0,900,800));view.UpdateLayout();
        var surface=(Grid)view.FindName("HeadSurface");var editor=(Canvas)view.FindName("RegionEditor");
        editor.Visibility=Visibility.Visible;view.UpdateLayout();
        var local=new Point(330,240);
        assert((surface.TranslatePoint(local,view)-editor.TranslatePoint(local,view)).Length<.01,"Eye editor matches scaled and translated head including pivot");
        void Wheel(){view.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});}
        view.BeginPlacement(true,()=>{});Wheel();
        assert(Math.Abs(character.HeadScale-.847)<.0001&&character.SceneScale==1&&character.BodyScale==1,"Wheel scales head only");view.EndPlacement();
        view.BeginPlacement(false,()=>{},true);Wheel();
        assert(Math.Abs(character.BodyScale-1.1)<.0001&&character.SceneScale==1&&Math.Abs(character.HeadScale-.847)<.0001,"Wheel scales body only");view.EndPlacement();
        view.BeginPlacement(false,()=>{});Wheel();
        assert(Math.Abs(character.SceneScale-1.1)<.0001,"Whole-character wheel uses scene scale");view.EndPlacement();
        var restored=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        assert(restored.BodyScale==character.BodyScale&&character.Duplicate().HeadScale==character.HeadScale,"Separate layer scales survive serialization and duplication");
        character.Head.Enabled=false; view.RefreshVisualSettings();
        assert(rotation.Angle==0 && ((TranslateTransform)view.FindName("HeadTranslation")).Y==0 && body.Visibility==Visibility.Collapsed,"Disabling layered head restores neutral transform and hides body");
    }
}
