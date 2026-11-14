using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class AccessoryChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();int count=0;
        void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine(text);}
        var o=new AccessoryOptions {Motion=false};var swing=new AccessorySwing();swing.Kick(30);double max=0;
        for(int i=0;i<2400;i++){swing.Step(1.0/120,o);max=Math.Max(max,Math.Abs(swing.Angle));}
        Check(max>2&&max<35&&!swing.Moving,"Impulse swings and damps to rest");
        var a=new AccessorySwing();var b=new AccessorySwing();a.Kick(30);b.Kick(30);
        for(int i=0;i<120;i++)a.Step(1.0/30,o);for(int i=0;i<480;i++)b.Step(1.0/120,o);
        Check(Math.Abs(a.Angle-b.Angle)<.03,"Swing stable across frame rates");
        Check(AccessorySprite.Centerline(0,100,.3)==new Point(),"Anchor stays fixed");
        var p=AccessorySprite.Centerline(130,100,.3);var q=AccessorySprite.Centerline(230,100,.3);
        Check(Math.Abs((q-p).Length-100)<1e-6,"Rigid tail preserves length");
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
            dc.DrawRectangle(Brushes.Silver,null,new Rect(48,0,4,125));dc.DrawRoundedRectangle(Brushes.SlateGray,new Pen(Brushes.White,2),new Rect(20,125,60,70),8,8);
        }
        var bitmap=new RenderTargetBitmap(100,200,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));var path=System.IO.Path.Combine(AppContext.BaseDirectory,"test-accessory.png");using(var file=System.IO.File.Create(path))png.Save(file);
        var c=new AvatarEmotion();o.Path=path;c.Accessories.Add(o);var copy=c.Duplicate();copy.Accessories[0].X=.1;
        Check(c.Accessories[0].X==.5,"Accessory copy independent");
        Check(System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(System.Text.Json.JsonSerializer.Serialize(c))!.Accessories[0].Path==path,"Accessory persists");
        var scene=new AvatarView();scene.ConfigureCharacter(c);scene.Measure(new Size(1000,1000));scene.Arrange(new Rect(0,0,1000,1000));scene.UpdateLayout();int saves=0;
        Check(scene.BeginAccessoryPlacement(o,()=>saves++),"Placement starts");scene.UpdateLayout();
        var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        Canvas Handles()=>(Canvas)typeof(AvatarView).GetField("_accessoryHandles",flags)!.GetValue(scene)!;
        Handles().Children.OfType<Thumb>().First(t=>(string)t.Tag=="move").RaiseEvent(new DragDeltaEventArgs(30,20));
        Check(o.X==.5,"Drag uses draft, not saved profile");scene.EndAccessoryPlacement(false);Check(o.X==.5&&saves==0,"Cancel preserves original");
        scene.BeginAccessoryPlacement(o,()=>saves++);scene.UpdateLayout();Handles().Children.OfType<Thumb>().First(t=>(string)t.Tag=="move").RaiseEvent(new DragDeltaEventArgs(30,20));scene.EndAccessoryPlacement(true);
        Check(o.X>.5&&saves==1,"Apply commits draft once");
        Check(scene.BeginAccessoryPlacement(o,()=>saves++,true),"Glint region editor opens");scene.UpdateLayout();
        Handles().Children.OfType<Thumb>().First(t=>(string)t.Tag=="shineStart").RaiseEvent(new DragDeltaEventArgs(10,20));
        Check(!o.ShineRegion&&o.ShineX==0,"Glint region edits remain draft");scene.EndAccessoryPlacement(false);Check(!o.ShineRegion&&saves==1,"Glint region cancel preserves profile");
        scene.BeginAccessoryPlacement(o,()=>saves++,true);scene.UpdateLayout();Handles().Children.OfType<Thumb>().First(t=>(string)t.Tag=="shineStart").RaiseEvent(new DragDeltaEventArgs(10,20));scene.EndAccessoryPlacement(true);
        Check(o.ShineRegion&&o.ShineX>0&&o.ShineWidth<1&&saves==2,"Glint region applies resized bounds once");
        scene.BeginAccessoryPlacement(o,()=>saves++);scene.UpdateLayout();
        Handles().Children.OfType<Thumb>().First(t=>(string)t.Tag=="height").RaiseEvent(new DragDeltaEventArgs(0,25));scene.SetAccessoryShape(20,12,10);
        Check(o.HeightScale==1&&o.Rotation==0,"Accessory deformation stays draft");scene.EndAccessoryPlacement(true);
        Check(o.HeightScale>1&&o.Rotation==20&&o.Skew==12&&o.Bend==10,"Accessory deformation applies");
        o.PulseGlow=true;scene.RefreshAccessories();
        System.Windows.Media.Effects.DropShadowEffect? Glow()=>((Canvas[])typeof(AvatarView).GetField("_accessoryHosts",flags)!.GetValue(scene)!).SelectMany(h=>h.Children.OfType<Canvas>()).Select(h=>h.Effect).OfType<System.Windows.Media.Effects.DropShadowEffect>().FirstOrDefault();
        typeof(AvatarView).GetMethod("RenderAccessories",flags)!.Invoke(scene,new object[]{.016});Check(Glow()?.Opacity>0,"Emblem glow pulses");
        o.PulseGlow=false;scene.RefreshAccessories();Check(Glow()==null,"Disabling removes glow effect");scene.TestAccessoryGlow(o.Id);typeof(AvatarView).GetMethod("RenderAccessories",flags)!.Invoke(scene,new object[]{.016});Check(Glow()?.Opacity>0&&!o.PulseGlow,"Glow preview does not enable profile");
        var hat=AccessoryOptions.AttachedPng(path,true,true);c.Accessories.Add(hat);scene.RefreshAccessories();
        var hosts=(Canvas[])typeof(AvatarView).GetField("_accessoryHosts",flags)!.GetValue(scene)!;
        var hatCanvas=hosts[3].Children.OfType<Canvas>().Single();
        Check(!hat.Motion&&Canvas.GetLeft(hatCanvas)==0&&Canvas.GetTop(hatCanvas)==0&&hatCanvas.Width==1000,"Full canvas hat starts aligned and follows head host");
        hat.Behind=true;scene.RefreshAccessories();Check(hosts[2].Children.Count==1&&hosts[3].Children.Count==0,"Hat can move behind head");
        hat.Head=false;scene.RefreshAccessories();Check(hosts[0].Children.Count==1&&hosts[2].Children.Count==0,"Attachment can switch to body");
        scene.BeginAccessoryPlacement(hat,()=>saves++);scene.NudgeAccessory(10,-5);scene.EndAccessoryPlacement(false);
        Check(hat.X==0&&hat.Y==0,"Arrow nudge cancels without changing attachment");
        scene.BeginAccessoryPlacement(hat,()=>saves++);scene.NudgeAccessory(10,-5);scene.UpdateLayout();
        Handles().Children.OfType<Thumb>().First(t=>(string)t.Tag=="size").RaiseEvent(new DragDeltaEventArgs(100,0));scene.EndAccessoryPlacement(true);
        Check(hat.X>0&&hat.Y<0&&hat.Width>1,"Full canvas accessory can enlarge and nudge");
        var cropped=AccessoryOptions.AttachedPng(path,false,false);Check(!cropped.Head&&!cropped.Motion&&cropped.Width==.3,"Cropped costume starts with manual placement size");
        var sprite=new AccessorySprite {Width=100,Height=200};sprite.Configure(bitmap,.5,.02,.625);
        var canvas=new Canvas {Width=220,Height=250,Background=Brushes.Transparent};Canvas.SetLeft(sprite,50);Canvas.SetTop(sprite,20);canvas.Children.Add(sprite);
        canvas.Measure(new Size(220,250));canvas.Arrange(new Rect(0,0,220,250));canvas.UpdateLayout();sprite.Bend(25);
        var bent=new RenderTargetBitmap(220,250,96,96,PixelFormats.Pbgra32);bent.Render(canvas);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bent));using(var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"accessory-bent.png")))encoder.Save(file);
        // Compare translucent, unbent artwork at fractional dimensions to a single image draw.
        var solid=new DrawingVisual();using(var dc=solid.RenderOpen())dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(180,90,140,190)),null,new Rect(0,0,100,200));
        var source=new RenderTargetBitmap(100,200,96,96,PixelFormats.Pbgra32);source.Render(solid);
        var flat=new AccessorySprite{Width=137.3,Height=273.7};flat.Configure(source,.5,.02,.65);
        flat.Measure(new Size(137.3,273.7));flat.Arrange(new Rect(0,0,137.3,273.7));flat.UpdateLayout();
        var actual=new RenderTargetBitmap(140,280,96,96,PixelFormats.Pbgra32);actual.Render(flat);
        var referenceVisual=new DrawingVisual();using(var dc=referenceVisual.RenderOpen())dc.DrawImage(source,new Rect(0,0,137.3,273.7));
        var expected=new RenderTargetBitmap(140,280,96,96,PixelFormats.Pbgra32);expected.Render(referenceVisual);
        var pixels=new byte[140*280*4];var referencePixels=new byte[pixels.Length];actual.CopyPixels(pixels,140*4,0);expected.CopyPixels(referencePixels,140*4,0);
        Check(pixels.SequenceEqual(referencePixels),"Unbent translucent accessory has no band seams at fractional size");
        flat.Bend(20);flat.Bend(0);flat.UpdateLayout();var returned=new RenderTargetBitmap(140,280,96,96,PixelFormats.Pbgra32);returned.Render(flat);returned.CopyPixels(pixels,140*4,0);
        Check(pixels.SequenceEqual(referencePixels),"Returning from bend restores seamless artwork");
        Console.WriteLine($"Accessory checks: {count}");
    }
}
