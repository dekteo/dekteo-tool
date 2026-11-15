using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class LightEditorChecks
{
    public static void Run(Action<bool,string> check)
    {
        BitmapSource Image(byte r,byte g,byte b,byte a=255)
        {
            var bytes=new byte[32*32*4];
            for(int i=0;i<bytes.Length;i+=4){bytes[i]=b;bytes[i+1]=g;bytes[i+2]=r;bytes[i+3]=a;}
            var result=BitmapSource.Create(32,32,96,96,PixelFormats.Bgra32,null,bytes,128);result.Freeze();return result;
        }
        byte[] Pixels(BitmapSource b){var bytes=new byte[b.PixelWidth*b.PixelHeight*4];b.CopyPixels(bytes,b.PixelWidth*4,0);return bytes;}
        var source=Image(140,140,140,120);var normal=Image(255,128,180);
        var options=new LightingOptions{LightX=0,LightY=.5,Color="#FFFFFF"};
        var left=Pixels(NormalMapLighting.Apply(source,normal,options)!);
        options.LightX=1;
        var right=Pixels(NormalMapLighting.Apply(source,normal,options)!);
        check(!left.SequenceEqual(right),"Lamp position visibly changes normal-map pixels");
        check(Enumerable.Range(0,1024).All(i=>right[i*4+3]==120),"Lighting preserves alpha");
        var character=new AvatarEmotion();character.Lighting.Added=true;character.Lighting.Enabled=true;
        var scene=new AvatarView();scene.ConfigureCharacter(character);
        var rotation=(RotateTransform)scene.FindName("EffectRotation");
        rotation.Angle=12;
        var state=(System.Windows.Controls.Image)scene.FindName("StateImage");state.Source=source;
        scene.GetType().GetField("_lastHeadDiffuse",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(scene,source);
        bool saved=false;
        scene.BeginLightEditing(()=>saved=true);
        double frozenAngle=rotation.Angle;
        scene.GetType().GetMethod("RenderFrame",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);
        scene.GetType().GetMethod("UpdateScene",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);
        check(rotation.Angle==frozenAngle && ReferenceEquals(state.Source,source),"Light editing freezes transforms and mouth state");
        check(((Canvas)scene.FindName("LampEditor")).Visibility==Visibility.Visible,"Lamp controls appear for editing");
        scene.EndLightEditing();
        check(saved && ((Canvas)scene.FindName("LampEditor")).Visibility==Visibility.Collapsed,"Done saves and removes lamp controls");
        var view=new EffectsView(character,scene,()=>{});
        var panel=(StackPanel)view.Content;
        check(panel.Children.OfType<Expander>().Any(x=>Equals(x.Header,"Освещение")),"Added lighting has a card");
        var content=(StackPanel)panel.Children.OfType<Expander>().Single(x=>Equals(x.Header,"Освещение")).Content;
        content.Children.OfType<Button>().Single(x=>Equals(x.Content,"Удалить эффект")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!character.Lighting.Enabled && !character.Lighting.Added && !panel.Children.OfType<Expander>().Any(x=>Equals(x.Header,"Освещение")),"Remove disables light and removes its card");
    }
}
