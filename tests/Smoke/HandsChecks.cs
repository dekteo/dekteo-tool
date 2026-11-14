using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class HandsChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();int n=0;void Check(bool value,string text){if(!value)throw new Exception(text);n++;Console.WriteLine(text);}
        var motion=new HandsMotion();motion.Step(.016,new(1,0,-1,0,1,0,true));Check(motion.LX>0&&motion.LX<1&&motion.RX<0,"Input smooths both sticks independently");
        for(int i=0;i<120;i++)motion.Step(.016,default);Check(Math.Abs(motion.LX)<1e-8&&motion.LeftPress<1e-8,"Disconnect settles to neutral");
        Check(HandsInputReader.Axis(1000)==0&&HandsInputReader.Axis(short.MinValue)==-1,"Stick deadzone and range");
        var dir=Path.Combine(AppContext.BaseDirectory,"hands-fixture");Directory.CreateDirectory(dir);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(Brushes.Cyan,null,new Rect(200,800,400,180));
        var bitmap=new RenderTargetBitmap(900,1100,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(dir,"layer.png")))encoder.Save(f);
        {
            var warp=new HandImageWarp(bitmap,200,850,24);var identity=warp.Image;
            byte[] ReadWarp(){var pixels=new byte[900*1100*4];warp.Image.CopyPixels(pixels,900*4,0);return pixels;}
            warp.Update(0,0);var neutral=ReadWarp();warp.Update(3,2);var moved=ReadWarp();
            Check(!neutral.SequenceEqual(moved),"Whole-hand deformation changes actual pixels");
            Check(neutral.Take(700*900*4).SequenceEqual(moved.Take(700*900*4)),"Deformation leaves remote pixels unchanged");
            Check(ReferenceEquals(identity,warp.Image),"Deformation retains one bitmap");
            warp.Update(0,0);Check(neutral.SequenceEqual(ReadWarp()),"Deformation resets exactly without accumulated damage");
            var timer=System.Diagnostics.Stopwatch.StartNew();for(int frame=0;frame<120;frame++)warp.Update(Math.Sin(frame*.1)*2,Math.Cos(frame*.1)*2);timer.Stop();
            Console.WriteLine($"Local warp: {timer.Elapsed.TotalMilliseconds/120:F2} ms/update (one hand)");
        }
        string json=JsonSerializer.Serialize(new{CanvasWidth=900,CanvasHeight=1100,Layers=Enumerable.Repeat("layer.png",5)});string file=Path.Combine(dir,"hands-pack.json");File.WriteAllText(file,json);
        var rig=HandsRig.Import(file);Check(rig.Layers.Count==5&&rig.Layers.All(Path.IsPathFullyQualified),"Pack imports resolved files");
        var c=new AvatarEmotion();c.Hands.Gamepad=rig;c.Hands.Desk=rig.Copy();var copy=c.Duplicate();copy.Hands.Gamepad.Layers[0]="changed";Check(c.Hands.Gamepad.Layers[0]!=copy.Hands.Gamepad.Layers[0],"Character copies keep independent hands");
        var restored=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!;Check(restored.Hands.Gamepad.Layers.Count==5&&!restored.Hands.Enabled,"Optional rig persists disabled by default");
        var scene=new AvatarView();scene.ConfigureCharacter(c);scene.Measure(new Size(900,1100));scene.Arrange(new Rect(0,0,900,1100));scene.UpdateLayout();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var host=(Canvas)typeof(AvatarView).GetField("_handsHost",flags)!.GetValue(scene)!;
        Check(host.Visibility==Visibility.Collapsed,"Disabled hands are hidden");
        int saved=0;double original=c.Hands.Gamepad.X;Check(scene.BeginHandsPlacement(()=>saved++),"Placement opens with valid layers");scene.NudgeHands(10,20);scene.ShapeHands(.8,5);Check(c.Hands.Gamepad.X==original,"Placement edits draft only");scene.EndHandsPlacement(false);Check(c.Hands.Gamepad.Scale==1&&saved==0,"Cancel keeps profile");
        scene.BeginHandsPlacement(()=>saved++);scene.NudgeHands(10,20);scene.ShapeHands(.8,5);scene.EndHandsPlacement(true);Check(c.Hands.Gamepad.X>original&&c.Hands.Gamepad.Scale==.8&&saved==1,"Apply saves placement once");
        var transforms=(TranslateTransform[])typeof(AvatarView).GetField("_handsOffsets",flags)!.GetValue(scene)!;
        var render=typeof(AvatarView).GetMethod("RenderHands",flags)!;scene.TestHands();render.Invoke(scene,new object[]{.05});Check(host.Visibility==Visibility.Visible&&transforms[3].X!=0,"Demo animates without enabling real input");
        var existing=transforms[3];for(int i=0;i<130;i++)render.Invoke(scene,new object[]{.05});Check(host.Visibility==Visibility.Collapsed&&existing.X==0,"Demo ends and restores hidden neutral state");
        Check(ReferenceEquals(existing,transforms[3]),"Animation retains transforms");
        scene.TestHands();scene.ConfigureCharacter(new AvatarEmotion());Check(host.Visibility==Visibility.Collapsed&&transforms.All(t=>t.X==0&&t.Y==0),"Character switch clears demo");
        var auto=new HandsAutoSwitch();auto.Reset(false);
        var connected=new HandsInput(0,0,0,0,0,0,true);var key=new HandsInput(0,0,0,0,1,0,true);var stick=new HandsInput(.8,0,0,0,0,0,true);
        for(int i=0;i<30;i++)auto.Step(.05,connected,default,true,true);
        Check(!auto.DeskMode,"Connection alone never changes pose");
        auto.Step(.04,default,key,true,true);auto.Step(.05,default,default,true,true);auto.Step(.05,default,default,true,true);
        Check(auto.DeskMode,"Brief click or key press can select desk after debounce");
        for(int i=0;i<5;i++)auto.Step(.05,stick,default,true,true);
        Check(auto.DeskMode,"Cooldown prevents immediate bounce");
        for(int i=0;i<30;i++)auto.Step(.05,stick,default,true,true);
        Check(!auto.DeskMode,"Sustained gamepad input returns to pad");
        for(int i=0;i<30;i++)auto.Step(.05,stick,key,true,true);
        Check(!auto.DeskMode,"Simultaneous devices retain pose");
        auto.Reset(false);for(int i=0;i<30;i++)auto.Step(.05,default,key,true,false);
        Check(!auto.DeskMode,"Missing desk images block switching to empty pose");
        auto.Reset(false);for(int i=0;i<30;i++)auto.Step(.05,default,new(0,0,.025,0,0,0,true),true,true);
        Check(!auto.DeskMode,"Mouse jitter stays below activity threshold");
        scene.ConfigureCharacter(c);scene.TestHands();
        typeof(AvatarView).GetMethod("SwitchHandsPose",flags)!.Invoke(scene,null);
        var previous=(Canvas)typeof(AvatarView).GetField("_handsPrevious",flags)!.GetValue(scene)!;
        Check(previous.Opacity==1&&host.Opacity==0,"Pose transition starts with outgoing snapshot");
        render.Invoke(scene,new object[]{.1});Check(host.Opacity>0&&host.Opacity<1&&Math.Abs(host.Opacity+previous.Opacity-1)<1e-8,"Crossfade blends both poses");
        for(int i=0;i<10;i++)render.Invoke(scene,new object[]{.1});Check(host.Opacity==1&&previous.Opacity==0,"Crossfade completes without residual old pose");
        scene.StopHandsTest();
        c.ColorCorrections["body"]=new ColorCorrectionOptions{Enabled=true,Hue=35};scene.ConfigureCharacter(c);scene.RefreshColorCorrection();
        Check(host.Effect is ColorCorrectionEffect && previous.Effect is ColorCorrectionEffect,"Body correction reaches current and outgoing hands");
        c.ColorCorrections["body"].Enabled=false;scene.RefreshColorCorrection();
        Check(host.Effect==null&&previous.Effect==null,"Disabling body correction clears hands effects");
        string sample=Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("DEKTEO_HANDS_SAMPLE_ROOT") ?? "artifacts/gamepad-ready", "character.json"));
        if(File.Exists(sample)){
            var model=JsonSerializer.Deserialize<AvatarEmotion>(File.ReadAllText(sample),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;var folder=Path.GetDirectoryName(sample)!;
            model.MouthLayer.BasePath=Path.Combine(folder,"02-head.png");model.Head.BodyImagePath=Path.Combine(folder,"01-body.png");model.Hands.Gamepad=HandsRig.Import(Path.Combine(folder,"hands-pack.json"));model.Hands.Gamepad.Y=0;model.Hands.Enabled=false;
            string deskPack=Path.Combine(folder,"Desk","hands-pack.json");
            if(File.Exists(deskPack))model.Hands.Desk=HandsRig.Import(deskPack);
            model.Hands.AutoSwitch=false;
            foreach(bool isDesk in File.Exists(deskPack)?new[]{false,true}:new[]{false}){
                model.Hands.DeskMode=isDesk;scene.ConfigureCharacter(model);scene.TestHands();
                int height=(int)model.Hands.Current.CanvasHeight;
                scene.Measure(new Size(900,height));scene.Arrange(new Rect(0,0,900,height));scene.UpdateLayout();
                var preview=new RenderTargetBitmap(900,height,96,96,PixelFormats.Pbgra32);preview.Render(scene);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(preview));using var output=File.Create(Path.Combine(folder,isDesk?"app-preview-desk.png":"app-preview.png"));png.Save(output);
                if(model.Hands.Current.ElbowRig){
                    render.Invoke(scene,new object[]{.1});
                    var rotations=(RotateTransform[])typeof(AvatarView).GetField("_handsRotations",flags)!.GetValue(scene)!;
                    Check(transforms[0].X==0&&transforms[0].Y==0,"Elbow rig keeps device base stationary");
                    Check(transforms[1].X==0&&transforms[1].Y==0&&transforms[2].X==0&&transforms[2].Y==0,"Elbow rig avoids whole-arm translation");
                    if(isDesk){
                        if(model.Hands.Current.WholeHands){
                            var bends=(SkewTransform[])typeof(AvatarView).GetField("_handsBends",flags)!.GetValue(scene)!;
                            Check(bends[2].AngleX!=0&&bends[2].AngleX==bends[4].AngleX,"Mouse and hand share anchored bend");
                            Check(new[]{0d,400d,900d}.All(x=>{var p=new Point(x,bends[4].CenterY);return (bends[4].Transform(p)-p).Length<1e-8;}),"Entire elbow attachment line remains fixed");
                        }else{
                            Check(rotations[2].Angle!=0&&rotations[2].Angle==rotations[4].Angle,"Mouse and click finger share elbow rotation");
                            var anchor=new Point(rotations[2].CenterX,rotations[2].CenterY);
                            Check((rotations[2].Transform(anchor)-anchor).Length<1e-8,"Mouse elbow anchor stays fixed");
                        }
                    }
                    if(Environment.GetEnvironmentVariable("DEKTEO_HANDS_FRAMES")=="1"){
                        for(int frame=0;frame<16;frame++){
                            render.Invoke(scene,new object[]{.1});
                            var shot=new RenderTargetBitmap(900,height,96,96,PixelFormats.Pbgra32);shot.Render(scene);
                            var frameEncoder=new PngBitmapEncoder();frameEncoder.Frames.Add(BitmapFrame.Create(shot));
                            using var stream=File.Create(Path.Combine(folder,$"motion-{(isDesk?"desk":"pad")}-{frame:00}.png"));frameEncoder.Save(stream);
                        }
                    }
                    scene.StopHandsTest();Check(rotations.All(r=>r.Angle==0),"Stopping clears elbow motion");
                }
                scene.StopHandsTest();
            }
        }
        Console.WriteLine($"Hands checks: {n}. Real hardware and game input not verified.");
    }
}
