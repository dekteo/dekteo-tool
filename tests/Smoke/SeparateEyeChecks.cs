using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class SeparateEyeChecks
{
    public static void Run(Action<bool,string> assert)
    {
        var idle = new MouseIdleGaze();
        assert(!idle.IsIdle(new Point(10,10),0,3) && !idle.IsIdle(new Point(10,10),2.9,3) && idle.IsIdle(new Point(10,10),3,3),"Hybrid gaze switches after the configured idle interval");
        assert(!idle.IsIdle(new Point(20,10),3.1,3),"Hybrid gaze returns to mouse on movement");
        idle.Reset(); assert(!idle.IsIdle(new Point(20,10),100,3),"Hybrid gaze reset starts a fresh idle interval");
        var small = new AutomaticGaze(7).Target(0,1); var large=new AutomaticGaze(7).Target(0,3);
        assert(large.Length>small.Length && large.Length<=1,"Automatic gaze strength increases motion within bounds");
        var otherEye = new EyeRegion {X=.7,Y=.3,RotationDegrees=25,ImagePath="right.png",ClosedEyeImagePath="lid.png"};
        otherEye.CopySizeFrom(new EyeRegion {Width=.2,Height=.1,PupilSize=.06});
        assert(otherEye.Width==.2 && otherEye.Height==.1 && otherEye.PupilSize==.06 && otherEye.X==.7 && otherEye.Y==.3 && otherEye.RotationDegrees==25 && otherEye.ImagePath=="right.png" && otherEye.ClosedEyeImagePath=="lid.png","Copy eye size preserves destination position, angle and images");
        var hybrid=new PupilOptions {GazeMode="hybrid",AutoGazeStrength=2,MouseIdleSeconds=5};
        var hybridSaved=JsonSerializer.Deserialize<PupilOptions>(JsonSerializer.Serialize(hybrid))!;
        assert(hybridSaved.EffectiveGazeMode=="hybrid" && hybridSaved.AutoGazeStrength==2 && hybrid.Copy().MouseIdleSeconds==5,"Hybrid gaze settings persist and copy");
        var gaze = new AutomaticGaze(42);
        var first = gaze.Target(0);
        assert(gaze.Target(.5)==first, "Automatic gaze holds its target during a pause");
        assert(gaze.Target(4)==new Vector(), "Automatic gaze returns to centre");
        bool bounded=true;
        for(int t=10;t<200;t+=10) { var target=gaze.Target(t); bounded &= Math.Abs(target.X)<=.35 && Math.Abs(target.Y)<=.175; }
        assert(bounded,"Automatic gaze stays within a small natural range");
        var gazeOptions=new PupilOptions { GazeMode="auto", FollowMouse=false };
        var savedGaze=JsonSerializer.Deserialize<PupilOptions>(JsonSerializer.Serialize(gazeOptions))!;
        assert(savedGaze.EffectiveGazeMode=="auto" && gazeOptions.Copy().EffectiveGazeMode=="auto", "Automatic gaze persists and copies");
        assert(new PupilOptions {FollowMouse=false}.EffectiveGazeMode=="forward" && new PupilOptions().EffectiveGazeMode=="mouse", "Legacy mouse tracking maps to gaze modes");
        var character = new AvatarEmotion();
        assert(character.VisibleSlots().SequenceEqual(new[]{1,2,3,4}), "Eyes: ordinary simple mode keeps four images");
        character.Pupils.Enabled=true;
        assert(character.VisibleSlots().SequenceEqual(new[]{1,3}), "Eyes: separate simple mode needs two images");
        character.AdvancedMouth=true;
        assert(character.VisibleSlots().SequenceEqual(new[]{1,3,5}), "Eyes: separate volume mode needs three images");
        character.UseVowels=true; character.SetVowelEnabled(VowelShape.O,true); character.SetVowelEnabled(VowelShape.IE,true);
        assert(character.VisibleSlots().SequenceEqual(new[]{1,3,9,13}), "Eyes: one image per selected vowel");
        character.SetPath("10","saved-closed.png"); character.Pupils.Left.ClosedEyeImagePath="lid.png";
        character.Pupils.BlinkIntervalMs=2500; character.Pupils.BlinkDurationMs=180;
        var restored=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        restored.Pupils.Enabled=false;
        assert(restored.VisibleSlots().Contains(10) && restored.VowelPaths[3]=="saved-closed.png", "Eyes: switching back restores closed-eye slots and PNG paths");
        assert(restored.Pupils.Left.ClosedEyeImagePath=="lid.png" && restored.Pupils.BlinkIntervalMs==2500 && restored.Pupils.BlinkDurationMs==180,"Eyes: independent blink and eyelids persist");
        var copy=character.Duplicate(); copy.Pupils.BlinkEnabled=false; copy.Pupils.Left.ClosedEyeImagePath="copy.png";
        assert(character.Pupils.BlinkEnabled && character.Pupils.Left.ClosedEyeImagePath=="lid.png", "Eyes: copied eyelids and blink settings are independent");
        // Exercise rendering without opening a window or changing the user's profile.
        var source=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{255,255,255,255},4);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream=new System.IO.MemoryStream(); encoder.Save(stream);
        BitmapImage Image()
        {
            stream.Position=0; var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
            image.StreamSource=stream; image.EndInit(); image.Freeze(); return image;
        }
        var open=Image(); var closed=Image(); var service=AvatarService.Instance;
        service.MouthClosedEyesOpen=open; service.MouthClosedEyesClosed=closed; service.IsTalking=false;
        var view=new AvatarView(); var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var ordinaryBlink=new BlinkService(service); view.GetType().GetField("_blink",flags)!.SetValue(view,ordinaryBlink);
        view.ConfigureCharacter(character);
        ((System.Windows.Controls.Image)view.FindName("LeftPupilOverlay")).Source=open;
        ((System.Windows.Controls.Image)view.FindName("LeftClosedEyeImage")).Source=closed;
        view.GetType().GetField("_eyesClosed",flags)!.SetValue(view,true);
        view.RefreshFromService();
        assert(ReferenceEquals(((System.Windows.Controls.Image)view.FindName("StateImage")).Source,open),"Eyes: ordinary blink cannot replace mouth PNG in separate mode");
        character.Pupils.FollowMouse=false;
        view.GetType().GetField("_gaze",flags)!.SetValue(view,new Vector(.8,.6));
        view.RefreshVisualSettings();
        var offset=(TranslateTransform)view.FindName("LeftPupilOffset");
        assert(offset.X==0 && offset.Y==0 && ((Canvas)view.FindName("LeftEyeClip")).Visibility==Visibility.Visible,"Eyes: disabled mouse tracking centres visible pupils");
        var noTrackingCopy=character.Duplicate(); noTrackingCopy.Pupils.FollowMouse=true;
        var noTrackingReload=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        assert(!character.Pupils.FollowMouse && !noTrackingReload.Pupils.FollowMouse && noTrackingCopy.Pupils.FollowMouse,"Eyes: mouse tracking setting persists and copies independently");
        assert(JsonSerializer.Deserialize<PupilOptions>("{}")!.FollowMouse,"Eyes: legacy profiles retain mouse tracking");
        view.SetEyesEnabled(false); view.PupilBlinkService.BlinkNow();
        assert(((Canvas)view.FindName("LeftEyeClip")).Visibility==Visibility.Collapsed && ((Grid)view.FindName("LeftClosedEye")).Visibility==Visibility.Visible,"Eyes: independent blink works with ordinary blink disabled");
        assert(ReferenceEquals(((System.Windows.Controls.Image)view.FindName("StateImage")).Source,open),"Eyes: separate blink leaves character image unchanged");
        view.PupilBlinkService.Stop();
        assert(((Canvas)view.FindName("LeftEyeClip")).Visibility==Visibility.Visible && ((Grid)view.FindName("LeftClosedEye")).Visibility==Visibility.Collapsed,"Eyes: reopening restores pupil and hides eyelid");
        ((System.Windows.Controls.Image)view.FindName("LeftClosedEyeImage")).Source=null;
        view.PupilBlinkService.BlinkNow();
        assert(((Canvas)view.FindName("LeftEyeClip")).Visibility==Visibility.Collapsed && ((Grid)view.FindName("LeftClosedEye")).Visibility==Visibility.Collapsed,"Eyes: missing eyelid uses hide-pupil blink");
        view.PupilBlinkService.Stop();
        string layerFile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"dekteo-layer-"+Guid.NewGuid().ToString("N")+".png");
        try
        {
            System.IO.File.WriteAllBytes(layerFile,stream.ToArray());
            character.Pupils.Left.MaskImagePath=character.Pupils.Left.WhiteImagePath=character.Pupils.Left.ForegroundImagePath=layerFile;
            view.RefreshVisualSettings();
            var clip=(Canvas)view.FindName("LeftEyeClip");
            assert(clip.Clip is RectangleGeometry && clip.OpacityMask is ImageBrush { IsFrozen:true },"Layers: PNG mask replaces ellipse with cached alpha mask");
            var mask=clip.OpacityMask; var geometry=clip.Clip;
            view.GetType().GetMethod("RenderFrame",flags)!.Invoke(view,null);
            assert(ReferenceEquals(mask,clip.OpacityMask) && ReferenceEquals(geometry,clip.Clip),"Layers: render frame preserves mask and geometry");
            var canvas=(Canvas)view.FindName("PupilCanvas");
            assert(canvas.Children.IndexOf((UIElement)view.FindName("LeftEyeForeground"))>canvas.Children.IndexOf(clip) && ((System.Windows.Controls.Image)view.FindName("LeftEyeWhite")).Source!=null,"Layers: foreground is above pupil and white is loaded");
            var savedLayers=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
            var layerCopy=character.Duplicate(); layerCopy.Pupils.Left.MaskImagePath="copy.png";
            assert(savedLayers.Pupils.Left.ForegroundImagePath==layerFile && character.Pupils.Left.MaskImagePath==layerFile,"Layers: paths persist and copy independently");
            view.PupilBlinkService.BlinkNow();
            assert(clip.Visibility==Visibility.Collapsed && ((Grid)view.FindName("LeftEyeForeground")).Visibility==Visibility.Collapsed,"Layers: blink hides white, pupil and open-eye foreground");
            view.PupilBlinkService.Stop();
            character.Pupils.Left.MaskImagePath=""; view.RefreshVisualSettings();
            assert(clip.Clip is EllipseGeometry && clip.OpacityMask==null,"Layers: removing mask restores legacy ellipse");
        }
        finally { System.IO.File.Delete(layerFile); }
        ordinaryBlink.Dispose();
        service.MouthClosedEyesOpen=service.MouthClosedEyesClosed=null!;
    }
}
