using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;

public sealed class ArmMotionWindow:Window
{
    public ArmMotionMap Result {get;private set;}
    public ArmMotionWindow(BitmapSource source,ArmMotionMap? original,bool desk,IEnumerable<KeyValuePair<string,ArmMotionMap>> copies,BitmapSource? body=null)
    {
        Result=original?.Copy()??new();Title=desk?"Разметка движения · клавиатура и мышь":"Разметка движения · геймпад";
        Width=1120;Height=850;MinWidth=860;MinHeight=640;Background=new SolidColorBrush(Color.FromRgb(25,33,43));Foreground=Brushes.White;
        var root=new DockPanel{Margin=new Thickness(12)};Content=root;
        var bottom=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var controls=new StackPanel{Width=285,Margin=new Thickness(0,0,12,0)};
        var scroll=new ScrollViewer{Content=controls,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Width=310};DockPanel.SetDock(scroll,Dock.Left);root.Children.Add(scroll);
        double w=600,h=w*source.PixelHeight/source.PixelWidth;
        var canvas=new Canvas{Width=w,Height=h,Background=Brushes.Transparent};root.Children.Add(new Viewbox{Child=canvas,Stretch=Stretch.Uniform});
        if(body!=null)canvas.Children.Add(new Image{Source=body,Width=w,Height=h,Opacity=.35,IsHitTestVisible=false});
        var image=new Image{Source=source,Width=w,Height=h,IsHitTestVisible=false};canvas.Children.Add(image);
        var overlay=new Image{Width=w,Height=h,IsHitTestVisible=false};canvas.Children.Add(overlay);
        var marks=new Canvas{Width=w,Height=h};canvas.Children.Add(marks);
        var effect=new CustomArmsEffect();
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};var clock=new Stopwatch();bool testing=false;
        int selected=0;
        void Stop(){testing=false;timer.Stop();effect.Set(new(),new(),new());image.Effect=null;overlay.Visibility=marks.Visibility=Visibility.Visible;}
        void Draw(){
            Stop();var part=Result.Parts[selected];
            var mask=ClothingRenderer.Pixels(ClothingRenderer.Mask(part.Area,(int)w,(int)Math.Ceiling(h)));
            for(int i=0;i<mask.Length;i+=4){byte a=(byte)(mask[i]*.4);mask[i]=220;mask[i+1]=220;mask[i+2]=0;mask[i+3]=a;}
            overlay.Source=BitmapSource.Create((int)w,(int)Math.Ceiling(h),96,96,PixelFormats.Bgra32,null,mask,(int)w*4);
            marks.Children.Clear();if(selected>=2)return;
            var points=new[]{part.Shoulder,part.Elbow,part.Wrist};
            var line=new Polyline{Stroke=Brushes.Gold,StrokeThickness=2,IsHitTestVisible=false};foreach(var p in points)line.Points.Add(new Point(p.X*w,p.Y*h));marks.Children.Add(line);
            for(int k=0;k<3;k++){
                int index=k;var handle=new Ellipse{Width=18,Height=18,Fill=k==0?Brushes.OrangeRed:k==1?Brushes.Gold:Brushes.Turquoise,Stroke=Brushes.Black,StrokeThickness=2,Cursor=Cursors.SizeAll,ToolTip=new[]{"Плечо · неподвижно","Локоть","Кисть · полное движение"}[k]};
                Canvas.SetLeft(handle,points[k].X*w-9);Canvas.SetTop(handle,points[k].Y*h-9);marks.Children.Add(handle);
                var label=new TextBlock{Text=new[]{"Плечо","Локоть","Кисть"}[k],Foreground=Brushes.White,Background=Brushes.Black,IsHitTestVisible=false};Canvas.SetLeft(label,points[k].X*w+11);Canvas.SetTop(label,points[k].Y*h-10);marks.Children.Add(label);
                handle.MouseLeftButtonDown+=(_,e)=>{handle.CaptureMouse();e.Handled=true;};
                handle.MouseMove+=(_,e)=>{
                    if(!handle.IsMouseCaptured||e.LeftButton!=MouseButtonState.Pressed)return;
                    var p=e.GetPosition(canvas);var normalized=new Point(Math.Clamp(p.X/w,0,1),Math.Clamp(p.Y/h,0,1));
                    if(index==0)part.Shoulder=normalized;else if(index==1)part.Elbow=normalized;else part.Wrist=normalized;
                    Canvas.SetLeft(handle,normalized.X*w-9);Canvas.SetTop(handle,normalized.Y*h-9);
                    Canvas.SetLeft(label,normalized.X*w+11);Canvas.SetTop(label,normalized.Y*h-10);line.Points[index]=new Point(normalized.X*w,normalized.Y*h);
                };
                handle.MouseLeftButtonUp+=(_,_)=>{handle.ReleaseMouseCapture();Draw();};
            }
        }
        controls.Children.Add(EditorControls.Label("1. Выберите часть. 2. Закрасьте её область. 3. Для рукава перетащите плечо, локоть и кисть. 4. Проверьте движение. Лево/право — на изображении."));
        var enabled=new CheckBox{Content="Использовать эту разметку",IsChecked=Result.Enabled,Foreground=Brushes.White};controls.Children.Add(enabled);enabled.Click+=(_,_)=>Result.Enabled=enabled.IsChecked==true;
        var names=desk?new[]{"Рука с мышью · слева на картинке","Рука на клавиатуре · справа","Палец мыши · нажатие","Пальцы клавиатуры · нажатие"}:new[]{"Рука слева на картинке","Рука справа на картинке","Указательный слева · RT / RB","Указательный справа · LT / LB","Большой слева · ABXY / правый стик","Большой справа · D-pad / левый стик","Геймпад · покачивание"};
        var selector=new ComboBox{ItemsSource=names,SelectedIndex=0};controls.Children.Add(selector);
        selector.SelectionChanged+=(_,_)=>{selected=selector.SelectedIndex;Draw();};
        EditorControls.Button(controls,"✎ Закрасить подвижную область",()=>{
            Stop();var edit=new ClothMaskWindow(source,Result.Parts[selected].Area,"Область движения · "+names[selected],"Бирюзовое — подвижная часть. Отметьте рукав целиком до кисти, либо выбранный палец/устройство. Ластик убирает лишнее; края сгладятся при движении."){Owner=this};
            if(edit.ShowDialog()==true){Result.Parts[selected].Area=edit.Result;Draw();}
        });
        var feather=EditorControls.Slider(controls,"Мягкость края (%)",Result.Parts[0].Feather*100,0,8,v=>{Result.Parts[selected].Feather=v/100;},"0.0");
        selector.SelectionChanged+=(_,_)=>feather.Value=Result.Parts[selected].Feather*100;
        EditorControls.Button(controls,"Очистить выбранную область",()=>{Result.Parts[selected].Area=new();Draw();});
        controls.Children.Add(EditorControls.Label("У плеча движение равно нулю, к кисти усиливается. Пальцы отмечаются отдельно поверх руки. Геймпад получает общее покачивание. Пустые области не двигаются."));
        void Test(bool only){
            Stop();var preview=Result.Copy();if(desk)for(int i=4;i<7;i++)preview.Parts[i].Area=new();
            if(only)for(int i=0;i<preview.Parts.Count;i++)if(i!=selected)preview.Parts[i].Area=new();
            effect.Configure(preview,512,Math.Clamp((int)(512*h/w),1,1536));image.Effect=effect;
            overlay.Visibility=marks.Visibility=Visibility.Collapsed;testing=true;clock.Restart();
            timer.Tag=only;timer.Start();
        }
        EditorControls.Button(controls,"▶ Проверить выбранную часть",()=>Test(true));
        EditorControls.Button(controls,"▶ Проверить всё · 6 секунд",()=>Test(false));
        EditorControls.Button(controls,"■ Остановить",Stop);
        timer.Tick+=(_,_)=>{
            if(!testing)return;double t=clock.Elapsed.TotalSeconds;if(t>6){Stop();return;}
            double x=Math.Sin(t*3)*14,y=Math.Cos(t*2)*8,press=(Math.Sin(t*8)+1)*3;
            bool only=(bool)timer.Tag;Point a=(!only||selected is 0 or 6)?new(x,y):new();Point b=(!only||selected is 1 or 6)?new(desk?-x:x,y):new();
            effect.Set(a,b,new Point(!only||selected==2?press:0,!only||selected==3?press:0),!only||selected==4?new Point(x*.35,y*.35):new(),!only||selected==5?new Point(-x*.35,y*.35):new());
        };
        var templates=copies.ToArray();if(templates.Length>0){
            controls.Children.Add(EditorControls.Label("Начать с разметки другого PNG"));
            var list=new ComboBox{ItemsSource=templates.Select(p=>System.IO.Path.GetFileName(p.Key)).ToArray(),SelectedIndex=0};controls.Children.Add(list);
            EditorControls.Button(controls,"Копировать в черновик",()=>{Result=templates[list.SelectedIndex].Value.Copy();enabled.IsChecked=Result.Enabled;feather.Value=Result.Parts[selected].Feather*100;Draw();});
        }
        controls.Children.Add(EditorControls.Label("Разметка сохраняется для этого PNG и этой позы. Под руками должен оставаться дорисованный торс. Прежняя анимация действует, пока новая разметка не сохранена."));
        EditorControls.Button(bottom,"Сохранить разметку",()=>{Stop();DialogResult=true;});
        EditorControls.Button(bottom,"Отмена",()=>{Stop();DialogResult=false;});
        Closed+=(_,_)=>Stop();Draw();
    }
}
