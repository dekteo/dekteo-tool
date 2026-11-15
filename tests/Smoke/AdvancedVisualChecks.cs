using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;
internal static class AdvancedVisualChecks
{
    public static void Run()
    {
        var app=new Dekotube.App();app.InitializeComponent();int checks=0;
        void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine(text);}
        var average=ScreenLightCapture.Average(new byte[]{0,0,200,255,200,0,0,255},2,1);
        Check(average.Left.R==200&&average.Left.B==0&&average.Right.B==200,"Left/right screen averages independent");
        var effect=new ColorCorrectionEffect();effect.SetLight(Colors.Red,Colors.Blue,.7);
        var root=new Border{Width=100,Height=20,Background=new SolidColorBrush(Color.FromArgb(128,30,30,30)),Effect=effect};
        byte[] Pixels(){root.Measure(new Size(100,20));root.Arrange(new Rect(0,0,100,20));root.UpdateLayout();var image=new RenderTargetBitmap(100,20,96,96,PixelFormats.Pbgra32);image.Render(root);var data=new byte[100*20*4];image.CopyPixels(data,400,0);return data;}
        var lit=Pixels();int l=(10*100+10)*4,r=(10*100+90)*4;
        Check(lit[l+2]>lit[l]&&lit[r]>lit[r+2],"Shader lights opposite sides with different colors");Check(lit[l+3]==128&&lit[r+3]==128,"Screen light preserves alpha");
        effect.SetLight(Colors.Black,Colors.Black,0);effect.SetGlint(.625,1);var shiny=Pixels();
        Check(shiny[(10*100+50)*4]>shiny[(10*100+5)*4]+25,"Metallic glint appears only in band");
        effect.SetGlint(.625,0);var plain=Pixels();Check(plain[(10*100+50)*4]<shiny[(10*100+50)*4],"Glint can be disabled");
        var c=new AvatarEmotion();c.ScreenLight.Enabled=true;c.ScreenLight.Strength=.4;c.ScreenEffect.DepthEnabled=true;c.ScreenEffect.DepthAmount=8;
        c.Accessories.Add(new AccessoryOptions{Frames=["001.png","002.png"],Intermittent=true,Metallic=true});
        var copy=c.Duplicate();copy.ScreenLight.Strength=.9;copy.Accessories[0].Frames.Clear();
        Check(c.ScreenLight.Strength==.4&&c.Accessories[0].Frames.Count==2,"Copies preserve independent light and frames");
        var loaded=System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(System.Text.Json.JsonSerializer.Serialize(c))!;
        Check(loaded.ScreenEffect.DepthEnabled&&loaded.Accessories[0].Metallic&&loaded.Accessories[0].Intermittent,"New modes survive JSON");
        var scene=new AvatarView();scene.ConfigureCharacter(c);var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        void Call(string name,params object[] args)=>typeof(AvatarView).GetMethod(name,flags)!.Invoke(scene,args);
        typeof(AvatarView).GetField("_targetLeft",flags)!.SetValue(scene,Colors.Red);typeof(AvatarView).GetField("_targetRight",flags)!.SetValue(scene,Colors.Blue);
        for(int i=0;i<600;i++)Call("RenderScreenLight",1.0/60);
        Check(scene.ScreenLightColors.Left.R==255&&scene.ScreenLightColors.Right.B==255,"Light smoothing converges without byte rounding stall");
        c.ScreenLight.Enabled=false;scene.RefreshScreenLight();Check(((UIElement)scene.FindName("LivingRig")).Effect==null,"Disabling light detaches shader");
        typeof(AvatarView).GetField("_perspectiveYaw",flags)!.SetValue(scene,12.0);for(int i=0;i<100;i++)Call("RenderScreenDepth",.016);
        var eyes=(TranslateTransform)((Canvas)scene.FindName("PupilCanvas")).RenderTransform;
        Check(eyes.X<0&&Math.Abs(eyes.X)<=8,"Depth bounded and moves opposite head yaw");
        scene.SetEditingPaused(true);Check(eyes.X==0,"Editing resets depth offset");scene.SetEditingPaused(false);
        var o=new AccessoryOptions{FramesPerSecond=4,Intermittent=true,IntervalMin=1,IntervalMax=1,HideBetween=true};var player=new AccessoryAnimation(1);
        Check(player.Step(0,2,o)==-1,"Intermittent animation starts hidden");player.Preview();
        Check(player.Step(0,2,o)==0&&player.Step(.25,2,o)==1&&player.Step(.25,2,o)==-1,"Preview plays once then hides");
        for(int i=0;i<3;i++)Check(player.Step(.25,2,o)==-1,"Interval waits");Check(player.Step(.25,2,o)>=0,"Interval starts next playback");
        var loop=new AccessoryAnimation(2);o.Intermittent=false;Check(loop.Step(.25,2,o)==1&&loop.Step(.25,2,o)==0,"Loop wraps frame sequence");
        var red=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat(new byte[]{0,0,255,255},4).SelectMany(x=>x).ToArray(),8);red.Freeze();
        var blue=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat(new byte[]{255,0,0,255},4).SelectMany(x=>x).ToArray(),8);blue.Freeze();
        var sprite=new AccessorySprite{Width=40,Height=40};sprite.Configure(red,.5,0,1);sprite.Measure(new Size(40,40));sprite.Arrange(new Rect(0,0,40,40));sprite.UpdateLayout();
        var field=typeof(AccessorySprite).GetField("_drawing",flags)!;var drawing=field.GetValue(sprite);sprite.SetFrame(blue);Check(ReferenceEquals(drawing,field.GetValue(sprite)),"Frame switch retains deformation geometry");
        var frameImage=new RenderTargetBitmap(40,40,96,96,PixelFormats.Pbgra32);frameImage.Render(sprite);byte[] framePixels=new byte[40*40*4];frameImage.CopyPixels(framePixels,160,0);Check(framePixels[(20*40+20)*4]>200,"Frame switch changes actual rendered pixels");
        c.ScreenEffect.X=.25;c.ScreenEffect.Y=.25;c.ScreenEffect.Width=.5;c.ScreenEffect.Height=.5;
        var mask=(Brush)typeof(AvatarView).GetMethod("ScreenProtection",flags)!.Invoke(scene,null)!;
        Check(ReferenceEquals(mask,typeof(AvatarView).GetMethod("ScreenProtection",flags)!.Invoke(scene,null)),"Screen protection geometry cached");
        effect.Protection=mask;effect.SetLight(Colors.White,Colors.White,1);var protectedLight=Pixels();int center=(10*100+50)*4;
        Check(Math.Abs(protectedLight[center]-plain[center])<=1 && protectedLight[l]>plain[l]+40,"Screen region preserves brightness while housing lights");
        effect.SetLight(Colors.Black,Colors.Black,0);effect.SetDarkness(.8);var dark=Pixels();
        Check(Math.Abs(dark[center]-plain[center])<=1 && dark[l]<plain[l]/2,"Screen region preserves brightness while housing darkens");
        effect.Protection=ColorCorrectionEffect.NoProtection;effect.SetDarkness(0);effect.SetGlint(.625,1);
        effect.SetShineRegion(new AccessoryOptions{ShineRegion=true,ShineX=0,ShineY=0,ShineWidth=.2,ShineHeight=1});var limited=Pixels();
        Check(limited[center]<shiny[center]-25,"Glint excluded outside selected accessory zone");
        c.ScreenLight.Enabled=true;c.ScreenLight.ProtectScreen=true;c.ScreenLight.Darken=true;scene.RefreshColorCorrection();
        Check(((UIElement)scene.FindName("LivingRig")).Effect==null && ((UIElement)scene.FindName("Composite")).Effect is ColorCorrectionEffect,"Protected light follows head instead of whole rig");
        c.ScreenLight.Enabled=false;scene.RefreshColorCorrection();Check(((UIElement)scene.FindName("Composite")).Effect==null,"Protected shader cleared when disabled");
        c.ScreenEffect.DepthEnabled=false;scene.TestScreenDepth();for(int i=0;i<40;i++)Call("RenderScreenDepth",.016);Check(Math.Abs(eyes.X)>1,"Depth test works without voice or enabled effect");scene.StopScreenDepthTest();Check(eyes.X==0,"Stopping depth test restores position");
        c.Accessories[0].ShineRegion=true;c.Accessories[0].ShineWidth=.3;
        var saved=System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(System.Text.Json.JsonSerializer.Serialize(c))!;
        Check(saved.ScreenLight.ProtectScreen&&saved.ScreenLight.Darken&&saved.Accessories[0].ShineWidth==.3,"Protection and accessory zone persist");
        var menu=new ScreenLightView(c,scene,()=>{});var menuRoot=new Border{Width=460,Height=1000,Background=Brushes.Black,Child=menu};menuRoot.Measure(new Size(460,1000));menuRoot.Arrange(new Rect(0,0,460,1000));menuRoot.UpdateLayout();
        var preview=new RenderTargetBitmap(460,1000,96,96,PixelFormats.Pbgra32);preview.Render(menuRoot);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(preview));using(var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"screen-light-menu.png")))encoder.Save(file);
        Console.WriteLine($"Advanced visual checks: {checks}. Desktop capture not invoked.");
    }
}
