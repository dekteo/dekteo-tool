using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class EyeClothingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var eye=new EyeRegion{RotationDegrees=15,PerspectiveEnabled=true,PerspectiveYaw=35,PerspectiveSlope=10,PerspectiveTravel=.7};
        var center=new Point(50,40);var plane=EyePerspective.Plane(eye,100,80);
        check((plane.Transform(center)-center).Length<1e-8,"Eye perspective preserves calibrated centre");
        var direction=EyePerspective.Direction(eye,new Vector(.7,0));var screen=plane.Transform(direction);
        check(screen.Y>0 && screen.X>0,"Horizontal gaze follows sloped screen plane");
        var far=eye.Copy();far.PerspectiveTravel=.4;
        check(EyePerspective.Direction(far,new Vector(.7,0)).Length<direction.Length,"Far eye has shorter travel without changing its calibration");
        eye.PerspectiveEnabled=false;
        var old=EyePerspective.Plane(eye,100,80).Transform(EyePerspective.Direction(eye,new Vector(.7,0)));
        check(Math.Abs(old.Y)<1e-8&&Math.Abs(old.X-.7)<1e-8,"Disabled perspective preserves legacy gaze");
        var character=new AvatarEmotion();character.Pupils.Enabled=true;character.Pupils.Left=far;
        var scene=new AvatarView();scene.ConfigureCharacter(character);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var render=typeof(AvatarView).GetMethod("RenderFrame",flags)!;render.Invoke(scene,null);
        var clip=(Canvas)scene.FindName("LeftEyeClip");var mask=clip.Clip;
        var closed=(Grid)scene.FindName("LeftClosedEye");
        check(clip.RenderTransform.Value==closed.RenderTransform.Value,"Blink and open eye share perspective");
        for(int i=0;i<100;i++)render.Invoke(scene,null);
        check(ReferenceEquals(mask,clip.Clip),"Perspective animation does not rebuild masks");
        var bytes=new byte[40*40*4];for(int y=0;y<40;y++)for(int x=0;x<40;x++){int i=(y*40+x)*4;bytes[i]=bytes[i+1]=bytes[i+2]=(byte)(60+x*3);bytes[i+3]=(byte)(x==0?0:180);}
        var raw=BitmapSource.Create(40,40,96,96,PixelFormats.Bgra32,null,bytes,160);raw.Freeze();
        var layer=new ClothLayer{Mask=new(){new(){Points=new(){new Point(.4,.2),new Point(.4,.8)},Size=.3}}};
        var restoredMask=JsonSerializer.Deserialize<ClothLayer>(JsonSerializer.Serialize(layer))!;
        check(restoredMask.Mask[0].Points[1]==new Point(.4,.8),"Painted mask coordinates survive profile serialization");
        var look=new ClothingLook{Color="#AA2233",Brightness=1.5};
        var output=ClothingRenderer.Pixels(ClothingRenderer.Apply(raw,look,layer));
        int outside=(20*40+35)*4,inside=(20*40+16)*4;
        check(output.Skip(outside).Take(4).SequenceEqual(bytes.Skip(outside).Take(4)),"Clothing leaves unmasked pixels exactly unchanged");
        check(output[inside+2]>output[inside+1]&&output[inside+3]==bytes[inside+3],"Clothing recolours fabric while preserving alpha");
        layer.Mask.Add(new(){Erase=true,Size=.1,Points=new(){new Point(.4,.5)}});
        output=ClothingRenderer.Pixels(ClothingRenderer.Apply(raw,look,layer));
        check(output.Skip(inside).Take(4).SequenceEqual(bytes.Skip(inside).Take(4)),"Eraser protects glove/device pixels");
        layer.WholeLayer=true;layer.Mask.Clear();
        output=ClothingRenderer.Pixels(ClothingRenderer.Apply(raw,look,layer));
        check(output[(20*40+30)*4+2]>output[(20*40+10)*4+2],"Recolour retains original light and dark folds");
        var print=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,255,0,255},4);
        layer.PrintX=.5;layer.PrintY=.5;layer.PrintWidth=.3;
        var printed=ClothingRenderer.Pixels(ClothingRenderer.Apply(raw,look,layer,print));int middle=(20*40+20)*4;
        check(printed[middle+1]>printed[middle+2]&&printed[middle+3]==bytes[middle+3],"Print composites into fabric without changing alpha");
        var temp=Path.Combine(Path.GetTempPath(),"vt-cloth-"+Guid.NewGuid()+".png");
        try{
            var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(raw));using(var stream=File.Create(temp))enc.Save(stream);
            character.Head.Enabled=true;character.Head.BodyImagePath=temp;
            character.Hands.Desk.LayeredArms=true;character.Hands.Desk.Layers=new(){temp};
            character.Hands.Gamepad.LayeredArms=true;character.Hands.Gamepad.Layers=new(){temp};
            character.Clothing.Enabled=true;character.Clothing.Current=look;look.Layers[temp]=layer;
            scene.ConfigureCharacter(character);scene.RefreshClothing();
            var body=(Image)scene.FindName("BodyImage");
            check(ClothingRenderer.Pixels((BitmapSource)body.Source)[middle+1]!=bytes[middle+1],"Avatar body uses clothing after lighting refresh");
            var hands=(Image[])typeof(AvatarView).GetField("_handsImages",flags)!.GetValue(scene)!;
            check(ClothingRenderer.Pixels((BitmapSource)hands[0].Source).SequenceEqual(ClothingRenderer.Pixels((BitmapSource)body.Source)),"Body and hands receive the same style before deformation");
            character.Hands.DeskMode=true;scene.RefreshHands();
            check(ClothingRenderer.Pixels((BitmapSource)hands[0].Source).SequenceEqual(ClothingRenderer.Pixels((BitmapSource)body.Source)),"Desk pose retains clothing style");
            character.Clothing.Enabled=false;scene.RefreshClothing();
            check(ClothingRenderer.Pixels((BitmapSource)body.Source).SequenceEqual(bytes),"Disabling clothing restores original PNG pixels");
            var saved=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
            check(saved.Pupils.Left.PerspectiveTravel==.4&&saved.Clothing.Current.Layers.ContainsKey(temp),"Eye perspective and clothing survive profile save/reload");
            var copy=character.Duplicate();copy.Clothing.Current.Layers[temp].WholeLayer=false;
            check(character.Clothing.Current.Layers[temp].WholeLayer,"Duplicated clothing settings are independent");
            var panel=new ClothingView(character,scene,()=>{});
            var preview=new Border{Width=480,Height=1050,Background=new SolidColorBrush(Color.FromRgb(25,33,43)),Child=panel};
            preview.Measure(new Size(480,1050));preview.Arrange(new Rect(0,0,480,1050));preview.UpdateLayout();
            var ui=new RenderTargetBitmap(480,1050,96,96,PixelFormats.Pbgra32);ui.Render(preview);
            Directory.CreateDirectory("artifacts/eye-clothing-v1");
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(ui));using(var file=File.Create("artifacts/eye-clothing-v1/clothing-ui.png"))png.Save(file);
            var maskWindow=new ClothMaskWindow(raw,layer);
            maskWindow.Show();maskWindow.UpdateLayout();
            var maskPreview=new RenderTargetBitmap((int)maskWindow.ActualWidth,(int)maskWindow.ActualHeight,96,96,PixelFormats.Pbgra32);maskPreview.Render(maskWindow);
            var maskPng=new PngBitmapEncoder();maskPng.Frames.Add(BitmapFrame.Create(maskPreview));using(var file=File.Create("artifacts/eye-clothing-v1/mask-ui.png"))maskPng.Save(file);
            maskWindow.Close();
        }finally{File.Delete(temp);}
    }
}

