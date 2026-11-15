using Dekotube.Services;
internal static class PoseChecks
{
    public static void Run()
    {
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine(name);}
        var motion=new PoseMotion(1);Check(motion.Step(.1,false,1)==(0,0),"Disabled starts neutral");
        motion.Preview();double last=0,max=0;bool lag=false;
        for(int i=0;i<1500;i++)
        {
            var p=motion.Step(.01,false,1);if(Math.Abs(p.Body-last)>.02)throw new Exception("Jump in pose");
            max=Math.Max(max,p.Body);lag|=p.Body-p.Head>.1;last=p.Body;
        }
        Check(max>.95 && max<=1,"Preview reaches bounded lean");Check(lag,"Head follows with delay");Check(last<.001 && !motion.Moving,"One preview returns to neutral and stops");
        motion.Preview();for(int i=0;i<40;i++)motion.Step(.1,false,1);motion.CancelPreview();
        for(int i=0;i<100;i++)motion.Step(.1,false,1);Check(!motion.Moving,"Cancel settles without leaving a pose");
        var a=new PoseMotion(5);var b=new PoseMotion(5);a.Preview();b.Preview();
        (double Body,double Head) p30=default,p120=default;
        for(int i=0;i<120;i++)p30=a.Step(1.0/30,false,1);
        for(int i=0;i<480;i++)p120=b.Step(1.0/120,false,1);
        Check(Math.Abs(p30.Head-p120.Head)<.02,"Similar motion at 30 and 120 FPS");
        var c=new AvatarEmotion();c.Head.PoseEnabled=true;c.Head.PoseAmount=.7;var copy=c.Duplicate();copy.Head.PoseAmount=2;
        Check(c.Head.PoseAmount==.7,"Pose settings copy independently");
        Check(System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(System.Text.Json.JsonSerializer.Serialize(c))!.Head.PoseEnabled,"Pose settings persist");
        motion.Reset();Check(!motion.Moving && motion.Step(0,false,1)==(0,0),"Reset clears pose");
    }
}
