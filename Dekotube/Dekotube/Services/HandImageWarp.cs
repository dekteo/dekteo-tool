using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Dekotube.Services;
// Deforms a small region of one intact premultiplied PNG. No separate finger sprites or masks.
public sealed class HandImageWarp
{
    public WriteableBitmap Image {get;}
    private readonly byte[] _source, _region;
    private readonly double[] _weights;
    private readonly int _width,_height,_stride;
    private readonly Int32Rect _rect;
    private double _lastX=double.NaN,_lastY=double.NaN;
    public HandImageWarp(BitmapSource source,double x,double y,double radius)
    {
        var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
        _width=converted.PixelWidth;_height=converted.PixelHeight;_stride=_width*4;
        _source=new byte[_stride*_height];converted.CopyPixels(_source,_stride,0);
        Image=new WriteableBitmap(converted);
        radius=Math.Clamp(radius,2,150);int reach=(int)Math.Ceiling(radius*3+8);
        int left=Math.Clamp((int)x-reach,0,_width-1),top=Math.Clamp((int)y-reach,0,_height-1);
        _rect=new Int32Rect(left,top,Math.Min(_width-left,reach*2+1),Math.Min(_height-top,reach*2+1));
        _region=new byte[_rect.Width*_rect.Height*4];_weights=new double[_rect.Width*_rect.Height];
        for(int row=0;row<_rect.Height;row++)for(int col=0;col<_rect.Width;col++){
            double distance=Math.Sqrt(Math.Pow(left+col-x,2)+Math.Pow(top+row-y,2))/radius;
            _weights[row*_rect.Width+col]=distance>=3?0:Math.Exp(-distance*distance/2)*Math.Min(1,(3-distance)*2);
        }
    }
    public void ReplaceSource(BitmapSource source)
    {
        if(source.PixelWidth!=_width||source.PixelHeight!=_height)return;
        var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
        converted.CopyPixels(_source,_stride,0);
        Image.WritePixels(new Int32Rect(0,0,_width,_height),_source,_stride,0);
        double x=_lastX,y=_lastY;_lastX=_lastY=double.NaN;
        if(double.IsFinite(x)&&double.IsFinite(y))Update(x,y);
    }
    public void Update(double dx,double dy)
    {
        dx=double.IsFinite(dx)?Math.Clamp(dx,-8,8):0;dy=double.IsFinite(dy)?Math.Clamp(dy,-8,8):0;
        if(Math.Abs(dx-_lastX)<.015&&Math.Abs(dy-_lastY)<.015)return;
        _lastX=dx;_lastY=dy;
        for(int row=0;row<_rect.Height;row++)for(int col=0;col<_rect.Width;col++){
            int dest=(row*_rect.Width+col)*4;double weight=_weights[dest/4];
            double sx=_rect.X+col-dx*weight,sy=_rect.Y+row-dy*weight;
            int ix=(int)Math.Floor(sx),iy=(int)Math.Floor(sy);double fx=sx-ix,fy=sy-iy;
            if(weight==0){int original=(_rect.Y+row)*_stride+(_rect.X+col)*4;for(int ch=0;ch<4;ch++)_region[dest+ch]=_source[original+ch];continue;}
            int p00=iy*_stride+ix*4,p10=p00+4,p01=p00+_stride,p11=p01+4;
            bool a=ix>=0&&iy>=0&&ix<_width&&iy<_height,b=ix+1>=0&&iy>=0&&ix+1<_width&&iy<_height;
            bool c=ix>=0&&iy+1>=0&&ix<_width&&iy+1<_height,d=ix+1>=0&&iy+1>=0&&ix+1<_width&&iy+1<_height;
            double w00=(1-fx)*(1-fy),w10=fx*(1-fy),w01=(1-fx)*fy,w11=fx*fy;
            for(int ch=0;ch<4;ch++)_region[dest+ch]=(byte)Math.Clamp(Math.Round((a?_source[p00+ch]*w00:0)+(b?_source[p10+ch]*w10:0)+(c?_source[p01+ch]*w01:0)+(d?_source[p11+ch]*w11:0)),0,255);
        }
        Image.WritePixels(_rect,_region,_rect.Width*4,0);
    }
}
