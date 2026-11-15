using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Dekotube.Services;

public sealed class ArmMotionPart
{
    public ClothLayer Area {get;set;}=new();
    public Point Shoulder {get;set;}=new(.2,.55);
    public Point Elbow {get;set;}=new(.2,.75);
    public Point Wrist {get;set;}=new(.4,.85);
    public double Feather {get;set;}=.02;
    public ArmMotionPart Copy()=>new(){Area=Area.Copy(),Shoulder=Shoulder,Elbow=Elbow,Wrist=Wrist,Feather=Feather};
}
public sealed class ArmMotionMap
{
    public bool Enabled {get;set;}=true;
    public List<ArmMotionPart> Parts {get;set;}=Enumerable.Range(0,7).Select(i=>new ArmMotionPart{Shoulder=new(i==1?.8:.2,.55),Elbow=new(i==1?.85:.2,.75),Wrist=new(i==1?.8:.4,.85)}).ToList();
    public ArmMotionMap Copy()=>new(){Enabled=Enabled,Parts=Parts.Select(p=>p.Copy()).ToList()};
    public static readonly string[] Names={"Рука слева на изображении","Рука справа на изображении","Указательный слева / клик мыши","Указательный справа / клавиши","Большой палец слева","Большой палец справа","Геймпад · общее движение"};
    public static double AlongArm(Point p,ArmMotionPart part)
    {
        var a=part.Elbow-part.Shoulder;var b=part.Wrist-part.Elbow;
        double la=a.Length,lb=b.Length;
        (double Distance,double Progress) Segment(Point start,Vector delta,double before,double length){
            double t=delta.LengthSquared<1e-10?0:Math.Clamp(Vector.Multiply(p-start,delta)/delta.LengthSquared,0,1);
            return ((p-(start+delta*t)).LengthSquared,(before+t*length)/Math.Max(1e-6,la+lb));
        }
        // Blend continuously around a bent elbow; choosing the nearest segment
        // introduced a jump in displacement along its Voronoi boundary.
        var first=Segment(part.Shoulder,a,0,la);var second=Segment(part.Elbow,b,la,lb);
        double softness=Math.Pow(Math.Max(.01,(la+lb)*.12),2);
        double wa=1/Math.Pow(first.Distance+softness,2),wb=1/Math.Pow(second.Distance+softness,2);
        double v=(first.Progress*wa+second.Progress*wb)/(wa+wb);
        double start=la<1e-6?1:Math.Clamp(Vector.Multiply(p-part.Shoulder,a)/(la*la*.25),0,1);
        v*=start*start*(3-2*start);
        double wrist=(p-part.Wrist).Length,shoulder=(p-part.Shoulder).Length;
        if(wrist<1e-8)return 1;
        if(shoulder<1e-8)return 0;
        return v*v*(3-2*v);
    }
    // Built only when calibration changes. RGB stores weights, alpha stays opaque.
    public BitmapSource[] Bake(int width,int height)
    {
        width=Math.Clamp(width,1,1024);height=Math.Clamp(height,1,1536);
        var channels=new byte[7][];
        for(int k=0;k<7;k++){
            var part=Parts[k];var mask=ClothingRenderer.Mask(part.Area,width,height);
            var visual=new DrawingVisual();
            visual.Effect=new System.Windows.Media.Effects.BlurEffect{Radius=Math.Clamp(part.Feather,0,.1)*width,RenderingBias=System.Windows.Media.Effects.RenderingBias.Quality};
            using(var dc=visual.RenderOpen())dc.DrawImage(mask,new Rect(0,0,width,height));
            var blurred=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);blurred.Render(visual);
            var bytes=ClothingRenderer.Pixels(blurred);channels[k]=new byte[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){
                int i=y*width+x;double weight=bytes[i*4]/255d;
                if(k<2)weight*=AlongArm(new Point(x/(double)width,y/(double)height),part);
                channels[k][i]=(byte)Math.Clamp(Math.Round(weight*255),0,255);
            }
        }
        var result=new BitmapSource[3];
        for(int t=0;t<3;t++){
            var pixels=new byte[width*height*4];
            for(int i=0;i<width*height;i++){pixels[i*4+3]=255;for(int c=0;c<3;c++){int k=t*3+c;if(k<7)pixels[i*4+2-c]=channels[k][i];}}
            result[t]=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);result[t].Freeze();
        }
        return result;
    }
}
