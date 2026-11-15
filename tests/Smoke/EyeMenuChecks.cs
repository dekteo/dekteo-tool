using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class EyeMenuChecks
{
    public static void RenderLayers()
    {
        var app=new Dekotube.App(); app.InitializeComponent();
        var window=new Dekotube.MainWindow();
        var character=new AvatarEmotion { Name="Проверка" };
        character.MouthLayer.Enabled=true;character.MouthLayer.Procedural=true;character.MouthLayer.ProceduralVowels=true;
        character.Pupils.Enabled=true;character.Head.Enabled=true;
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(Dekotube.MainWindow).GetField("_selectedEmotion",flags)!.SetValue(window,character);
        var build=typeof(Dekotube.MainWindow).GetMethod("BuildStateSlots",flags)!;
        var rail=(Border)window.FindName("RightPanelBorder");
        ((Panel)rail.Parent).Children.Remove(rail);
        var root=new Border { Background=Brushes.DarkSlateGray,Child=rail,Width=320,Height=780 };
        foreach(var mode in new[]{"procedural","png","legacy"})
        {
            character.MouthLayer.Enabled=mode!="legacy";character.MouthLayer.Procedural=mode=="procedural";
            build.Invoke(window,null);
            foreach (var section in ((StackPanel)window.FindName("LayerImages")).Children.OfType<Border>()) ((Expander)section.Child).IsExpanded=true;
            if((((StackPanel)window.FindName("LegacyImages")).Visibility==Visibility.Visible)!=(mode=="legacy"))throw new Exception("Wrong mode content");
            root.Measure(new Size(320,780));root.Arrange(new Rect(0,0,320,780));root.UpdateLayout();
            var dock=(DockPanel)rail.Child;
            if(dock.Children[0] is not StackPanel || dock.Children[1] is not ScrollViewer scroll || scroll.ViewportHeight<=0)throw new Exception("Header/scroll layout invalid");
            var bitmap=new RenderTargetBitmap(320,780,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"layers-"+mode+".png"));encoder.Save(file);
        }
        character.MouthLayer.Enabled=true;character.MouthLayer.Procedural=true;
        foreach(var entry in new (string,UserControl)[] {
            ("mouth-settings",new MouthLayerView(character,new AvatarView(),()=>{},true)),
            ("head-settings",new HeadView(character,new AvatarView(),()=>{},true)) })
        {
            var preview=new Border { Background=new SolidColorBrush(Color.FromRgb(20,20,20)),Child=entry.Item2,Width=460,Height=850 };
            preview.Measure(new Size(460,850));preview.Arrange(new Rect(0,0,460,850));preview.UpdateLayout();
            var bitmap=new RenderTargetBitmap(460,850,96,96,PixelFormats.Pbgra32);bitmap.Render(preview);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,entry.Item1+".png"));encoder.Save(output);
        }
        Console.WriteLine("Layer modes, fixed header and settings construction passed; 3 previews rendered.");
    }
    public static void RenderMouth()
    {
        var app=new Dekotube.App(); app.InitializeComponent();
        var character=new AvatarEmotion(); character.MouthLayer.Procedural=true; character.MouthLayer.Enabled=true;
        var scene=new AvatarView(); scene.ConfigureCharacter(character);
        var view=new MouthLayerView(character,scene,()=>{});
        var root=new Border { Background=new SolidColorBrush(Color.FromRgb(20,23,28)),Child=view,Width=520,Height=980 };
        root.Measure(new Size(520,980));root.Arrange(new Rect(0,0,520,980));root.UpdateLayout();
        var image=new RenderTargetBitmap(520,980,96,96,PixelFormats.Pbgra32);image.Render(root);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
        using var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"mouth-menu.png"));png.Save(file);
        ((StackPanel)view.Content).Children.OfType<ComboBox>().Single().SelectedIndex=0;
        root.UpdateLayout();
        var pngImage=new RenderTargetBitmap(520,980,96,96,PixelFormats.Pbgra32);pngImage.Render(root);
        var pngEncoder=new PngBitmapEncoder();pngEncoder.Frames.Add(BitmapFrame.Create(pngImage));
        using var pngFile=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"mouth-menu-png.png"));pngEncoder.Save(pngFile);
    }
    public static void Run(Action<bool,string> check)
    {
        var character=new AvatarEmotion();character.Pupils.Enabled=true;
        character.Pupils.Left.ImagePath="kept.png";
        character.Pupils.Left.SetLayerVisible("pupil",false);
        character.Pupils.LinkSizes=true;
        var copy=character.Duplicate();copy.Pupils.Left.SetLayerVisible("pupil",true);
        check(!character.Pupils.Left.IsLayerVisible("pupil") && copy.Pupils.LinkSizes,"Visibility copy is independent; linked sizes copied");
        var restored=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        check(restored.Pupils.Left.ImagePath=="kept.png" && restored.Pupils.Left.VisiblePath("pupil","kept.png")=="","Hidden PNG remains saved");
        var scene=new AvatarView();
        var view=new PupilsView(character,scene,()=>{});
        var editor=(StackPanel)view.FindName("Editor");
        var details=(StackPanel)editor.Children[3];
        var rows=(StackPanel)details.Children[1];
        check(rows.Children.OfType<Border>().Count()==6,"Six compact layer rows");
        var grid=(Grid)((Border)rows.Children[0]).Child;
        check(grid.Children.OfType<Expander>().Single().IsExpanded==false,"Layer settings start collapsed");
        check(grid.Children.OfType<StackPanel>().Single().Children.Count==3,"Visibility, add and remove remain outside collapsed settings");
        ((ComboBox)editor.Children[1]).SelectedIndex=0;
        check(details.Visibility==Visibility.Collapsed,"PNG mode hides separate layer settings");
        ((ComboBox)editor.Children[1]).SelectedIndex=1;
        var app=new Dekotube.App();
        app.InitializeComponent();
        var root=new Border { Background=new SolidColorBrush(Color.FromRgb(20,23,28)),Child=view,Width=580,Height=720 };
        root.Measure(new Size(580,720));root.Arrange(new Rect(0,0,580,720));root.UpdateLayout();
        var image=new RenderTargetBitmap(580,720,96,96,PixelFormats.Pbgra32);image.Render(root);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
        using var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"eye-menu.png"));png.Save(file);
    }
}
