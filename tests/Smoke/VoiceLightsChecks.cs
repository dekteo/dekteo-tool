using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class VoiceLightsChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();int n=0;
        void Check(bool ok,string text){if(!ok)throw new Exception(text);n++;Console.WriteLine(text);}
        var o=new VoiceLightsOptions();var m=new VoiceLightsMotion();
        void Step(bool talking,double level,int frames=120){for(int i=0;i<frames;i++)m.Step(.016,o,talking,level);}
        Step(true,1);Check(m.Levels.All(v=>v==0),"Disabled lights stay dark");o.Enabled=true;
        Step(true,.035);Check(m.Levels[0]>.9&&m.Levels[1]==0&&m.Levels[2]==0,"Quiet voice lights first tier");
        Step(true,.09);Check(m.Levels[0]>.9&&m.Levels[1]>.9&&m.Levels[2]==0,"Medium voice lights two tiers");
        Step(true,.15);Check(m.Levels.All(v=>v>.9),"Loud voice lights all tiers");
        m.Step(.016,o,false,0);Check(m.Levels.All(v=>v>0&&v<1),"Silence fades smoothly");Step(false,0,200);Check(!m.Moving,"Silence reaches zero");
        o.Enabled=false;m.Test();Step(false,0,50);Check(m.Levels.Any(v=>v>0),"Test works without voice or enabling");Step(false,0,500);Check(!m.Moving,"Test ends automatically");
        var c=new AvatarEmotion();c.VoiceLights.Enabled=true;var copy=c.Duplicate();copy.VoiceLights.Points[0].X=.9;Check(c.VoiceLights.Points[0].X==.25,"Points copy independently");
        Check(JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!.VoiceLights.Points.Count==6,"Six points persist");
        var scene=new AvatarView();scene.ConfigureCharacter(c);scene.Measure(new Size(1000,1000));scene.Arrange(new Rect(0,0,1000,1000));scene.UpdateLayout();int saves=0;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        scene.BeginVoiceLightsPlacement(()=>saves++);
        var handles=(Canvas)typeof(AvatarView).GetField("_lightsHandles",flags)!.GetValue(scene)!;
        ((Thumb)handles.Children[0]).RaiseEvent(new DragDeltaEventArgs(30,20));
        Check(c.VoiceLights.Points[0].X==.25,"Dragging changes draft only");scene.EndVoiceLightsPlacement(false);Check(saves==0&&c.VoiceLights.Points[0].X==.25,"Cancel restores position");
        scene.BeginVoiceLightsPlacement(()=>saves++);handles=(Canvas)typeof(AvatarView).GetField("_lightsHandles",flags)!.GetValue(scene)!;((Thumb)handles.Children[0]).RaiseEvent(new DragDeltaEventArgs(30,20));scene.EndVoiceLightsPlacement(true);
        Check(saves==1&&c.VoiceLights.Points[0].X>.25,"Apply saves position once");
        scene.BeginVoiceLightsPlacement(()=>saves++);scene.SelectVoiceLight(4);
        var editingLights=(Ellipse[])typeof(AvatarView).GetField("_voiceLights",flags)!.GetValue(scene)!;
        Check(editingLights[4].Opacity>editingLights[0].Opacity*4,"Selection dims other points");
        double oldX=c.VoiceLights.Points[4].X;scene.NudgeVoiceLight(1,-1);scene.ResizeVoiceLight(1.7);scene.ColorVoiceLight("#FF6030");
        Check(c.VoiceLights.Points[4].X==oldX&&c.VoiceLights.Points[4].Size==1&&c.VoiceLights.Points[4].Color==null,"Nudge size and color stay draft");
        Check(scene.SelectedVoiceLightSize==1.7&&((SolidColorBrush)editingLights[4].Fill).Color==Color.FromRgb(255,96,48),"Size and color preview live");
        scene.EndVoiceLightsPlacement(false);Check(c.VoiceLights.Points[4].Color==null,"Cancel discards point color");
        scene.BeginVoiceLightsPlacement(()=>saves++);scene.SelectVoiceLight(4);scene.NudgeVoiceLight(1,0);scene.ResizeVoiceLight(1.7);scene.ColorVoiceLight("#FF6030");scene.EndVoiceLightsPlacement(true);
        Check(c.VoiceLights.Points[4].X>oldX&&c.VoiceLights.Points[4].Size==1.7&&c.VoiceLights.Points[4].Color=="#FF6030"&&c.VoiceLights.Points[0].Color==null,"Apply preserves independent point position size color");
        scene.TestVoiceLights();for(int i=0;i<50;i++)typeof(AvatarView).GetMethod("RenderVoiceLights",flags)!.Invoke(scene,new object[]{.016});
        var lights=(Ellipse[])typeof(AvatarView).GetField("_voiceLights",flags)!.GetValue(scene)!;Check(lights[0].Opacity>0&&lights[0].Opacity==lights[3].Opacity&&lights[1].Opacity==lights[4].Opacity,"Left and right tiers match");
        var canvas=(Canvas)lights[0].Parent;Check(canvas.Clip==null&&ReferenceEquals(canvas.Parent,typeof(AvatarView).GetField("_faceContent",flags)!.GetValue(scene)),"Lights follow head outside screen mask");
        scene.ConfigureCharacter(new AvatarEmotion());Check(lights.All(l=>l.Opacity==0),"Character switch clears lights");
        Console.WriteLine($"Voice lights checks: {n}; real voice and model not verified.");
    }
}
