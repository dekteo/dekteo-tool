using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class LayeredRigChecks
{
    public static void Run()
    {
        var app=new Dekotube.App();app.InitializeComponent();
        string root=Path.GetFullPath("artifacts/layered-rig-v1"),pack=Path.Combine(root,"HandsSample");
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS: "+name);}
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var c=new AvatarEmotion();c.Head.Enabled=true;c.Head.BodyImagePath=Path.Combine(pack,"01-body.png");
        c.MouthLayer.Enabled=true;c.MouthLayer.BasePath=Path.Combine(pack,"02-head.png");
        c.Hands.Gamepad=HandsRig.Import(Path.Combine(pack,"hands-pack.json"));
        c.Hands.Desk=HandsRig.Import(Path.Combine(pack,"Desk/hands-pack.json"));c.Hands.Enabled=true;
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        scene.Measure(new Size(1174,1339));scene.Arrange(new Rect(0,0,1174,1339));scene.UpdateLayout();
        var host=(Canvas)typeof(AvatarView).GetField("_handsHost",flags)!.GetValue(scene)!;
        var effect=(LayeredArmsEffect)typeof(AvatarView).GetField("_layeredArmsEffect",flags)!.GetValue(scene)!;
        var images=(Image[])typeof(AvatarView).GetField("_handsImages",flags)!.GetValue(scene)!;
        var draw=typeof(AvatarView).GetMethod("RenderHands",flags)!;
        byte[] Capture(FrameworkElement element,string name)
        {
            element.UpdateLayout();int w=(int)Math.Ceiling(element.ActualWidth),h=(int)Math.Ceiling(element.ActualHeight);
            var b=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);b.Render(element);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(Path.Combine(root,name+".png")))png.Save(f);
            var bytes=new byte[w*h*4];b.CopyPixels(bytes,w*4,0);return bytes;
        }
        foreach(bool desk in new[]{false,true})
        {
            string mode=desk?"desk":"gamepad";c.Hands.DeskMode=desk;scene.SelectHandsSet(desk);scene.UpdateLayout();
            Check(host.Visibility==Visibility.Visible&&images[0].Effect==effect,"Layered pose imports into AvatarView: "+mode);
            effect.Set(new(),new(),new(),desk);var neutral=Capture(host,mode+"-arms-neutral");Capture(scene,mode+"-runtime-neutral");
            effect.Set(new(12,8),new(desk?-5:12,8),new(4,4),desk,desk?new():new(3,2),desk?new():new(-3,2));
            var moved=Capture(host,mode+"-arms-moved");Capture(scene,mode+"-runtime-moved");
            Check(!neutral.SequenceEqual(moved),"GPU deformation changes rendered hands: "+mode);
            int fixedBytes=(int)(host.ActualHeight*765/1339)*(int)Math.Ceiling(host.ActualWidth)*4;
            Check(neutral.AsSpan(0,fixedBytes).SequenceEqual(moved.AsSpan(0,fixedBytes)),"Shoulder pixels stay fixed: "+mode);
            effect.Set(new(),new(),new(),desk);Check(neutral.SequenceEqual(Capture(host,mode+"-arms-reset")),"Exact neutral reset: "+mode);
            scene.TestHands();draw.Invoke(scene,new object[]{.1});
            Check(!neutral.SequenceEqual(Capture(host,mode+"-arms-demo")),"AvatarView demo drives shader: "+mode);
            c.Hands.Enabled=false;scene.StopHandsTest();host.Visibility=Visibility.Visible;
            Check(neutral.SequenceEqual(Capture(host,mode+"-arms-stopped")),"Stopping demo with live input off clears motion: "+mode);c.Hands.Enabled=true;
        }
        var rt=LayeredFingerMotion.Map(new(0,0,0,0,0,0,true,RT:1),false);
        Check(rt.Press==new Point(4,0)&&rt.RightThumb==new Point(),"RT bends only character right index");
        var button=LayeredFingerMotion.Map(new(0,0,0,0,0,0,true,Buttons:0x1000),false);
        Check(button.Press==new Point()&&button.RightThumb.Y==3&&button.LeftThumb==new Point(),"A moves thumb, not index");
        var dpad=LayeredFingerMotion.Map(new(0,0,0,0,0,0,true,Buttons:8),false);
        Check(dpad.LeftThumb.X==2&&dpad.RightThumb==new Point(),"D-pad targets opposite thumb");
        File.WriteAllText(Path.Combine(root,"verification.txt"),"PASS: both packs render through AvatarView; shader movement, fixed shoulder pixels, exact reset, demo/stop and independent index/thumb mapping. Hardware and OBS not verified.");
    }
}
