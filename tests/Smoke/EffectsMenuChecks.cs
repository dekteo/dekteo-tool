using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;
internal static class EffectsMenuChecks
{
    public static void Run()
    {
        var app=new Dekotube.App();app.InitializeComponent();
        var c=new AvatarEmotion{AddedEffectCards=["mimic","head","screenLight","color"]};
        c.ScreenEffect.Added=true;c.Lighting.Added=true;c.Effects.Add(new CharacterEffect{Kind="wave"});
        var scene=new AvatarView();scene.ConfigureCharacter(c);int saves=0;
        var view=new EffectsView(c,scene,()=>saves++);
        var panel=(StackPanel)view.Content;
        var groups=panel.Children.OfType<StackPanel>().ToArray();
        if(groups.Length!=4)throw new Exception("Expected four groups");
        var titles=groups.Select(g=>((TextBlock)((Border)g.Children[0]).Child).Text).ToArray();
        if(!titles.SequenceEqual(new[]{"ЛИЦО","ДВИЖЕНИЕ","ЭКРАН","ОСВЕЩЕНИЕ"}))throw new Exception("Group order");
        if(c.ScreenEffect.Enabled||c.Lighting.Enabled||c.ScreenLight.Enabled||saves!=0)throw new Exception("Opening menu enabled or saved effects");
        var expanders=groups.SelectMany(g=>g.Children.OfType<Border>().Select(b=>b.Child).OfType<Expander>()).ToArray();
        if(expanders.Length!=7||expanders.Any(e=>e.IsExpanded))throw new Exception("Cards must begin compact");
        var root=new Border{Width=470,Height=850,Background=Brushes.Black,Child=view};root.Measure(new Size(470,850));root.Arrange(new Rect(0,0,470,850));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(470,850,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"effects-groups.png")))png.Save(file);
        Button AddButton()=>panel.Children.OfType<Button>().First();
        AddButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var picker=panel.Children.OfType<Border>().Last();if(picker.Visibility!=Visibility.Visible)throw new Exception("Picker did not open");
        var choices=(StackPanel)picker.Child;
        Button Choice(string title)=>choices.Children.OfType<Border>().Select(b=>(Expander)b.Child).Select(e=>(StackPanel)e.Content).SelectMany(p=>p.Children.OfType<Button>()).Single(b=>(string)b.Content==title);
        foreach(var name in new[]{"Обработка…","Цифровой сбой","Индикатор голоса"})if(Choice(name)==null)throw new Exception("Missing screen effect");
        Choice("Обработка…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(!c.AddedEffectCards.Contains("processing")||saves!=1||c.RobotScreen.Processing)throw new Exception("Add must save once without enabling");
        AddButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));AddButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(panel.Children.OfType<Border>().Last().Visibility!=Visibility.Collapsed)throw new Exception("Picker did not close");
        Console.WriteLine("Four ordered groups, seven collapsed cards, opening does not enable/save effects; preview rendered.");
    }
}
