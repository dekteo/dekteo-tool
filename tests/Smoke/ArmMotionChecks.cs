using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;
internal static class ArmMotionChecks
{
    public static void Run(Action<bool,string> check)
    {
        var app=new Dekotube.App();app.InitializeComponent();
        var map=new ArmMotionMap();var part=map.Parts[0];
        part.Shoulder=new(.3,.2);part.Elbow=new(.3,.45);part.Wrist=new(.6,.8);part.Feather=0;
        part.Area.WholeLayer=true;
        check(ArmMotionMap.AlongArm(part.Shoulder,part)==0&&ArmMotionMap.AlongArm(part.Wrist,part)>.999,"Shoulder fixed and wrist fully movable");
        check(ArmMotionMap.AlongArm(part.Elbow,part)>0&&ArmMotionMap.AlongArm(part.Elbow,part)<1,"Movement blends smoothly through elbow");
        var texture=map.Bake(256,256);var weights=ClothingRenderer.Pixels(texture[0]);
        check(weights[(40*256+77)*4+2]==0&&weights[(210*256+160)*4+2]>240,"Baked GPU weights preserve fixed shoulder and moving wrist");
        var bytes=new byte[256*256*4];for(int y=0;y<256;y++)for(int x=0;x<256;x++){int i=(y*256+x)*4;bytes[i]=(byte)(x%20<10?220:40);bytes[i+1]=(byte)(y%20<10?220:40);bytes[i+2]=100;bytes[i+3]=255;}
        var source=BitmapSource.Create(256,256,96,96,PixelFormats.Bgra32,null,bytes,1024);source.Freeze();
        var effect=new CustomArmsEffect();effect.Configure(map,256,256);var image=new Image{Source=source,Width=256,Height=256,Effect=effect};
        image.Measure(new Size(256,256));image.Arrange(new Rect(0,0,256,256));image.UpdateLayout();
        byte[] Capture(){var bitmap=new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32);bitmap.Render(image);return ClothingRenderer.Pixels(bitmap);}
        var neutral=Capture();effect.Set(new(25,12),new(),new());var moved=Capture();
        check(!neutral.SequenceEqual(moved),"Custom shader actually moves rendered wrist pixels");
        check(neutral.AsSpan(0,35*256*4).SequenceEqual(moved.AsSpan(0,35*256*4)),"Custom shader leaves pixels above shoulder unchanged");
        effect.Set(new(),new(),new());check(neutral.SequenceEqual(Capture()),"Custom motion returns exactly to neutral");
        var masked=new ArmMotionMap();masked.Parts[2].Area.Mask.Add(new(){Size=.2,Points=new(){new Point(.5,.5)}});
        effect.Configure(masked,256,256);effect.Set(new(),new(),new(20,0));
        check(!neutral.SequenceEqual(Capture()),"Painted finger area reacts to its press channel");
        effect.Set(new(),new(),new(0,20));check(neutral.SequenceEqual(Capture()),"Unpainted finger channel leaves image unchanged");
        var c=new AvatarEmotion();c.Hands.Gamepad.Layers=new(){"costume-a.png"};c.Hands.Gamepad.LayeredArms=true;c.Hands.Gamepad.MotionMaps["costume-a.png"]=map;c.Hands.Gamepad.MotionMaps["costume-b.png"]=masked;
        var saved=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!;
        check(saved.Hands.Gamepad.MotionMap!.Parts[0].Elbow==part.Elbow,"Costume rig and anchors survive save/restart");
        saved.Hands.Gamepad.Layers[0]="costume-b.png";check(saved.Hands.Gamepad.MotionMap!.Parts[2].Area.Mask.Count==1,"Changing PNG selects its own calibration");
        var copy=c.Duplicate();copy.Hands.Gamepad.MotionMap!.Parts[0].Wrist=new(.1,.1);check(c.Hands.Gamepad.MotionMap!.Parts[0].Wrist==part.Wrist,"Duplicating costume keeps independent calibration");
        var window=new ArmMotionWindow(source,map,false,c.Hands.Gamepad.MotionMaps);window.Show();window.UpdateLayout();
        window.Result.Parts[0].Wrist=new(.9,.9);check(map.Parts[0].Wrist==part.Wrist,"Editor draft does not modify original before saving");
        Directory.CreateDirectory("artifacts/motion-editor-v1");
        var shot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(window);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(shot));using(var file=File.Create("artifacts/motion-editor-v1/editor.png"))png.Save(file);window.Close();
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        var images=(Image[])typeof(AvatarView).GetField("_handsImages",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(scene)!;
        check(images[0].Effect is CustomArmsEffect,"AvatarView uses selected PNG calibration");
        c.Hands.Gamepad.MotionMap!.Enabled=false;scene.RefreshHands();check(images[0].Effect is LayeredArmsEffect,"Disabling custom calibration restores legacy animation");
    }
}
