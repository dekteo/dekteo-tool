using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;
using Dekotube.Views;

internal static class NaturalMotionChecks
{
    public static void Run(Action<bool,string> assert)
    {
        assert(NaturalMotion.HeadTarget(.6,.7,3)==0 && NaturalMotion.HeadTarget(1,.7,3)==3 && NaturalMotion.HeadTarget(-1,.7,3)==-3,"Head gaze has dead zone, symmetric response and bounded angle");
        Vector v=new(), pos=new(); double last=0; bool monotone=true;
        for(int i=0;i<120;i++) {pos=NaturalMotion.Smooth(pos,new Vector(1,0),ref v,1.0/60,150);monotone &= pos.X>=last && pos.X<=1;last=pos.X;}
        assert(monotone && pos.X>.999,"Soft gaze reaches target without overshoot");
        Vector v30=new(),v120=new(),p30=new(),p120=new();
        for(int i=0;i<30;i++) p30=NaturalMotion.Smooth(p30,new Vector(.8,.2),ref v30,1.0/30,200);
        for(int i=0;i<120;i++) p120=NaturalMotion.Smooth(p120,new Vector(.8,.2),ref v120,1.0/120,200);
        assert((p30-p120).Length<.000001,"Soft gaze is independent of frame rate");
        assert(NaturalMotion.BlinkPhase(true,0,150)==1 && NaturalMotion.BlinkPhase(true,.05,150)==2 && NaturalMotion.BlinkPhase(false,0,150)==1 && NaturalMotion.BlinkPhase(false,.08,150)==0,"Blink has intermediate closing and slower opening phases");
        var c=new AvatarEmotion { MouthTransitionMs=60, Head=new HeadOptions {FollowGaze=true,GazeAngle=4,GazeThreshold=.6}, Pupils=new PupilOptions {Enabled=true,SoftGaze=true,SmoothBlink=true} };
        c.Pupils.Left.HalfClosedImagePath="half.png";
        var saved=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!;var copy=c.Duplicate(); copy.Pupils.Left.HalfClosedImagePath="other";
        assert(saved.MouthTransitionMs==60 && saved.Head.FollowGaze && saved.Head.GazeAngle==4 && saved.Pupils.SoftGaze && saved.Pupils.SmoothBlink && c.Pupils.Left.HalfClosedImagePath=="half.png","Motion options and intermediate eyelid persist and copy independently");
        var raw=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{255,255,255,255},4);
        BitmapImage Make() {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(raw));using var stream=new MemoryStream();encoder.Save(stream);stream.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}
        var a=Make();var b=Make();var service=AvatarService.Instance;service.IsTalking=false;service.MouthClosedEyesOpen=a;service.MouthOpenEyesOpen=b;
        var view=new AvatarView();view.ConfigureCharacter(c);
        void Set(string field,object value)=>typeof(AvatarView).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(view,value);
        void Invoke(string method)=>typeof(AvatarView).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(view,null);
        T Named<T>(string name)=>(T)view.FindName(name);
        var clock=(Stopwatch)typeof(AvatarView).GetField("_clock",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;
        Named<Image>("LeftHalfEyeImage").Source=a;Named<Image>("LeftClosedEyeImage").Source=a;Named<Image>("LeftPupilOverlay").Source=a;
        Set("_pupilEyesClosed",true); Set("_blinkChanged",clock.Elapsed.TotalSeconds+.01);Invoke("RenderFrame");
        assert(Named<Grid>("LeftHalfEye").Visibility==Visibility.Visible && Named<Canvas>("LeftEyeClip").Visibility==Visibility.Visible,"Intermediate lid keeps pupil visible underneath");
        Set("_blinkChanged",clock.Elapsed.TotalSeconds-.1);Invoke("RenderFrame");
        assert(Named<Grid>("LeftClosedEye").Visibility==Visibility.Visible && Named<Canvas>("LeftEyeClip").Visibility==Visibility.Collapsed,"Fully closed phase hides pupil");
        Named<Image>("LeftHalfEyeImage").Source=null; Set("_blinkChanged",clock.Elapsed.TotalSeconds); Invoke("RenderFrame");
        assert(Named<Grid>("LeftClosedEye").Visibility==Visibility.Visible,"Missing intermediate PNG preserves ordinary blinking");
        service.IsTalking=true;Invoke("UpdateScene");
        assert(ReferenceEquals(Named<Image>("StateImage").Source,b) && ReferenceEquals(Named<Image>("PreviousStateImage").Source,a),"Mouth target changes immediately while previous image supports transition");
        Set("_mouthFadeStart",clock.Elapsed.TotalSeconds-1);Invoke("RenderFrame");
        assert(Named<Image>("PreviousStateImage").Source==null && Named<Grid>("CurrentStateLayer").Opacity==1,"Completed transition releases outgoing image");
        service.IsTalking=false;Invoke("UpdateScene"); c.MouthTransitionMs=0;Invoke("UpdateScene");
        assert(Named<Grid>("CurrentStateLayer").Opacity==1 && Named<Image>("PreviousStateImage").Source==null,"Zero transition duration immediately restores normal rendering");
        service.MouthOpenEyesOpen=service.MouthClosedEyesOpen=null!; service.IsTalking=false;
    }
}
