using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class MouthStartupChecks
{
    public static void Run(Action<bool,string> check)
    {
        AvatarService.Instance.IsTalking=false;
        var c=new AvatarEmotion();
        c.MouthLayer.Enabled=true;c.MouthLayer.Procedural=true;
        c.MouthLayer.PerspectiveYaw=25;c.MouthLayer.RestThickness=.08;
        c.MouthLayer.GlowEnabled=false;
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var raw=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[4],4);
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,raw);
        var render=typeof(AvatarView).GetMethod("RenderMouthLayer",flags)!;
        for(int i=0;i<120;i++)render.Invoke(scene,new object[]{.016,1d});
        var host=(Grid)typeof(AvatarView).GetField("_mouthPoseHost",flags)!.GetValue(scene)!;
        ((Canvas)host.Parent).Children.Remove(host);
        host.Width=200;host.Height=100;
        var sprite=(Grid)typeof(AvatarView).GetField("_mouthSprite",flags)!.GetValue(scene)!;
        sprite.Width=200;sprite.Height=100;
        var projection=(HeadPerspectiveView)typeof(AvatarView).GetField("_mouthProjection",flags)!.GetValue(scene)!;
        projection.Configure(200,100,.5,.5);
        var window=new Window{Content=host,Width=240,Height=160,ShowInTaskbar=false,WindowStyle=WindowStyle.None};window.Show();
        sprite.Measure(new Size(200,100));sprite.Arrange(new Rect(0,0,200,100));sprite.UpdateLayout();
        host.Measure(new Size(200,100));host.Arrange(new Rect(0,0,200,100));host.UpdateLayout();
        host.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        host.UpdateLayout();
        var bmp=new RenderTargetBitmap(200,100,96,96,PixelFormats.Pbgra32);bmp.Render(host);
        var pixels=new byte[200*100*4];bmp.CopyPixels(pixels,800,0);
        window.Close();
        int min=100,max=-1;
        for(int y=0;y<100;y++)for(int x=0;x<200;x++)if(pixels[(y*200+x)*4+3]>100){min=Math.Min(min,y);max=Math.Max(max,y);}
        check(max>=min && max-min<25,$"Silent projected mouth keeps rest thickness after startup (height {max-min+1}px)");
    }
}
