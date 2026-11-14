using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

internal static class MouthLayerChecks
{
    public static void Run(Action<bool,string> check)
    {
        double a=0,b=0;
        for(int i=0;i<30;i++)a=MouthLayerOptions.Smooth(a,1,1.0/60);
        for(int i=0;i<60;i++)b=MouthLayerOptions.Smooth(b,1,1.0/120);
        check(Math.Abs(a-b)<1e-9 && a>.99 && a<=1,"Mouth smoothing is frame-rate independent and bounded");
        check(MouthLayerOptions.Smooth(0,1,.016)>1-MouthLayerOptions.Smooth(1,0,.016),"Mouth opens faster than it closes");
        var c=new AvatarEmotion();c.MouthLayer.Enabled=true;c.MouthLayer.X=.4;
        var copy=c.Duplicate();copy.MouthLayer.X=.8;
        check(c.MouthLayer.X==.4 && JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!.MouthLayer.Enabled,"Mouth layer persists and copies independently");
        c.MouthLayer.VowelPaths[VowelShape.O] = "o.png";
        var vowelCopy = c.Duplicate(); vowelCopy.MouthLayer.VowelPaths[VowelShape.O] = "other.png";
        check(c.MouthLayer.VowelPaths[VowelShape.O] == "o.png" && vowelCopy.MouthLayer.VowelPaths[VowelShape.O] == "other.png", "Mouth vowel paths copy independently");
        var scene=new AvatarView();scene.ConfigureCharacter(c);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var image=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[16],8);image.Freeze();
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        scene.MouthLayerPreview=true;
        var render=typeof(AvatarView).GetMethod("RenderMouthLayer",flags)!;
        for(int i=0;i<20;i++)render.Invoke(scene,new object[]{.016,.3});
        var scale=(ScaleTransform)typeof(AvatarView).GetField("_mouthOpenScale",flags)!.GetValue(scene)!;
        check(scale.ScaleY>.6 && scale.ScaleY<1,"Silent preview smoothly deforms the separate mouth");
        scene.MouthLayerPreview=false;AvatarService.Instance.IsTalking=false;
        for(int i=0;i<100;i++)render.Invoke(scene,new object[]{.016,1.0});
        check(scale.ScaleY<.121,"Silence smoothly closes the mouth");
        var sprite=(Grid)typeof(AvatarView).GetField("_mouthSprite",flags)!.GetValue(scene)!;
        var open=(Image)typeof(AvatarView).GetField("_layerOpen",flags)!.GetValue(scene)!;
        var closed=(Image)typeof(AvatarView).GetField("_layerClosed",flags)!.GetValue(scene)!;
        open.Source=closed.Source=image;
        sprite.Measure(new Size(sprite.Width,sprite.Height));
        sprite.Arrange(new Rect(0,0,sprite.Width,sprite.Height));
        bool aligned=true;
        foreach(double opening in new[]{.12,.3,.7,1.0})
        {
            scale.ScaleY=opening;
            var openCenter=open.TranslatePoint(new Point(open.ActualWidth/2,open.ActualHeight/2),sprite);
            var closedCenter=closed.TranslatePoint(new Point(closed.ActualWidth/2,closed.ActualHeight/2),sprite);
            aligned &= (openCenter-closedCenter).Length<.001;
        }
        check(aligned,"Open and closed mouth centers stay aligned throughout deformation");
        BitmapSource Mouth(int top,int height)
        {
            var pixels=new byte[100*100*4];
            for(int y=top;y<top+height;y++)for(int x=20;x<80;x++)pixels[(y*100+x)*4+3]=255;
            return BitmapSource.Create(100,100,96,96,PixelFormats.Bgra32,null,pixels,400);
        }
        open.Source=Mouth(25,50);closed.Source=Mouth(47,6);
        typeof(AvatarView).GetMethod("UpdateMouthAlignment",flags)!.Invoke(scene,null);
        for(int i=0;i<150;i++)render.Invoke(scene,new object[]{.016,1.0});
        check(Math.Abs(scale.ScaleY-.12)<.001 && Math.Abs(scale.ScaleX-1)<.001,"Closing matches actual PNG contour dimensions instead of a fixed width");
        double previous=0; bool smooth=true;
        for(int i=0;i<=100;i++)
        {
            typeof(AvatarView).GetField("_mouthOpening",flags)!.SetValue(scene,i/100.0);
            render.Invoke(scene,new object[]{0.0,1.0});
            smooth &= open.Opacity>=previous && open.Opacity-previous<.05;
            previous=open.Opacity;
        }
        check(smooth,"Closed/open blend is continuous and gradual throughout the transition");
        check(((Grid)scene.FindName("Composite")).IsAncestorOf(sprite),"Mouth shares the head hierarchy with eyes and perspective");
        c.MouthLayer.Procedural = true;
        c.MouthLayer.ProceduralVowels = true;
        c.MouthLayer.Color = "#FF7CD6";
        c.MouthLayer.RestThickness = .08;
        c.MouthLayer.Glow = 17;
        c.MouthLayer.MaxOpening = 1;
        c.MouthLayer.VowelInfluence = 1;
        c.MouthLayer.Liveliness = 0;
        var saved = JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!.MouthLayer;
        var duplicated = c.Duplicate().MouthLayer;
        check(saved.Procedural && saved.ProceduralVowels && saved.Color == "#FF7CD6" && duplicated.RestThickness == .08 && duplicated.Glow == 17,
            "Procedural appearance survives save and duplication");
        scene.RefreshVisualSettings();
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        var procedural = (Border)typeof(AvatarView).GetField("_proceduralMouth",flags)!.GetValue(scene)!;
        var proceduralScale = (ScaleTransform)procedural.RenderTransform;
        check(procedural.Visibility == Visibility.Visible && open.Visibility == Visibility.Collapsed && closed.Visibility == Visibility.Collapsed,
            "Procedural mode replaces PNG mouth layers without deleting them");
        scene.MouthLayerPreview = true;
        for(int i=0;i<80;i++) render.Invoke(scene,new object[]{.016,.3});
        double aWidth = proceduralScale.ScaleX, aHeight = proceduralScale.ScaleY;
        for(int i=0;i<80;i++) render.Invoke(scene,new object[]{.016,5.3});
        check(aHeight > .8 && proceduralScale.ScaleX < aWidth * .6 && proceduralScale.ScaleY < aHeight,
            "Silent test opens procedural mouth and cycles to a narrower U shape");
        scene.MouthLayerPreview = false;
        for(int i=0;i<180;i++) render.Invoke(scene,new object[]{.016,6.0});
        check(Math.Abs(proceduralScale.ScaleY - .08) < .001 && Math.Abs(proceduralScale.ScaleX - 1) < .001 && procedural.RenderTransformOrigin == new Point(.5,.5),
            "Procedural mouth returns to centered resting strip after test stops");
        c.MouthLayer.Procedural = false;
        scene.RefreshVisualSettings();
        check(procedural.Visibility == Visibility.Collapsed && open.Visibility == Visibility.Visible,
            "Switching back restores PNG mode");
        var limited = MouthLayerOptions.ProceduralShape(1, VowelShape.A, .1, .35);
        check(Math.Abs(limited.Height - .35) < .001 && MouthLayerOptions.ProceduralShape(0, VowelShape.A, .1, .35).Height == .1,
            "Opening limit caps loud speech while keeping resting thickness");
        check(procedural.CornerRadius.TopLeft > 0 && procedural.Child != null,
            "Procedural strip has straight sides and adjustable rounded corners");
        check(MicrophoneService.AmplifiedLevel(.08, 2) == 16 && MicrophoneService.AmplifiedLevel(.9, 8) == 100 && MicrophoneService.AmplifiedLevel(0, 8) == 0,
            "Microphone sensitivity amplifies quiet input, caps peaks and preserves silence");
        check(MicrophoneService.AmplifiedLevel(double.NaN, 2) == 0 && MicrophoneService.AmplifiedLevel(.1, double.NaN) == 10,
            "Microphone gain handles invalid values safely");
        c.MouthLayer.Procedural = true; c.MouthLayer.Liveliness = .8; c.MouthLayer.VowelInfluence = .35;
        scene.RefreshVisualSettings();
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        scene.MouthLayerPreview = true;
        for(int i=0;i<100;i++) render.Invoke(scene,new object[]{.016,.3});
        double bend = (double)typeof(AvatarView).GetField("_mouthBend",flags)!.GetValue(scene)!;
        check(bend > .1 && proceduralScale.ScaleX > 1 && proceduralScale.ScaleX < 1.1,
            "Speech envelope gently bends and widens the strip");
        double beforeWidth = proceduralScale.ScaleX;
        render.Invoke(scene,new object[]{.016,5.3});
        check(Math.Abs(proceduralScale.ScaleX-beforeWidth) < .06,
            "Vowel change has bounded gradual influence instead of a snap");
        scene.MouthLayerPreview = false;
        for(int i=0;i<180;i++) render.Invoke(scene,new object[]{.016,6.0});
        check(Math.Abs((double)typeof(AvatarView).GetField("_mouthBend",flags)!.GetValue(scene)!) < .001 && Math.Abs(proceduralScale.ScaleX-1) < .001,
            "Living motion settles back to a straight resting strip");
        var settings = JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!.MouthLayer;
        check(settings.Liveliness == .8 && c.Duplicate().MouthLayer.VowelInfluence == .35,
            "Living mouth settings persist and duplicate");
        var stripType = typeof(AvatarView).Assembly.GetType("VeadotubeMy.Views.ProceduralMouthStrip")!;
        var strip = (FrameworkElement)Activator.CreateInstance(stripType)!;
        strip.Measure(new Size(240,40)); strip.Arrange(new Rect(0,0,240,40)); strip.UpdateLayout();
        stripType.GetMethod("Configure")!.Invoke(strip, new object[]{Colors.Cyan,.4});
        stripType.GetMethod("Bend")!.Invoke(strip, new object[]{8.0});
        strip.UpdateLayout();
        var rendered = new RenderTargetBitmap(240,60,96,96,PixelFormats.Pbgra32); rendered.Render(strip);
        var pixels = new byte[240*60*4]; rendered.CopyPixels(pixels,240*4,0);
        double Center(int x) {
            double mass=0, moment=0;
            for(int y=0;y<60;y++){ double alpha=pixels[(y*240+x)*4+3]; mass+=alpha; moment+=alpha*y; }
            return mass>0 ? moment/mass : -100;
        }
        check(Center(120)>Center(24)+3 && Center(24)>0,
            $"Rendered strip curves down at its middle while ends remain higher ({Center(120):F1}, {Center(24):F1})");
        // Editing uses drafts; neither cancellation nor a character change commits them.
        int saves=0;
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        check(scene.BeginMouthCurveEditing(()=>saves++), "Mouth editor opens for an enabled procedural mouth");
        var draft=(double[])typeof(AvatarView).GetField("_mouthRestDraft",flags)!.GetValue(scene)!;
        var thumbs=(System.Windows.Controls.Primitives.Thumb[])typeof(AvatarView).GetField("_mouthCurveThumbs",flags)!.GetValue(scene)!;
        thumbs[1].RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,3) { RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        check(draft[3]>0 && (bool)typeof(AvatarView).GetField("_editingPaused",flags)!.GetValue(scene)!,
            "Dragging middle handle updates preview draft while avatar is paused");
        draft[3]=.7;
        check(c.MouthLayer.RestCurve[3]==0, "Mouth curve draft does not alter saved settings");
        scene.EndMouthCurveEditing(false);
        check(c.MouthLayer.RestCurve[3]==0 && saves==0 && !scene.IsMouthCurveEditing,
            "Cancelling mouth editing discards draft and does not save");
        check(!(bool)typeof(AvatarView).GetField("_editingPaused",flags)!.GetValue(scene)!,"Cancel restores avatar animation state");
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        scene.BeginMouthCurveEditing(()=>saves++);
        draft=(double[])typeof(AvatarView).GetField("_mouthRestDraft",flags)!.GetValue(scene)!; draft[3]=.4;
        scene.EndMouthCurveEditing(true);
        check(c.MouthLayer.RestCurve[3]==.4 && saves==1, "Applying mouth curve commits once");
        var curveCopy=c.Duplicate(); curveCopy.MouthLayer.RestCurve[3]=-.4;
        check(c.MouthLayer.RestCurve[3]==.4 && JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!.MouthLayer.RestCurve[3]==.4,
            "Curves persist and duplicate independently");
        c.MouthLayer.BendEnabled=false; c.MouthLayer.StretchEnabled=false; c.MouthLayer.GlowEnabled=false; c.MouthLayer.ProceduralVowels=false;
        scene.RefreshVisualSettings(); typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        scene.MouthLayerPreview=true;
        for(int i=0;i<180;i++)render.Invoke(scene,new object[]{.016,.3});
        var blended=(double[])typeof(AvatarView).GetField("_blendedMouthCurve",flags)!.GetValue(scene)!;
        check(Math.Abs(proceduralScale.ScaleX-1)<.001 && Math.Abs((double)typeof(AvatarView).GetField("_mouthBend",flags)!.GetValue(scene)!)<.001 && Math.Abs(blended[3])<.001 && procedural.Effect==null,
            "Bend, width motion and added glow can be disabled independently of mouth opening");
        stripType.GetMethod("SetSurface")!.Invoke(strip,new object[]{Mouth(25,50), false});
        stripType.GetMethod("SetCurve")!.Invoke(strip,new object[]{new double[]{0,0,.5,0,1,0}});
        stripType.GetMethod("Bend")!.Invoke(strip,new object[]{0.0}); strip.UpdateLayout();
        var pngRender=new RenderTargetBitmap(240,60,96,96,PixelFormats.Pbgra32); pngRender.Render(strip); pngRender.CopyPixels(pixels,240*4,0);
        check(pixels[(20*240+120)*4+3]>240 && pixels[(20*240+10)*4+3]==0,
            "PNG strip retains alpha transparency across deformation slices");
        scene.MouthLayerPreview=false;
        scene.BeginMouthCurveEditing(()=>saves++);
        scene.ConfigureCharacter(new AvatarEmotion());
        check(!scene.IsMouthCurveEditing && saves==1,"Changing characters cancels mouth editor without saving");
        c.MouthLayer.BendEnabled=true; c.MouthLayer.Procedural=true; c.MouthLayer.MaxOpening=.4;
        scene.ConfigureCharacter(c); typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        var sizes=new Dictionary<VowelShape,(double W,double H)>();
        foreach(var vowel in new[]{VowelShape.A,VowelShape.O,VowelShape.U,VowelShape.IE,VowelShape.E})
        {
            scene.BeginMouthCurveEditing(()=>saves++,vowel);
            sizes[vowel]=(proceduralScale.ScaleX,proceduralScale.ScaleY);
        }
        check(sizes[VowelShape.U].W<sizes[VowelShape.O].W && sizes[VowelShape.O].W<sizes[VowelShape.E].W && sizes[VowelShape.E].W<sizes[VowelShape.A].W && sizes[VowelShape.A].W<sizes[VowelShape.IE].W,
            "Five vowel previews have distinct ordered widths at identical full opening");
        check(sizes[VowelShape.IE].H<sizes[VowelShape.E].H && sizes[VowelShape.E].H<sizes[VowelShape.A].H && sizes.Values.All(v=>v.H<=.40001),
            "I E and A have distinct openings within the global opening limit");
        var formWidth=(Slider)typeof(AvatarView).GetField("_mouthFormWidth",flags)!.GetValue(scene)!;
        formWidth.Value=72;
        check(Math.Abs(proceduralScale.ScaleX-.72)<.001 && Math.Abs(c.MouthLayer.VowelPoses[VowelShape.E].Width-.95)<.001,
            "Per-vowel width slider updates the preview without saving its draft");
        scene.EndMouthCurveEditing(false);
        check(c.MouthLayer.VowelPoses[VowelShape.E].Width==.95,"Cancel preserves previous vowel pose");
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        scene.BeginMouthCurveEditing(()=>saves++,VowelShape.E); formWidth.Value=73; scene.EndMouthCurveEditing(true);
        var vowelPosesCopy=c.Duplicate();vowelPosesCopy.MouthLayer.VowelPoses[VowelShape.E].Curve[3]=1.2;
        check(Math.Abs(c.MouthLayer.VowelPoses[VowelShape.E].Width-.73)<.001 && c.MouthLayer.VowelPoses[VowelShape.E].Curve[3]!=1.2 && c.MouthLayer.VowelPoses[VowelShape.IE].Width==1.25,
            "Applying E pose leaves I independent and duplicates curves deeply");
        typeof(AvatarView).GetField("_mouthBase",flags)!.SetValue(scene,image);
        c.Head.Enabled=true; c.Head.PerspectiveEnabled=true;
        ((Image)scene.FindName("BodyImage")).Source=image;
        typeof(AvatarView).GetMethod("SyncPerspective",flags)!.Invoke(scene,null);
        scene.BeginMouthCurveEditing(()=>saves++,VowelShape.A);
        var sizeThumbs=(System.Windows.Controls.Primitives.Thumb[])typeof(AvatarView).GetField("_mouthSizeThumbs",flags)!.GetValue(scene)!;
        using var hitHost=new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Mouth hit-test probe") { Width=1000,Height=1000,WindowStyle=unchecked((int)0x80000000),PositionX=-20000,PositionY=-20000 });
        hitHost.RootVisual=scene;
        ((Canvas)typeof(AvatarView).GetField("_mouthCanvas",flags)!.GetValue(scene)!).Visibility=Visibility.Visible;
        ((FrameworkElement)scene.FindName("PlaceholderBox")).Visibility=Visibility.Collapsed;
        scene.Measure(new Size(1000,1000)); scene.Arrange(new Rect(0,0,1000,1000)); scene.UpdateLayout();
        foreach(var handle in sizeThumbs)
        {
            var point=handle.TranslatePoint(new Point(handle.ActualWidth/2,handle.ActualHeight/2),scene);
            var hit=scene.InputHitTest(point) as DependencyObject;
            while(hit!=null && hit!=handle) hit=VisualTreeHelper.GetParent(hit);
            check(hit==handle,"Size handle is reachable through scene hit testing at "+point);
        }
        double beforeHeight=proceduralScale.ScaleY;
        sizeThumbs[3].RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0,15) { RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
        check(proceduralScale.ScaleY>beforeHeight+.1 && proceduralScale.ScaleY>c.MouthLayer.MaxOpening,
            "Lower size handle directly opens A beyond the common base opening");
        var poses=(MouthVowelPose[])typeof(AvatarView).GetField("_mouthPoseDrafts",flags)!.GetValue(scene)!;
        poses[(int)VowelShape.A].Curve[3]=.6;
        typeof(AvatarView).GetMethod("StraightenMouthCurve",flags)!.Invoke(scene,null);
        check(poses[(int)VowelShape.A].Curve[3]==0 && proceduralScale.ScaleY>beforeHeight+.1,
            "Straighten removes smile without losing mouth height");
        double editedHeight=proceduralScale.ScaleY;scene.EndMouthCurveEditing(true);
        check((bool)typeof(AvatarView).GetField("_perspectiveAttached",flags)!.GetValue(scene)! && c.Head.PerspectiveEnabled,
            "Ending mouth editing restores enabled head perspective");
        var stored=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!;
        check(Math.Abs(stored.MouthLayer.VowelPoses[VowelShape.A].Opening!.Value-editedHeight)<.001 && stored.MouthLayer.MaxOpening==.4 && stored.MouthLayer.VowelPoses[VowelShape.E].Opening==null,
            "Custom A opening persists without altering E or global setting");
    }
}
