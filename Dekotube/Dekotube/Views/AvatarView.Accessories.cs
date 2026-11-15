using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Dekotube.Services;
namespace Dekotube.Views;
public partial class AvatarView
{
    private readonly Dictionary<string,double> _badgeTests=new();
    private readonly Canvas[] _accessoryHosts=Enumerable.Range(0,4).Select(_=>new Canvas {IsHitTestVisible=false}).ToArray();
    private readonly Dictionary<string,(AccessoryOptions Options,Canvas Container,AccessorySprite Sprite,AccessorySwing Swing,double Aspect)> _accessoryItems=new();
    private readonly Dictionary<string,ImageSource?> _accessoryFiles=new();
    private readonly Dictionary<string,double> _accessoryDrive=new();
    private readonly Dictionary<string,(List<ImageSource> Frames,AccessoryAnimation Player)> _accessoryAnimations=new();
    public string AccessoryLoadStatus {get;private set;}="";
    private AccessoryOptions? _accessoryEdit,_accessoryOriginal;
    private bool _accessoryPause;
    private Action? _accessorySave;
    private Canvas? _accessoryHandles;
    public bool IsAccessoryEditing => _accessoryEdit!=null;
    private void InitializeAccessories()
    {
        LivingRig.Children.Insert(0,_accessoryHosts[0]);LivingRig.Children.Add(_accessoryHosts[1]);
        Composite.Children.Insert(0,_accessoryHosts[2]);Composite.Children.Add(_accessoryHosts[3]);
        Artwork.SizeChanged+=(_,_)=>{foreach(var id in _accessoryItems.Keys)LayoutAccessory(id);};
    }
    public void RefreshAccessories()
    {
        if(_character==null||IsAccessoryEditing)return;
        foreach(var host in _accessoryHosts)host.Children.Clear();
        var previous=_accessoryItems.ToDictionary(p=>p.Key,p=>p.Value.Swing);_accessoryItems.Clear();
        var animations=_accessoryAnimations.ToDictionary(p=>p.Key,p=>p.Value.Player);_accessoryAnimations.Clear();AccessoryLoadStatus="";
        _character.Accessories??=new();
        foreach(var o in _character.Accessories)
        {
            if(!_accessoryFiles.TryGetValue(o.Path,out var source))_accessoryFiles[o.Path]=source=LoadImage(o.Path);
            if(source is not System.Windows.Media.Imaging.BitmapSource bitmap){AccessoryLoadStatus="Не найден или не читается PNG: "+o.Name;continue;}
            if(o.Frames?.Count>1)
            {
                var frames=new List<ImageSource>();long pixels=0;
                foreach(var path in o.Frames.Take(128))
                {
                    if(!_accessoryFiles.TryGetValue(path,out var frame))_accessoryFiles[path]=frame=LoadImage(path);
                    if(frame is not System.Windows.Media.Imaging.BitmapSource f || f.PixelWidth!=bitmap.PixelWidth || f.PixelHeight!=bitmap.PixelHeight){AccessoryLoadStatus="Кадры должны быть доступны и иметь одинаковый размер: "+o.Name;frames.Clear();break;}
                    pixels+=(long)f.PixelWidth*f.PixelHeight;if(pixels>32000000){AccessoryLoadStatus="Слишком большой набор кадров: "+o.Name;frames.Clear();break;}frames.Add(frame);
                }
                if(frames.Count>1)_accessoryAnimations[o.Id]=(frames,animations.GetValueOrDefault(o.Id)??new AccessoryAnimation());
            }
            var container=new Canvas {IsHitTestVisible=false,Visibility=o.Visible?Visibility.Visible:Visibility.Hidden};
            var sprite=new AccessorySprite();container.Children.Add(sprite);
            Color glowColor;try{glowColor=(Color)ColorConverter.ConvertFromString(o.PulseColor);}catch{glowColor=Colors.Cyan;}
            if(o.PulseGlow||_badgeTests.ContainsKey(o.Id))container.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=glowColor,ShadowDepth=0,BlurRadius=18,Opacity=0};
            if(o.Metallic){var shine=new ColorCorrectionEffect();shine.SetGlint(.6,o.Shine);shine.SetShineRegion(o);sprite.Effect=shine;}
            _accessoryHosts[(o.Head?2:0)+(o.Behind?0:1)].Children.Add(container);
            _accessoryItems[o.Id]=(o,container,sprite,previous.GetValueOrDefault(o.Id)??new AccessorySwing(),(double)bitmap.PixelHeight/bitmap.PixelWidth);
            LayoutAccessory(o.Id,source);
        }
        foreach(var id in _badgeTests.Keys.Where(id=>!_accessoryItems.ContainsKey(id)).ToArray())_badgeTests.Remove(id);
        var used=_character.Accessories.SelectMany(a=>(a.Frames??new()).Append(a.Path)).ToHashSet();
        foreach(var path in _accessoryFiles.Keys.Where(p=>!used.Contains(p)).ToArray())_accessoryFiles.Remove(path);
        UpdateRenderingSubscription();
    }
    private void LayoutAccessory(string id,ImageSource? source=null)
    {
        var item=_accessoryItems[id];var o=item.Options;
        if(item.Sprite.Effect is ColorCorrectionEffect shine)shine.SetShineRegion(o);
        double w=Math.Clamp(o.Width,.01,2)*Artwork.Width,h=w*item.Aspect*Math.Clamp(o.HeightScale,.2,5);
        item.Container.Width=item.Sprite.Width=w;item.Container.Height=item.Sprite.Height=h;
        Canvas.SetLeft(item.Container,o.X*Artwork.Width-o.PinX*w);Canvas.SetTop(item.Container,o.Y*Artwork.Height-o.PinY*h);
        item.Sprite.Configure(source??_accessoryFiles[o.Path]!,o.PinX,o.PinY,o.RigidFrom);
        item.Sprite.RenderTransform=new TransformGroup{Children=new TransformCollection{new SkewTransform(o.Skew,0,o.PinX*w,o.PinY*h),new RotateTransform(o.Rotation,o.PinX*w,o.PinY*h)}};
        item.Sprite.Bend((IsAccessoryEditing?0:item.Swing.Angle)+o.Bend);
        if(_accessoryHandles!=null&&_accessoryEdit==o)
        {
            _accessoryHandles.Width=w;_accessoryHandles.Height=h;
            foreach(FrameworkElement control in _accessoryHandles.Children)
            {
                switch(control.Tag as string)
                {
                    case "shineArea":control.Width=o.ShineWidth*w;control.Height=o.ShineHeight*h;Canvas.SetLeft(control,o.ShineX*w);Canvas.SetTop(control,o.ShineY*h);break;
                    case "shineStart":Canvas.SetLeft(control,o.ShineX*w-8);Canvas.SetTop(control,o.ShineY*h-8);break;
                    case "shineEnd":Canvas.SetLeft(control,(o.ShineX+o.ShineWidth)*w-8);Canvas.SetTop(control,(o.ShineY+o.ShineHeight)*h-8);break;
                    case "move":control.Width=w;control.Height=h;break;
                    case "pin":Canvas.SetLeft(control,o.PinX*w-8);Canvas.SetTop(control,o.PinY*h-8);break;
                    case "rigid":control.Width=w;Canvas.SetTop(control,o.RigidFrom*h-5);break;
                    case "height":Canvas.SetLeft(control,w/2-8);Canvas.SetTop(control,h-8);break;
                    case "size":Canvas.SetLeft(control,w-8);Canvas.SetTop(control,h-8);break;
                }
            }
        }
    }
    private void RenderAccessories(double dt)
    {
        foreach(var item in _accessoryItems.Values)
        {
            if(_badgeTests.TryGetValue(item.Options.Id,out double remaining)){remaining-=dt;if(remaining<=0)_badgeTests.Remove(item.Options.Id);else _badgeTests[item.Options.Id]=remaining;}
            if(item.Container.Effect is System.Windows.Media.Effects.DropShadowEffect glow)glow.Opacity=(item.Options.PulseGlow||_badgeTests.ContainsKey(item.Options.Id))?Math.Clamp(item.Options.PulseStrength,0,1)*(.25+.75*(.5+.5*Math.Sin(_clock.Elapsed.TotalSeconds*2*Math.PI/Math.Clamp(item.Options.PulseSeconds,1,10)))):0;
            if(!item.Options.Visible)continue;
            if(_accessoryAnimations.TryGetValue(item.Options.Id,out var animation))
            {
                int frame=animation.Player.Step(dt,animation.Frames.Count,item.Options);
                item.Container.Visibility=frame<0?Visibility.Hidden:Visibility.Visible;
                if(frame>=0)item.Sprite.SetFrame(animation.Frames[frame]);
            }
            double drive=EffectTranslation.X*.06+EffectRotation.Angle*.8+(item.Options.Head?HeadRotation.Angle*.8:0);
            if(item.Options.Motion && _accessoryDrive.TryGetValue(item.Options.Id,out var previous))item.Swing.Kick(Math.Clamp(previous-drive,-3,3));
            _accessoryDrive[item.Options.Id]=drive;
            item.Swing.Step(dt,item.Options);item.Sprite.Bend(item.Swing.Angle+item.Options.Bend);
            if(item.Sprite.Effect is ColorCorrectionEffect shine)shine.SetGlint(.6+Math.Sin(item.Swing.Angle*Math.PI/180)*1.8,item.Options.Shine);
        }
    }
    public void TestAccessoryGlow(string id){if(_accessoryItems.ContainsKey(id)){_badgeTests[id]=4;RefreshAccessories();UpdateRenderingSubscription();}}
    public void TestAccessory(string id)
    {
        if(IsAccessoryEditing)return;
        if(_accessoryAnimations.TryGetValue(id,out var animation))animation.Player.Preview();
        else if(_accessoryItems.TryGetValue(id,out var item))item.Swing.Kick(Math.Max(8,item.Options.Amount*3));
        UpdateRenderingSubscription();
    }
    public bool BeginAccessoryPlacement(AccessoryOptions o,Action save,bool shineRegion=false)
    {
        EndAccessoryPlacement(false);EndPlacement();EndMouthCurveEditing(false);EndLightEditing();EditEyeRegion(null);EndScreenRegionEditing(false);
        if(!_accessoryItems.TryGetValue(o.Id,out var item)||!o.Visible)return false;
        _accessoryOriginal=o;o=o.Copy();_accessoryEdit=o;_accessorySave=save;_accessoryPause=_editingPaused;SetEditingPaused(true);SyncPerspective();
        _accessoryItems[o.Id]=(o,item.Container,item.Sprite,item.Swing,item.Aspect);
        ((Canvas)item.Container.Parent).Children.Remove(item.Container);_accessoryHosts[o.Head?3:1].Children.Add(item.Container);
        item.Sprite.Bend(0);item.Container.IsHitTestVisible=true;
        item.Container.Visibility=Visibility.Visible;
        foreach(var host in _accessoryHosts)host.IsHitTestVisible=true;
        _accessoryHandles=new Canvas();item.Container.Children.Add(_accessoryHandles);
        Thumb Handle(string tag,Brush color,double width,double height)
        {
            var thumb=new Thumb {Focusable=true,Tag=tag,Width=width,Height=height,Background=color,Cursor=System.Windows.Input.Cursors.SizeAll};
            var border=new FrameworkElementFactory(typeof(Border));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
            border.SetValue(Border.BorderBrushProperty,Brushes.Turquoise);border.SetValue(Border.BorderThicknessProperty,new Thickness(1));thumb.Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};
            thumb.PreviewMouseLeftButtonDown+=(_,_)=>thumb.Focus();
            if(!shineRegion)thumb.PreviewKeyDown+=(_,e)=>{
                double step=(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Shift)!=0?10:1;
                switch(e.Key){case System.Windows.Input.Key.Left:NudgeAccessory(-step,0);break;case System.Windows.Input.Key.Right:NudgeAccessory(step,0);break;case System.Windows.Input.Key.Up:NudgeAccessory(0,-step);break;case System.Windows.Input.Key.Down:NudgeAccessory(0,step);break;default:return;}e.Handled=true;
            };
            _accessoryHandles.Children.Add(thumb);return thumb;
        }
        if(shineRegion)
        {
            o.ShineRegion=true;
            var area=Handle("shineArea",new SolidColorBrush(Color.FromArgb(70,255,210,60)),1,1);area.ToolTip="Переместить зону блика";
            area.DragDelta+=(_,e)=>{o.ShineX=Math.Clamp(o.ShineX+e.HorizontalChange/item.Container.Width,0,1-o.ShineWidth);o.ShineY=Math.Clamp(o.ShineY+e.VerticalChange/item.Container.Height,0,1-o.ShineHeight);LayoutAccessory(o.Id);};
            var start=Handle("shineStart",Brushes.Gold,16,16);start.ToolTip="Верхний левый угол зоны";
            start.DragDelta+=(_,e)=>{double right=o.ShineX+o.ShineWidth,bottom=o.ShineY+o.ShineHeight;o.ShineX=Math.Clamp(o.ShineX+e.HorizontalChange/item.Container.Width,0,right-.01);o.ShineY=Math.Clamp(o.ShineY+e.VerticalChange/item.Container.Height,0,bottom-.01);o.ShineWidth=right-o.ShineX;o.ShineHeight=bottom-o.ShineY;LayoutAccessory(o.Id);};
            var end=Handle("shineEnd",Brushes.White,16,16);end.ToolTip="Нижний правый угол зоны";
            end.DragDelta+=(_,e)=>{o.ShineWidth=Math.Clamp(o.ShineWidth+e.HorizontalChange/item.Container.Width,.01,1-o.ShineX);o.ShineHeight=Math.Clamp(o.ShineHeight+e.VerticalChange/item.Container.Height,.01,1-o.ShineY);LayoutAccessory(o.Id);};
            LayoutAccessory(o.Id);return true;
        }
        var move=Handle("move",new SolidColorBrush(Color.FromArgb(20,0,255,255)),1,1);move.ToolTip="Переместить аксессуар";
        move.DragDelta+=(_,e)=>{o.X=Math.Clamp(o.X+e.HorizontalChange/Artwork.Width,-1,2);o.Y=Math.Clamp(o.Y+e.VerticalChange/Artwork.Height,-1,2);LayoutAccessory(o.Id);};
        var rigid=Handle("rigid",Brushes.Gold,1,10);rigid.ToolTip="Граница: выше изгибается, ниже — жёсткая подвеска";
        rigid.DragDelta+=(_,e)=>{o.RigidFrom=Math.Clamp(o.RigidFrom+e.VerticalChange/item.Container.Height,o.PinY+.01,1);LayoutAccessory(o.Id);};
        var pin=Handle("pin",Brushes.OrangeRed,16,16);pin.ToolTip="Точка крепления";
        pin.DragDelta+=(_,e)=>{
            double x=Math.Clamp(o.PinX+e.HorizontalChange/item.Container.Width,0,1),y=Math.Clamp(o.PinY+e.VerticalChange/item.Container.Height,0,Math.Min(.95,o.RigidFrom-.01));
            o.X+=(x-o.PinX)*item.Container.Width/Artwork.Width;o.Y+=(y-o.PinY)*item.Container.Height/Artwork.Height;o.PinX=x;o.PinY=y;LayoutAccessory(o.Id);
        };
        var height=Handle("height",Brushes.LimeGreen,16,16);height.ToolTip="Растянуть по высоте";
        height.DragDelta+=(_,e)=>{o.HeightScale=Math.Clamp(o.HeightScale+e.VerticalChange/(item.Container.Width*item.Aspect),.2,5);LayoutAccessory(o.Id);};
        var size=Handle("size",Brushes.White,16,16);size.ToolTip="Размер (пропорции сохраняются)";
        size.DragDelta+=(_,e)=>{o.Width=Math.Clamp(o.Width+e.HorizontalChange/Artwork.Width,.01,2);LayoutAccessory(o.Id);};
        LayoutAccessory(o.Id);return true;
    }
    public void NudgeAccessory(double dx,double dy)
    {if(_accessoryEdit is {} o){o.X=Math.Clamp(o.X+dx/Artwork.Width,-1,2);o.Y=Math.Clamp(o.Y+dy/Artwork.Height,-1,2);LayoutAccessory(o.Id);}}
    public void SetAccessoryShape(double rotation,double skew,double bend)
    {if(_accessoryEdit is {} o){o.Rotation=Math.Clamp(rotation,-180,180);o.Skew=Math.Clamp(skew,-40,40);o.Bend=Math.Clamp(bend,-30,30);LayoutAccessory(o.Id);}}
    public void EndAccessoryPlacement(bool apply)
    {
        if(_accessoryEdit is not {} o)return;
        foreach(var host in _accessoryHosts)host.IsHitTestVisible=false;
        if(_accessoryHandles!=null){foreach(var thumb in _accessoryHandles.Children.OfType<Thumb>())thumb.ReleaseMouseCapture();}
        if(apply&&_accessoryOriginal is {} s){s.X=o.X;s.Y=o.Y;s.Width=o.Width;s.PinX=o.PinX;s.PinY=o.PinY;s.RigidFrom=o.RigidFrom;s.HeightScale=o.HeightScale;s.Rotation=o.Rotation;s.Skew=o.Skew;s.Bend=o.Bend;s.ShineRegion=o.ShineRegion;s.ShineX=o.ShineX;s.ShineY=o.ShineY;s.ShineWidth=o.ShineWidth;s.ShineHeight=o.ShineHeight;}
        var save=_accessorySave;_accessoryEdit=null;_accessoryOriginal=null;_accessoryHandles=null;_accessorySave=null;
        RefreshAccessories();SyncPerspective();SetEditingPaused(_accessoryPause);if(apply)save?.Invoke();
    }
}
