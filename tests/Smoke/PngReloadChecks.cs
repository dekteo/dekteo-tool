using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class PngReloadChecks
{
    public static void Run()
    {
        var app=new Dekotube.App();app.InitializeComponent();
        string dir=Path.Combine(AppContext.BaseDirectory,"png-reload-fixture");Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"same-name.png");
        void Write(byte red,byte blue,int size=32){var bytes=new byte[size*size*4];for(int i=0;i<bytes.Length;i+=4){bytes[i]=blue;bytes[i+2]=red;bytes[i+3]=255;}var image=BitmapSource.Create(size,size,96,96,PixelFormats.Bgra32,null,bytes,size*4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var f=File.Create(path);png.Save(f);}
        void Check(bool yes,string name){if(!yes)throw new Exception(name);Console.WriteLine("PASS: "+name);}
        byte[] Pixel(BitmapSource image){var b=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(b,image.PixelWidth*4,0);return b;}
        void Blue(BitmapSource image,string name){var p=Pixel(image);Check(p[0]==255&&p[2]==0,name);}
        Write(255,0);var stamp=File.GetLastWriteTimeUtc(path);
        var c=new AvatarEmotion();c.Head.Enabled=true;c.Head.BodyImagePath=path;c.SceneScale=.8;
        c.MouthLayer.Enabled=true;c.MouthLayer.BasePath=path;c.MouthLayer.ClosedPath=path;c.MouthLayer.OpenPath=path;
        c.Pupils.Enabled=true;c.Pupils.Left.ImagePath=path;c.Pupils.Left.MaskImagePath=path;c.Pupils.Left.ClosedEyeImagePath=path;
        c.Hands.Gamepad=new HandsRig{CanvasWidth=32,CanvasHeight=32,Y=.12,Scale=.7,Layers=Enumerable.Repeat(path,5).ToList()};c.Hands.Enabled=true;
        c.Accessories.Add(AccessoryOptions.AttachedPng(path,false,true));
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        object Field(string name)=>typeof(AvatarView).GetField(name,flags)!.GetValue(scene)!;
        var oldBody=((Image)scene.FindName("BodyImage")).Source;
        Write(0,255);File.SetLastWriteTimeUtc(path,stamp); // No filename or timestamp change.
        scene.ReloadImageFiles();
        Blue((BitmapSource)((Image)scene.FindName("BodyImage")).Source,"Overwritten body reloads at identical path/timestamp");
        Check(!ReferenceEquals(oldBody,((Image)scene.FindName("BodyImage")).Source),"Body source is replaced");
        Blue((BitmapSource)((Image)scene.FindName("LeftPupilOverlay")).Source,"Pupil filename guard invalidated");
        Blue((BitmapSource)Field("_leftMask"),"Eye mask reloaded");
        Blue((BitmapSource)Field("_mouthBase"),"Head/mouth base cache reloaded");
        Blue((BitmapSource)((Image)Field("_layerClosed")).Source,"Mouth layer tag invalidated");
        Blue((BitmapSource)((Image[])Field("_handsImages"))[0].Source,"Device layer cache reloaded");
        var accessories=(Dictionary<string,ImageSource?>)Field("_accessoryFiles");Blue((BitmapSource)accessories[path]!,"Accessory cache reloaded");
        Check(c.SceneScale==.8&&c.Hands.Gamepad.Y==.12&&c.Hands.Gamepad.Scale==.7,"Reload preserves placement/calibration");
        var editor=new HandsRigView(c,scene,()=>{});
        IEnumerable<System.Windows.DependencyObject> Nodes(System.Windows.DependencyObject node){yield return node;foreach(var child in System.Windows.LogicalTreeHelper.GetChildren(node).OfType<System.Windows.DependencyObject>())foreach(var descendant in Nodes(child))yield return descendant;}
        var buttons=Nodes(editor).OfType<Button>().ToList();
        Check(buttons.Any(b=>Equals(b.Content,"↻ Перечитать PNG с диска")),"Hands editor exposes explicit reload");
        buttons.Single(b=>Equals(b.Content,"Убрать PNG · Геймпад")).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        Check(c.Hands.Gamepad.Layers[0]==""&&File.Exists(path),"Removing device PNG clears reference without deleting file");
        Write(255,0,48);Check(PngFile.Read(path).PixelWidth==48,"Import validation reads new dimensions without URI cache");
        Write(0,255,48);Blue(PngFile.Read(path),"Fresh loader releases file for overwrite");
    }
}
