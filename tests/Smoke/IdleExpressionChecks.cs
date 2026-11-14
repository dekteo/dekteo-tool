using System.Reflection;
using System.Text.Json;
using System.Windows.Media;
using VeadotubeMy.Services;
using VeadotubeMy.Views;
internal static class IdleExpressionChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();int checks=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine(name);}
        var o=new IdleExpressionOptions();var m=new IdleExpression(3);
        for(int i=0;i<5000;i++)m.Step(.016,o,false);
        Check(!m.Moving&&m.Mouth==0,"Disabled remains neutral");
        m.Preview();for(int i=0;i<80;i++)m.Step(.016,o,false);
        Check(m.Mouth>.3&&m.Mouth<=o.Strength&&m.Left==0,"Mouth bend preview bounded and independent");
        double before=m.Mouth;m.Step(.016,o,true);Check(m.Mouth>0&&m.Mouth<before,"Speech fades expression smoothly");
        for(int i=0;i<120;i++)m.Step(.016,o,true);Check(!m.Moving&&m.Mouth==0,"Speech returns to neutral");
        o.Enabled=true;o.Interval=6;m.Reset();int episodes=0;bool previous=false;
        for(int i=0;i<18000;i++){m.Step(1.0/60,o,false);if(m.Moving&&!previous)episodes++;previous=m.Moving;}
        Check(episodes>15&&episodes<50,"Automatic gestures separated by rests");
        m.Reset();for(int i=0;i<1000;i++)m.Step(.016,o,true);Check(!m.Moving,"No gestures during speech");
        o.Eyes=false;m.Preview();for(int i=0;i<70;i++)m.Step(.016,o,false);Check(m.Left==0&&m.Mouth>0,"Eyes independently disabled");
        o.Eyes=true;o.Mouth=false;m.Preview(0);for(int i=0;i<70;i++)m.Step(.016,o,false);Check(m.Left>0&&m.Mouth==0,"Mouth independently disabled");
        var a=new IdleExpression(1);var b=new IdleExpression(1);a.Preview(0);b.Preview(0);
        for(int i=0;i<45;i++)a.Step(1.0/30,o,false);for(int i=0;i<180;i++)b.Step(1.0/120,o,false);Check(Math.Abs(a.Left-b.Left)<.02,"Stable across frame rates");
        o.Mouth=true;o.Eyes=true;o.Enabled=false;m.Reset();m.PreviewAll(o);var seen=new HashSet<string>();
        for(int i=0;i<2400;i++){m.Step(.016,o,false);if(m.Status.StartsWith("Тест:"))seen.Add(m.Status);}
        Check(seen.Count==5&&!m.Moving,"Sequence previews five variants and ends");
        o.Widen=false;o.Corner=false;m.Reset();m.PreviewAll(o);seen.Clear();
        for(int i=0;i<1800;i++){m.Step(.016,o,false);if(m.Status.StartsWith("Тест:"))seen.Add(m.Status);}
        Check(seen.Count==3&&!seen.Contains("Тест: Лёгкое раскрытие глаз")&&!seen.Contains("Тест: Поднятие уголка"),"Unchecked variants excluded from sequence");
        o.Widen=true;m.Preview(1);for(int i=0;i<80;i++)m.Step(.016,o,false);Check(m.Left<0&&m.Right<0&&m.Mouth==0,"Widen distinct from squint");
        o.Corner=true;m.Preview(3);for(int i=0;i<80;i++)m.Step(.016,o,false);Check(m.Asymmetry!=0&&m.Mouth==0&&m.Narrowing==0,"Corner independent of bend and width");
        m.Preview(4);for(int i=0;i<80;i++)m.Step(.016,o,false);Check(m.Narrowing>0&&m.Mouth==0&&m.Asymmetry==0,"Narrowing independent of bend and corner");
        m.PreviewAll(o);m.Step(.1,o,false);m.StopPreview();for(int i=0;i<300;i++)m.Step(.016,o,false);Check(!m.Moving,"Stop cancels entire sequence");
        var c=new AvatarEmotion();c.IdleExpression.Enabled=true;var copy=c.Duplicate();copy.IdleExpression.Strength=1;
        Check(c.IdleExpression.Strength==.6&&JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(c))!.IdleExpression.Enabled,"Persistence and independent copy");
        c.Pupils.Enabled=true;var scene=new AvatarView();scene.ConfigureCharacter(c);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        scene.PreviewIdleExpression();for(int i=0;i<80;i++)typeof(AvatarView).GetMethod("RenderIdleExpression",flags)!.Invoke(scene,new object[]{.016});
        var scale=(ScaleTransform)typeof(AvatarView).GetField("_leftExpression",flags)!.GetValue(scene)!;Check(scale.ScaleY<1,"Scene eye transform reacts");
        scene.SetEditingPaused(true);Check(scale.ScaleY==1,"Editing restores neutral eyes");scene.ConfigureCharacter(new AvatarEmotion());Check(scale.ScaleY==1,"Character switch resets");
        Console.WriteLine($"Idle expression checks: {checks}");
    }
}
