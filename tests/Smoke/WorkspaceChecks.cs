using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class WorkspaceChecks
{
    public static void Run(Action<bool,string> check)
    {
        var p=new ArmMotionPart{Shoulder=new(.2,.2),Elbow=new(.2,.65),Wrist=new(.7,.65)};
        double jump=0;
        for(double y=.25;y<.85;y+=.01)for(double x=.05;x<.9;x+=.001)jump=Math.Max(jump,Math.Abs(ArmMotionMap.AlongArm(new Point(x,y),p)-ArmMotionMap.AlongArm(new Point(x+.001,y),p)));
        check(jump<.02,$"Bent elbow field is continuous (max neighbouring change {jump:F4})");
        check(HandsInputReader.CursorAxis(1440,0,1920)==.5&&HandsInputReader.CursorAxis(-960,-1920,3840)==-.5,"Mouse position mapping supports negative desktop coordinates");
        check(!HandsAutoSwitch.Active(new(0,0,.8,.6,0,0,true,PointerMoved:false))&&HandsAutoSwitch.Active(new(0,0,.8,.6,0,0,true,PointerMoved:true)),"Held mouse position does not count as fresh input for pose switching");
        var app=new VeadotubeMy.App();app.InitializeComponent();
        var c=new AvatarEmotion{Name="CRT · проверка интерфейса"};c.Head.Enabled=true;c.MouthLayer.Enabled=true;c.Pupils.Enabled=true;
        string pack=Path.GetFullPath("artifacts/motion-editor-v1/app/HandsSample");
        c.Head.BodyImagePath=Path.Combine(pack,"01-body.png");c.MouthLayer.BasePath=Path.Combine(pack,"02-head.png");
        var window=new VeadotubeMy.MainWindow(c);
        var root=(Grid)window.Content;window.Content=null;
        root.Measure(new Size(1280,850));root.Arrange(new Rect(0,0,1280,850));root.UpdateLayout();
        var rail=(Border)window.FindName("RightPanelBorder");var inspector=(Border)window.FindName("OverlayHost");
        var tree=((StackPanel)window.FindName("LayerImages")).Children.OfType<TreeView>().Single();
        var layers=(TreeViewItem)tree.Items[0];
        var eye=layers.Items.OfType<TreeViewItem>().First(n=>((TextBlock)n.Header).Text=="Глаза");
        eye.RaiseEvent(new RoutedEventArgs(TreeViewItem.SelectedEvent,eye));
        root.UpdateLayout();
        check(rail.HorizontalAlignment==HorizontalAlignment.Left&&inspector.HorizontalAlignment==HorizontalAlignment.Right&&inspector.Visibility==Visibility.Visible,"Layer tree is on left and selected layer properties on right");
        check(((TextBlock)window.FindName("OverlayTitle")).Text=="Глаза","Eye selection opens its own image and property panel");
        var scene=(AvatarView)window.FindName("AvatarView");var artwork=(Grid)scene.FindName("Artwork");double width=artwork.ActualWidth,height=artwork.ActualHeight;
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(VeadotubeMy.MainWindow).GetMethod("HideOverlay",flags)!.Invoke(window,null);root.UpdateLayout();
        check(artwork.ActualWidth==width&&artwork.ActualHeight==height,"Closing inspector preserves avatar geometry");
        eye.IsSelected=true;
        typeof(VeadotubeMy.MainWindow).GetMethod("HideOverlay",flags)!.Invoke(window,null);
        eye.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent});root.UpdateLayout();
        check(inspector.Visibility==Visibility.Visible,"Clicking the selected layer reopens its closed inspector");
        var basis=layers.Items.OfType<TreeViewItem>().First(n=>((TextBlock)n.Header).Text=="Основа персонажа"); eye.IsSelected=true;root.UpdateLayout();
        Directory.CreateDirectory("artifacts/workspace-v2");var image=new RenderTargetBitmap(1280,850,96,96,PixelFormats.Pbgra32);image.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create("artifacts/workspace-v2/interface.png"))encoder.Save(file);
    }
}
