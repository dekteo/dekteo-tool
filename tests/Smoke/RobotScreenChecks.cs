using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class RobotScreenChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();int count=0;
        void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine(name);}
        var o=new RobotScreenOptions();var m=new RobotScreenMotion();
        void Step(int frames,bool talking=false,double level=0){for(int i=0;i<frames;i++)m.Step(.016,i*.016,o,talking,level);}
        Step(5000);Check(m.Dots==0&&m.Glitch==0&&m.Voice==0,"All effects off by default");
        o.Processing=true;o.QuietSeconds=5;m.Reset();Step(320);Check(m.Dots>0,"Processing starts after quiet interval");
        var dots=m.Dots;m.Step(.016,6,o,true,.3);Check(m.Dots<dots&&m.Dots>0,"Speech fades processing");Step(150,true);Check(m.Dots==0,"Speech clears dots");
        o.Processing=false;m.Reset();m.Test("processing");Step(30);Check(m.Dots>.5,"Processing preview without enabling");
        m.Reset();m.Test("glitch");Step(10);Check(m.Glitch>0&&m.Dots==0,"Glitch independent from processing");Step(100);Check(m.Glitch==0&&!m.Testing,"Glitch preview ends");
        m.Reset();o.VoiceBars=true;Step(30,true,.2);Check(m.Voice>.3&&m.Glitch==0,"Voice level drives bars");Step(100);Check(m.Voice==0,"Bars fade on silence");
        o.VoiceBars=false;m.Test("bars");Step(30);Check(m.Voice>0,"Silent bars preview works");
        var c=new AvatarEmotion();c.RobotScreen.Glitch=true;c.Accessories.Add(new AccessoryOptions{PulseGlow=true,PulseSeconds=4});var copy=c.Duplicate();copy.RobotScreen.Glitch=false;copy.Accessories[0].PulseSeconds=8;
        Check(c.RobotScreen.Glitch&&c.Accessories[0].PulseSeconds==4,"Independent copies");var saved=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!;Check(saved.RobotScreen.Glitch&&saved.Accessories[0].PulseGlow,"New effects persist");
        var scene=new AvatarView();scene.ConfigureCharacter(c);scene.Measure(new Size(500,500));scene.Arrange(new Rect(0,0,500,500));scene.UpdateLayout();
        scene.TestRobotScreen("bars");var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var render=typeof(AvatarView).GetMethod("RenderRobotScreen",flags)!;for(int i=0;i<60;i++)render.Invoke(scene,new object[]{.016,i*.016});
        var visual=(FrameworkElement)typeof(AvatarView).GetField("_robotVisual",flags)!.GetValue(scene)!;var field=visual.GetType().GetField("_drawing",flags)!;var geometry=field.GetValue(visual);
        render.Invoke(scene,new object[]{.016,1.1});Check(ReferenceEquals(geometry,field.GetValue(visual)),"Frames reuse geometry");
        var bitmap=new RenderTargetBitmap(1000,1000,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var data=new byte[1000*1000*4];bitmap.CopyPixels(data,4000,0);Check(data.Where((_,i)=>i%4==3).Any(a=>a>0),"Screen bars render pixels");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"robot-screen.png")))png.Save(file);
        int saves=0;scene.BeginRobotPlacement("bars",()=>saves++);
        var handles=(System.Windows.Controls.Canvas)typeof(AvatarView).GetField("_robotHandles",flags)!.GetValue(scene)!;
        handles.Children.OfType<System.Windows.Controls.Primitives.Thumb>().First().RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(-50,-30));
        Check(c.RobotScreen.BarsPlacement.X==.87,"Screen position stays draft");scene.EndRobotPlacement(false);Check(c.RobotScreen.BarsPlacement.X==.87&&saves==0,"Cancel preserves screen placement");
        scene.BeginRobotPlacement("bars",()=>saves++);handles=(System.Windows.Controls.Canvas)typeof(AvatarView).GetField("_robotHandles",flags)!.GetValue(scene)!;
        handles.Children.OfType<System.Windows.Controls.Primitives.Thumb>().First().RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(-50,-30));
        handles.Children.OfType<System.Windows.Controls.Primitives.Thumb>().Last().RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(10,0));scene.EndRobotPlacement(true);
        Check(c.RobotScreen.BarsPlacement.X<.87&&c.RobotScreen.BarsPlacement.Size>1&&saves==1,"Screen move and size apply once");
        var clone=c.Duplicate();clone.RobotScreen.BarsPlacement.X=0;Check(c.RobotScreen.BarsPlacement.X>0,"Placement copy independent");
        scene.ConfigureCharacter(new AvatarEmotion());var motion=(RobotScreenMotion)typeof(AvatarView).GetField("_robotMotion",flags)!.GetValue(scene)!;Check(!motion.Testing&&motion.Voice==0,"Character switch clears preview");
        Console.WriteLine($"Robot screen checks: {count}; real microphone/OBS not tested.");
    }
}
