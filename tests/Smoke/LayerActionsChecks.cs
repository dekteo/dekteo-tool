using System.Windows;
using System.Windows.Controls;
using Dekotube.Services;
using Dekotube.Views;
internal static class LayerActionsChecks
{
    public static void Run()
    {
        var app=new Dekotube.App();app.InitializeComponent();
        var c=new AvatarEmotion();c.Head.Enabled=true;c.HiddenParts.Add("base");c.HiddenParts.Add("body");
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine(text);}
        Check(((UIElement)scene.FindName("CurrentStateLayer")).Visibility==Visibility.Hidden && ((UIElement)scene.FindName("BodyShade")).Visibility==Visibility.Hidden,"Base and body hide including shadow");
        c.HiddenParts.Clear();scene.RefreshVisualSettings();Check(((UIElement)scene.FindName("BodyImage")).Visibility==Visibility.Visible,"Visibility restores without changing source");
        c.HiddenParts.Add("mouth");var copy=c.Duplicate();copy.HiddenParts.Clear();Check(c.HiddenParts.Count==1,"Hidden state copy independent");
        Check(System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(System.Text.Json.JsonSerializer.Serialize(c))!.HiddenParts.Contains("mouth"),"Hidden state persists");
        int saves=0;double original=c.MouthLayer.X;
        var mouth=new MouthPlacementView(c,scene,()=>saves++);mouth.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var root=(StackPanel)mouth.Content;root.Children.OfType<Slider>().First().Value=75;
        Check(c.MouthLayer.X==.75 && saves==0,"Mouth live draft does not save");
        mouth.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Check(c.MouthLayer.X==original && saves==0,"Closing mouth editor cancels");
        mouth=new MouthPlacementView(c,scene,()=>saves++);mouth.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));root=(StackPanel)mouth.Content;
        root.Children.OfType<Slider>().First().Value=60;root.Children.OfType<Button>().First(b=>(string)b.Content=="Применить").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        mouth.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Check(c.MouthLayer.X==.6 && saves==1,"Apply mouth commits once");
        var placement=new PlacementView(c,scene,()=>saves++);placement.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        ((StackPanel)placement.Content).Children.OfType<Slider>().Single().Value=150;placement.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Check(c.SceneScale==1 && saves==1,"Closing character placement cancels scale");
        scene.EditEyeRegion(1);Check(((UIElement)scene.FindName("LeftRegionOutline")).Visibility==Visibility.Collapsed && ((UIElement)scene.FindName("RightRegionOutline")).Visibility==Visibility.Visible,"Only selected eye outlined");scene.EditEyeRegion(null);
    }
}
