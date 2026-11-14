using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

internal static class HandLightingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"vt-hand-light-"+Guid.NewGuid());Directory.CreateDirectory(root);
        var context=System.Threading.SynchronizationContext.Current;
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try{
            BitmapSource Solid(byte r,byte g,byte b,int size=24){var bytes=new byte[size*size*4];for(int i=0;i<bytes.Length;i+=4){bytes[i]=b;bytes[i+1]=g;bytes[i+2]=r;bytes[i+3]=170;}var image=BitmapSource.Create(size,size,96,96,PixelFormats.Bgra32,null,bytes,size*4);image.Freeze();return image;}
            string Save(string name,BitmapSource image){string path=Path.Combine(root,name+".png");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);png.Save(file);return path;}
            var source=Solid(140,130,120);string pngPath=Save("arms",source),pad=Save("pad-normal",Solid(255,128,128)),desk=Save("desk-normal",Solid(0,128,128));
            var c=new AvatarEmotion();c.Lighting.Enabled=true;c.Lighting.Added=true;c.Lighting.LightX=1;c.Lighting.LightY=.5;c.Lighting.Radius=2;c.Lighting.Color="#FFFFFF";
            c.Hands.Gamepad=new(){LayeredArms=true,Layers=new(){pngPath},NormalPaths=new(){pad}};
            c.Hands.Desk=new(){LayeredArms=true,Layers=new(){pngPath},NormalPaths=new(){desk}};
            var scene=new AvatarView();scene.ConfigureCharacter(c);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var images=(Image[])typeof(AvatarView).GetField("_handsImages",flags)!.GetValue(scene)!;
            var busy=typeof(AvatarView).GetField("_lightBusy",flags)!;
            void Settle(){var time=Stopwatch.StartNew();do{scene.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);System.Threading.Thread.Sleep(1);if(time.ElapsedMilliseconds>5000)throw new Exception("Lighting worker timeout");}while((bool)busy.GetValue(scene)!);}
            byte[] Pixels()=>ClothingRenderer.Pixels((BitmapSource)images[0].Source);
            scene.RefreshLighting(true);Settle();var padPixels=Pixels();
            check(padPixels.SequenceEqual(ClothingRenderer.Pixels(NormalMapLighting.Apply(source,PngFile.Read(pad),c.Lighting)!)),"Gamepad arms use their own normal map");
            var offsets=(TranslateTransform[])typeof(AvatarView).GetField("_handsOffsets",flags)!.GetValue(scene)!;offsets[0].X=7;
            c.Lighting.LightX=.8;scene.RefreshLighting();Settle();
            check(offsets[0].X==7,"Refreshing hand light does not reset arm transforms");
            c.Hands.DeskMode=true;scene.RefreshHands();Settle();var deskPixels=Pixels();
            check(!deskPixels.SequenceEqual(padPixels)&&deskPixels.SequenceEqual(ClothingRenderer.Pixels(NormalMapLighting.Apply(source,PngFile.Read(desk),c.Lighting)!)),"Desk arms switch to their own normal map");
            c.Hands.Gamepad.NormalPaths.Clear();c.Hands.DeskMode=false;scene.RefreshHands();Settle();
            check(Pixels().SequenceEqual(ClothingRenderer.Pixels(source)),"Missing arm map does not reuse body or other pose map");
            c.Hands.Desk.NormalPaths[0]=Save("wrong-size",Solid(128,128,255,8));c.Hands.DeskMode=true;scene.RefreshHands();scene.RefreshLighting();Settle();
            check(Pixels().SequenceEqual(ClothingRenderer.Pixels(source))&&scene.LightingStatus.Contains("Размер"),"Wrong-size normal is rejected without corrupting hands");
            c.Hands.Desk.NormalPaths[0]=desk;c.Lighting.Enabled=false;scene.RefreshLighting();Settle();
            check(Pixels().SequenceEqual(ClothingRenderer.Pixels(source)),"Disabling lighting restores arm source");
            c.Lighting.Enabled=true;c.Lighting.LightTarget="head";scene.RefreshLighting();Settle();
            check(Pixels().SequenceEqual(ClothingRenderer.Pixels(source)),"Head-only light leaves arms unchanged");
            c.Lighting.LightTarget="scene";c.Clothing.Enabled=true;
            c.Clothing.Current.Color="#3388DD";var fabric=new ClothLayer{WholeLayer=true};c.Clothing.Current.Layers[pngPath]=fabric;
            scene.RefreshClothing();Settle();
            var coloured=ClothingRenderer.Apply(source,c.Clothing.Current,fabric);
            check(Pixels().SequenceEqual(ClothingRenderer.Pixels(NormalMapLighting.Apply(coloured,PngFile.Read(desk),c.Lighting)!)),"Hand normals light the recoloured fabric rather than replacing clothing");
            var reloaded=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!;var copy=c.Duplicate();copy.Hands.Desk.NormalPaths[0]="other";
            check(reloaded.Hands.Desk.NormalPath(0)==desk&&c.Hands.Desk.NormalPath(0)==desk,"Per-pose normal paths persist and copy independently");
            var warp=new HandImageWarp(source,12,12,4);warp.Update(2,1);var replacement=Solid(200,100,80);warp.ReplaceSource(replacement);
            var expected=new HandImageWarp(replacement,12,12,4);expected.Update(2,1);
            check(ClothingRenderer.Pixels(warp.Image).SequenceEqual(ClothingRenderer.Pixels(expected.Image)),"Replacing hand light preserves finger deformation");
        }finally{System.Threading.SynchronizationContext.SetSynchronizationContext(context);foreach(var path in Directory.GetFiles(root))File.Delete(path);Directory.Delete(root);}
    }
}
