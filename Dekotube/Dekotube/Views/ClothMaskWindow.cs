using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Dekotube.Services;
namespace Dekotube.Views;

public sealed class ClothMaskWindow : Window
{
    public ClothLayer Result {get;}
    public ClothMaskWindow(BitmapSource source,ClothLayer original,string? title=null,string? hint=null)
    {
        Result=original.Copy();Title=title??"Маска ткани · закрасьте только одежду";Width=820;Height=820;
        Background=new SolidColorBrush(Color.FromRgb(25,33,43));Foreground=Brushes.White;
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;
        var bar=new StackPanel();DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        bar.Children.Add(EditorControls.Label(hint??"Бирюзовое — ткань для перекраски и принта. Кисть добавляет, ластик исключает перчатки, молнию и устройство. Исходный PNG не меняется."));
        var erase=new CheckBox{Content="Ластик",Foreground=Brushes.White};bar.Children.Add(erase);
        double size=.05;EditorControls.Slider(bar,"Кисть (% ширины PNG)",5,.5,20,v=>size=v/100,"0.0");
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};bar.Children.Add(buttons);
        double w=600,h=w*source.PixelHeight/source.PixelWidth;
        var canvas=new Canvas{Width=w,Height=h,Background=Brushes.Transparent,Cursor=Cursors.Cross};
        canvas.Children.Add(new Image{Source=source,Width=w,Height=h,IsHitTestVisible=false});
        var overlay=new Image{Width=w,Height=h,IsHitTestVisible=false};canvas.Children.Add(overlay);
        var scroll=new ScrollViewer{Content=canvas,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void Preview(){
            int mw=(int)w,mh=(int)Math.Ceiling(h);var pixels=ClothingRenderer.Pixels(ClothingRenderer.Mask(Result,mw,mh));
            for(int i=0;i<pixels.Length;i+=4){byte alpha=(byte)(pixels[i]*.45);pixels[i]=220;pixels[i+1]=220;pixels[i+2]=0;pixels[i+3]=alpha;}
            overlay.Source=BitmapSource.Create(mw,mh,96,96,PixelFormats.Bgra32,null,pixels,mw*4);
        }
        EditorControls.Button(buttons,"Весь слой",()=>{Result.WholeLayer=true;Result.Mask.Clear();Preview();});
        EditorControls.Button(buttons,"Очистить",()=>{Result.WholeLayer=false;Result.Mask.Clear();Preview();});
        EditorControls.Button(buttons,"Отменить мазок",()=>{if(Result.Mask.Count>0)Result.Mask.RemoveAt(Result.Mask.Count-1);Preview();});
        var footer=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        EditorControls.Button(footer,"Сохранить маску",()=>DialogResult=true);
        EditorControls.Button(footer,"Отмена",()=>DialogResult=false);
        root.Children.Add(scroll);
        ClothStroke? stroke=null;Polyline? line=null;
        void Add(Point p){
            p=new Point(Math.Clamp(p.X,0,w),Math.Clamp(p.Y,0,h));
            stroke!.Points.Add(new Point(p.X/w,p.Y/h));line!.Points.Add(p);
        }
        canvas.MouseLeftButtonDown+=(_,e)=>{
            stroke=new(){Size=size,Erase=erase.IsChecked==true};Result.Mask.Add(stroke);
            line=new Polyline{Stroke=stroke.Erase?Brushes.OrangeRed:Brushes.Turquoise,Opacity=.6,StrokeThickness=size*w,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false};canvas.Children.Add(line);
            Add(e.GetPosition(canvas));canvas.CaptureMouse();e.Handled=true;
        };
        canvas.MouseMove+=(_,e)=>{if(stroke!=null&&e.LeftButton==MouseButtonState.Pressed)Add(e.GetPosition(canvas));};
        void Finish(){if(stroke==null)return;stroke=null;if(line!=null)canvas.Children.Remove(line);line=null;canvas.ReleaseMouseCapture();Preview();}
        canvas.MouseLeftButtonUp+=(_,_)=>Finish();canvas.LostMouseCapture+=(_,_)=>Finish();
        Preview();
    }
}
