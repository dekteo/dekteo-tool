using System.Windows.Media;
using System.Windows.Threading;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public partial class AvatarView
{
    private object? _protectionKey;
    private Brush _protectionBrush=ColorCorrectionEffect.NoProtection;
    private Brush ScreenProtection()
    {
        var s=_character!.ScreenEffect;double aw=Artwork.Width,ah=Artwork.Height;
        object key=(aw,ah,s.X,s.Y,s.Width,s.Height,s.CornerRadius,s.CurveTop,s.CurveBottom,s.CurveLeft,s.CurveRight,s.SkewX,s.SkewY);
        if(Equals(key,_protectionKey))return _protectionBrush;
        double x=Math.Clamp(s.X,0,.99)*aw,y=Math.Clamp(s.Y,0,.99)*ah,w=Math.Clamp(s.Width,.01,1)*aw,h=Math.Clamp(s.Height,.01,1)*ah;
        var geometry=ScreenZoneGeometry.Create(new System.Windows.Rect(x,y,w,h),s).Clone();
        geometry.Transform=new SkewTransform(Math.Clamp(s.SkewX,-12,12),Math.Clamp(s.SkewY,-12,12),x+w/2,y+h/2);
        var drawing=new DrawingGroup();drawing.Children.Add(new GeometryDrawing(Brushes.Black,null,new RectangleGeometry(new System.Windows.Rect(0,0,aw,ah))));drawing.Children.Add(new GeometryDrawing(Brushes.White,null,geometry));
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.PushTransform(new ScaleTransform(512/aw,512/ah));dc.DrawDrawing(drawing);dc.Pop();}
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(512,512,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();var brush=new ImageBrush(bitmap){Stretch=Stretch.Fill};brush.Freeze();_protectionKey=key;return _protectionBrush=brush;
    }
    private readonly DispatcherTimer _screenLightTimer=new();
    private bool _screenLightBusy;
    private bool _screenLightReady;
    private int _screenLightVersion;
    private Color _screenLeft=Colors.Black,_screenRight=Colors.Black,_targetLeft=Colors.Black,_targetRight=Colors.Black;
    private System.Windows.Media.Media3D.Vector3D _smoothLeft,_smoothRight;
    private readonly List<ColorCorrectionEffect> _screenLightEffects=new();
    public string ScreenLightStatus {get;private set;}="Выключено";
    public (Color Left,Color Right) ScreenLightColors=>( _screenLeft,_screenRight );
    public event EventHandler? ScreenLightChanged;
    private void InitializeScreenLight()
    {
        _screenLightTimer.Tick+=async (_,_)=>{
            if(_screenLightBusy||_character?.ScreenLight.Enabled!=true)return;
            var options=_character.ScreenLight.Copy();int version=_screenLightVersion;_screenLightBusy=true;
            try{var result=await Task.Run(()=>ScreenLightCapture.Capture(options));if(version!=_screenLightVersion)return;
                _screenLightReady=true;_targetLeft=result.Left;_targetRight=result.Right;ScreenLightStatus=$"Обновляется · захват {result.Ms:0.0} мс · {options.Fps} раз/с";
            }catch(Exception ex){if(version==_screenLightVersion){_screenLightReady=false;_targetLeft=_targetRight=Colors.Black;ScreenLightStatus=ex.Message;}}
            finally{_screenLightBusy=false;ScreenLightChanged?.Invoke(this,EventArgs.Empty);}
        };
        Loaded+=(_,_)=>RefreshScreenLight();Unloaded+=(_,_)=>{_screenLightTimer.Stop();_screenLightVersion++;};
    }
    public void RefreshScreenLight()
    {
        _screenLightVersion++;_screenLightTimer.Stop();
        if(_character==null)return;_character.ScreenLight??=new();
        var o=_character.ScreenLight;
        if(o.Enabled&&IsLoaded&&o.Width>0&&o.Height>0){_screenLightTimer.Interval=TimeSpan.FromSeconds(1.0/Math.Clamp(o.Fps,1,15));_screenLightTimer.Start();ScreenLightStatus="Ожидание цветов…";}
        else {_screenLightReady=false;ScreenLightStatus=o.Enabled?"Выберите область экрана":"Выключено";_screenLeft=_screenRight=_targetLeft=_targetRight=Colors.Black;_smoothLeft=_smoothRight=new();}
        RefreshColorCorrection();UpdateRenderingSubscription();ScreenLightChanged?.Invoke(this,EventArgs.Empty);
    }
    private void RenderScreenLight(double dt)
    {
        if(_character?.ScreenLight.Enabled!=true)return;
        double blend=1-Math.Exp(-Math.Max(0,dt)/Math.Clamp(_character.ScreenLight.SmoothSeconds,.05,3));
        static Color Blend(ref System.Windows.Media.Media3D.Vector3D a,Color b,double t){a+=(new System.Windows.Media.Media3D.Vector3D(b.R,b.G,b.B)-a)*t;return Color.FromRgb((byte)Math.Clamp(Math.Round(a.X),0,255),(byte)Math.Clamp(Math.Round(a.Y),0,255),(byte)Math.Clamp(Math.Round(a.Z),0,255));}
        _screenLeft=Blend(ref _smoothLeft,_targetLeft,blend);_screenRight=Blend(ref _smoothRight,_targetRight,blend);
        foreach(var effect in _screenLightEffects){effect.SetLight(_screenLeft,_screenRight,_character.ScreenLight.Strength);effect.SetDarkness(_screenLightReady && _character.ScreenLight.Darken?_character.ScreenLight.DarkenStrength:0);}
        if(_character.ScreenLight.ProtectScreen && Composite.Effect is ColorCorrectionEffect head)head.Protection=ScreenProtection();
    }
}
