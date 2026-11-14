using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class ScanlineLayerChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();
        var c=new AvatarEmotion();c.ScreenEffect.Scanlines=true;c.ScreenEffect.ScanlineOpacity=.5;
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var artwork=(Grid)scene.FindName("Artwork");artwork.Width=artwork.Height=200;
        var composite=(Grid)scene.FindName("Composite");composite.Width=composite.Height=200;
        var mouth=(Canvas)typeof(AvatarView).GetField("_mouthCanvas",flags)!.GetValue(scene)!;
        mouth.Visibility=Visibility.Visible;mouth.Children.Add(new Rectangle{Width=200,Height=200,Fill=Brushes.Cyan});
        var eyes=(Canvas)scene.FindName("PupilCanvas");
        var screen=(Canvas)scene.FindName("ScreenEffectCanvas");
        if(Panel.GetZIndex((UIElement)screen.Parent)<=Panel.GetZIndex((UIElement)mouth.Parent)||!ReferenceEquals(mouth.Parent,eyes.Parent))throw new Exception("Screen effects behind face");
        typeof(AvatarView).GetMethod("RenderScreenEffect",flags)!.Invoke(scene,new object[]{0.0});
        composite.Measure(new Size(200,200));composite.Arrange(new Rect(0,0,200,200));composite.UpdateLayout();
        byte[] Render(){var image=new RenderTargetBitmap(200,200,96,96,PixelFormats.Pbgra32);image.Render(composite);var pixels=new byte[200*200*4];image.CopyPixels(pixels,800,0);return pixels;}
        var on=Render();screen.Visibility=Visibility.Collapsed;var off=Render();
        bool difference=false;for(int y=50;y<95;y++)for(int x=70;x<120;x++){int i=(y*200+x)*4;if(on[i]<off[i]-2)difference=true;}
        if(!difference)throw new Exception("Scanlines do not render over opaque mouth layer");
        c.ScreenEffect.Scanlines=false;c.ScreenEffect.Flicker=true;c.ScreenEffect.FlickerAmount=.5;
        typeof(AvatarView).GetMethod("RenderScreenEffect",flags)!.Invoke(scene,new object[]{1.0});var flicker=Render();
        if(flicker[(70*200+90)*4]>=off[(70*200+90)*4])throw new Exception("Flicker does not darken mouth");
        var brush=(VisualBrush)typeof(AvatarView).GetField("_faceBrush",flags)!.GetValue(scene)!;
        if(!ReferenceEquals(brush.Visual,mouth.Parent)||!ReferenceEquals(brush.Visual,eyes.Parent))throw new Exception("Glitch sample misses face layers");
        var robot=(FrameworkElement)typeof(AvatarView).GetField("_robotVisual",flags)!.GetValue(scene)!;
        if(!ReferenceEquals(robot.Parent,screen.Parent))throw new Exception("Robot effects outside shared top layer");
        Console.WriteLine("Scanlines above eye/mouth siblings; rendered stripes visible over opaque mouth. No face deformation added.");
    }
}
