using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace VeadotubeMy.Services;

public static class ClothingRenderer
{
    public static byte[] Pixels(BitmapSource source)
    {
        var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        var pixels=new byte[source.PixelWidth*source.PixelHeight*4];converted.CopyPixels(pixels,source.PixelWidth*4,0);return pixels;
    }
    public static BitmapSource Mask(ClothLayer layer,int width,int height)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
            dc.DrawRectangle(layer.WholeLayer?Brushes.White:Brushes.Black,null,new Rect(0,0,width,height));
            foreach(var stroke in layer.Mask){
                double radius=Math.Clamp(stroke.Size,.001,1)*width/2;
                var brush=stroke.Erase?Brushes.Black:Brushes.White;
                var pen=new Pen(brush,radius*2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                Point? previous=null;
                foreach(var p in stroke.Points){var point=new Point(p.X*width,p.Y*height);dc.DrawEllipse(brush,null,point,radius,radius);if(previous is Point start)dc.DrawLine(pen,start,point);previous=point;}
            }
        }
        var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
    }
    public static BitmapSource Apply(BitmapSource source,ClothingLook look,ClothLayer layer,BitmapSource? print=null)
    {
        if(!layer.WholeLayer&&layer.Mask.Count==0)return source;
        Color color;try{color=(Color)ColorConverter.ConvertFromString(look.Color);}catch{return source;}
        int w=source.PixelWidth,h=source.PixelHeight;
        var pixels=Pixels(source);var mask=Pixels(Mask(layer,w,h));byte[]? printPixels=null;
        if(print!=null){
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
                double pw=Math.Clamp(layer.PrintWidth,.005,1)*w,ph=pw*print.PixelHeight/print.PixelWidth;
                dc.PushTransform(new RotateTransform(layer.PrintAngle,layer.PrintX*w,layer.PrintY*h));
                dc.DrawImage(print,new Rect(layer.PrintX*w-pw/2,layer.PrintY*h-ph/2,pw,ph));dc.Pop();
            }
            var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);printPixels=Pixels(bitmap);
        }
        for(int i=0;i<pixels.Length;i+=4){
            if(pixels[i+3]==0||mask[i]==0)continue;
            double a=mask[i]/255d,light=(pixels[i]*.0722+pixels[i+1]*.7152+pixels[i+2]*.2126)/255;
            double shade=Math.Clamp(light*Math.Clamp(look.Brightness,.2,5),0,1.5);
            for(int c=0;c<3;c++){
                double value=Math.Clamp((c==0?color.B:c==1?color.G:color.R)*shade,0,255);
                if(printPixels!=null){double pa=printPixels[i+3]/255d;value=value*(1-pa)+printPixels[i+c]*Math.Clamp(light*2,.15,1.2)*pa;}
                pixels[i+c]=(byte)Math.Clamp(Math.Round(pixels[i+c]*(1-a)+value*a),0,255);
            }
        }
        var result=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,w*4);result.Freeze();return result;
    }
}
