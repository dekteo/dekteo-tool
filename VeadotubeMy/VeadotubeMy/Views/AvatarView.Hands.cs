using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public partial class AvatarView
{
    private readonly Canvas _handsHost=new(){IsHitTestVisible=false};
    private readonly Image[] _handsImages=Enumerable.Range(0,5).Select(_=>new Image{Stretch=Stretch.Fill,IsHitTestVisible=false}).ToArray();
    private readonly TranslateTransform[] _handsOffsets=Enumerable.Range(0,5).Select(_=>new TranslateTransform()).ToArray();
    private readonly RotateTransform[] _handsRotations=Enumerable.Range(0,5).Select(_=>new RotateTransform()).ToArray();
    private readonly SkewTransform[] _handsBends=Enumerable.Range(0,5).Select(_=>new SkewTransform()).ToArray();
    private readonly HandImageWarp?[] _handWarps=new HandImageWarp?[2];
    private readonly HandsInputReader _handsReader=new();
    private readonly HandsInputReader _deskReader=new();
    private readonly HandsAutoSwitch _handsAuto=new();
    private bool _handsAutoInitialized;
    private readonly Canvas _handsPrevious=new(){IsHitTestVisible=false,Opacity=0};
    private readonly Image[] _handsPreviousImages=Enumerable.Range(0,5).Select(_=>new Image{Stretch=Stretch.Fill}).ToArray();
    private readonly Dictionary<string,ImageSource?> _handsCache=new();
    private double _handsFade=1;
    private bool RuntimeDesk=>_character?.Hands.AutoSwitch==true?_handsAuto.DeskMode:_character?.Hands.DeskMode==true;
    private HandsRig RuntimeRig=>RuntimeDesk?_character!.Hands.Desk:_character!.Hands.Gamepad;
    private readonly LayeredArmsEffect _layeredArmsEffect=new();
    private readonly Dictionary<ArmMotionMap,CustomArmsEffect> _customArmEffects=new();
    private CustomArmsEffect? _customArmEffect;
    private void SetArmMotion(Point left,Point right,Point press,bool desk,Point rightThumb=default,Point leftThumb=default){
        _layeredArmsEffect.Set(left,right,press,desk,rightThumb,leftThumb);
        _customArmEffect?.Set(left,right,press,rightThumb,leftThumb);
    }
    private readonly LayeredFingerMotion _layeredFingerMotion=new();
    private double _armTime;
    private bool RigReady(HandsRig rig)=>(rig.LayeredArms&&rig.Layers.Count==1||rig.Layers.Count==5||rig.WholeHands&&rig.Layers.Count==3)&&rig.Layers.All(p=>!string.IsNullOrWhiteSpace(p)&&_handsCache.GetValueOrDefault(p)!=null);
    public void SelectHandsSet(bool desk){_handsAuto.Reset(desk);_handsAutoInitialized=true;_handsFade=1;_handsPrevious.Opacity=0;RefreshHands();}

    private readonly HandsMotion _handsMotion=new();
    private HandsRig? _handsDraft;private bool _handsWasPaused;private Action? _handsSave;private Thumb? _handsGrip;
    private double _handsTest;
    private bool _handsReady;
    public bool HandsSettingsOpen {get;set;}
    public bool IsHandsEditing=>_handsDraft!=null;
    public string HandsStatus {get;private set;}="Выключено";
    public event EventHandler? HandsStatusChanged;
    private void SetHandsStatus(string text){if(HandsStatus!=text){HandsStatus=text;HandsStatusChanged?.Invoke(this,EventArgs.Empty);}}
    private void InitializeHands()
    {
        LivingRig.Children.Add(_handsPrevious);Panel.SetZIndex(_handsPrevious,2);foreach(var im in _handsPreviousImages)_handsPrevious.Children.Add(im);
        LivingRig.Children.Add(_handsHost);Panel.SetZIndex(_handsHost,2);
        for(int i=0;i<5;i++){_handsImages[i].RenderTransform=new TransformGroup{Children=new TransformCollection{_handsOffsets[i],_handsRotations[i],_handsBends[i]}};_handsHost.Children.Add(_handsImages[i]);}
        Artwork.SizeChanged+=(_,_)=>LayoutHands();
    }
    private void ResetHands(){_customArmEffects.Clear();_customArmEffect=null;EndHandsPlacement(false);_handsTest=0;_handsReader.Reset();_deskReader.Reset();_handsAutoInitialized=false;_handsCache.Clear();_handWarps[0]=_handWarps[1]=null;_handsFade=1;_handsPrevious.Opacity=0;_handsMotion.Reset();foreach(var t in _handsOffsets){t.X=t.Y=0;}foreach(var r in _handsRotations)r.Angle=0;foreach(var bend in _handsBends)bend.AngleX=0;}
    public void RefreshHands()
    {
        if(_character==null)return;_character.Hands??=new();var o=_character.Hands;
        var maps=o.Gamepad.MotionMaps.Values.Concat(o.Desk.MotionMaps.Values).ToHashSet();
        foreach(var stale in _customArmEffects.Keys.Where(m=>!maps.Contains(m)).ToArray())_customArmEffects.Remove(stale);
        if(!_handsAutoInitialized){_handsAuto.Reset(o.DeskMode);_handsAutoInitialized=true;}
        foreach(var path in o.Gamepad.Layers.Concat(o.Desk.Layers).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct())if(!_handsCache.ContainsKey(path))_handsCache[path]=Clothed(path,LoadImage(path));
        var used=o.Gamepad.Layers.Concat(o.Desk.Layers).ToHashSet();foreach(var path in _handsCache.Keys.Where(p=>!used.Contains(p)).ToArray())_handsCache.Remove(path);
        LoadHandsRig();UpdateRenderingSubscription();
        if(_character.Lighting.Enabled){_lightPending=true;if(!_lightBusy)Dispatcher.BeginInvoke(new Action(ProcessLighting));}
    }
    private void LoadHandsRig()
    {
        var o=_character!.Hands;var rig=_handsDraft??RuntimeRig;
        bool ready=rig.LayeredArms&&rig.Layers.Count==1||rig.Layers.Count==5||rig.WholeHands&&rig.Layers.Count==3;
        _customArmEffect=null;
        if(rig.LayeredArms&&rig.MotionMap is {Enabled:true} map){
            if(!_customArmEffects.TryGetValue(map,out _customArmEffect)){
                _customArmEffect=new();_customArmEffect.Configure(map,512,Math.Clamp((int)(512*rig.CanvasHeight/Math.Max(1,rig.CanvasWidth)),1,1536),RuntimeDesk);
                _customArmEffects[map]=_customArmEffect;
            }
        }
        SetArmMotion(new(),new(),new(),RuntimeDesk);
        _layeredFingerMotion.Reset();
        _handsImages[0].Effect=rig.LayeredArms?(_customArmEffect as System.Windows.Media.Effects.Effect??_layeredArmsEffect):null;
        foreach(var t in _handsOffsets)t.X=t.Y=0;
        foreach(var r in _handsRotations)r.Angle=0;
        foreach(var b in _handsBends)b.AngleX=0;
        _handWarps[0]=_handWarps[1]=null;
        for(int i=0;i<5;i++){
            string? path=rig.Layers.ElementAtOrDefault(i);
            {_handsImages[i].Source=_handsShownSources[i]=HandLitImage(rig,i);_handsImages[i].Tag=path;}
            if(i<rig.Layers.Count&&_handsImages[i].Source==null)ready=false;
        }
        if(ready&&rig.WholeHands){
            for(int side=0;side<2;side++){
                int index=rig.Layers.Count==3?side+1:side+3;
                if(_handsImages[index].Source is System.Windows.Media.Imaging.BitmapSource bitmap){
                    var warp=new HandImageWarp(bitmap,side==0?rig.Finger1X:rig.Finger2X,side==0?rig.Finger1Y:rig.Finger2Y,rig.FingerRadius);
                    _handWarps[side]=warp;_handsImages[index].Source=warp.Image;
                }
            }
        }
        _handsReady=ready;
        _handsHost.Visibility=ready&&(o.Enabled||_handsTest>0||IsHandsEditing)?Visibility.Visible:Visibility.Collapsed;
        if(!ready)SetHandsStatus("Загрузите PNG слоёв или готовый набор");else if(!o.Enabled&&_handsTest<=0)SetHandsStatus("Реакция выключена");
        if(!o.Enabled&&_handsTest<=0&&!IsHandsEditing){_handsFade=1;_handsPrevious.Opacity=0;}
        _handsHost.Opacity=1;LayoutHands();
    }
    private void LayoutHands()
    {
        if(_character==null)return;var r=_handsDraft??RuntimeRig;
        double w=Artwork.Width,h=w*Math.Clamp(r.CanvasHeight/Math.Max(1,r.CanvasWidth),.1,10);
        _handsHost.Width=w;_handsHost.Height=h;
        foreach(var im in _handsImages){im.Width=w;im.Height=h;}
        _handsHost.RenderTransform=new TransformGroup{Children=new TransformCollection{new ScaleTransform(Math.Clamp(r.Scale,.1,3),Math.Clamp(r.Scale,.1,3),w*.5,h*.85),new RotateTransform(Math.Clamp(r.Rotation,-90,90),w*.5,h*.85),new TranslateTransform(r.X*w,r.Y*w)}};
        double unit=w/Math.Max(1,r.CanvasWidth);
        for(int i=0;i<5;i++){_handsRotations[i].CenterX=(i==1||i==3?r.Elbow1X:r.Elbow2X)*unit;_handsRotations[i].CenterY=r.ElbowY*unit;_handsRotations[i].Angle=0;_handsBends[i].CenterY=r.ElbowY*unit;_handsBends[i].AngleX=0;}
        if(_handsGrip!=null){_handsGrip.Width=w*.78;_handsGrip.Height=h*.3;Canvas.SetLeft(_handsGrip,w*.11);Canvas.SetTop(_handsGrip,h*.69);}
    }
    private void RenderHands(double dt)
    {
        if(_character==null||IsHandsEditing)return;
        var o=_character.Hands;if(!o.Enabled&&_handsTest<=0)return;
        if(_handsTest>0&&_handsTest<=dt){_layeredFingerMotion.Reset();SetArmMotion(new(),new(),new(),RuntimeDesk);}
        HandsInput input;
        if(_handsTest>0){_handsTest=Math.Max(0,_handsTest-dt);double t=6-_handsTest;input=new(Math.Sin(t*3),Math.Cos(t*2),Math.Cos(t*3),Math.Sin(t*2),Math.Sin(t*8)>.3?1:0,Math.Cos(t*9)>.3?1:0,true);SetHandsStatus("Тест без устройства");if(_handsTest==0){_handsMotion.Reset();foreach(var tr in _handsOffsets){tr.X=tr.Y=0;}foreach(var r in _handsRotations)r.Angle=0;foreach(var bend in _handsBends)bend.AngleX=0;foreach(var warp in _handWarps)warp?.Update(0,0);_handsHost.Visibility=o.Enabled?Visibility.Visible:Visibility.Collapsed;SetHandsStatus(o.Enabled?"Тест завершён":"Реакция выключена");return;}}
        else{
            var pad=_handsReader.Poll(dt,false);var desk=o.AutoSwitch||o.DeskMode?_deskReader.Poll(dt,true,o.MouseFollowsPosition):default;
            if(o.AutoSwitch&&!HandsSettingsOpen&&_handsAuto.Step(dt,pad,desk,RigReady(o.Gamepad),RigReady(o.Desk))){
                SwitchHandsPose();
            }
            input=RuntimeDesk?desk:pad;SetHandsStatus(RuntimeDesk?"WASD / пробел · движение мыши и клики":input.Connected?"Геймпад подключён · XInput":"Геймпад XInput не найден");}
        if(!_handsReady)return;
        if(_handsFade<1){_handsFade=Math.Min(1,_handsFade+dt/Math.Clamp(o.TransitionSeconds,.1,1));double fade=_handsFade*_handsFade*(3-2*_handsFade);_handsHost.Opacity=fade;_handsPrevious.Opacity=1-fade;}
        _handsMotion.Step(dt,input);double amount=Math.Clamp(RuntimeRig.Amount,0,2)*Artwork.Width/900;
        if(RuntimeRig.LayeredArms){
            _armTime+=dt;double s=Math.Clamp(RuntimeRig.Amount,0,2),idle=Math.Sin(_armTime*1.4)*2*s;
            var m=_handsMotion;
            var right=RuntimeDesk?new Point(m.RX*9*s,(m.RY*5+m.RightPress*2)*s+idle):new Point((m.LX+m.RX)*2*s,idle);
            var left=RuntimeDesk?new Point(0,m.LeftPress*3*s+idle):right;
            if(_handsTest>0)input=input with{LT=input.LeftPress,RT=input.RightPress};
            _layeredFingerMotion.Step(dt,input,RuntimeDesk);
            Point Scale(Point p)=>new(p.X*s,p.Y*s);
            SetArmMotion(right,left,Scale(_layeredFingerMotion.Press),RuntimeDesk,Scale(_layeredFingerMotion.RightThumb),Scale(_layeredFingerMotion.LeftThumb));
            return;
        }
        if(RuntimeRig.WholeHands){
            double strength=Math.Clamp(RuntimeRig.Amount,0,2);
            foreach(var t in _handsOffsets){t.X=t.Y=0;}foreach(var r in _handsRotations)r.Angle=0;foreach(var bend in _handsBends)bend.AngleX=0;
            if(RuntimeDesk){
                _handWarps[0]?.Update(0,_handsMotion.LeftPress*3*strength);
                _handWarps[1]?.Update(0,_handsMotion.RightPress*2*strength);
                double angle=(_handsMotion.RX*.8+_handsMotion.RY*.25)*strength;
                _handsBends[2].AngleX=_handsBends[4].AngleX=angle;
            }else{
                _handWarps[0]?.Update(_handsMotion.LX*.7*strength,(_handsMotion.LY*.4+_handsMotion.LeftPress*.65)*strength);
                _handWarps[1]?.Update(_handsMotion.RX*.7*strength,(_handsMotion.RY*.4+_handsMotion.RightPress*.65)*strength);
            }
            return;
        }
        if(RuntimeRig.ElbowRig){
            foreach(var t in _handsOffsets){t.X=t.Y=0;}
            foreach(var rotation in _handsRotations)rotation.Angle=0;
            if(RuntimeDesk){
                double strength=Math.Clamp(RuntimeRig.Amount,0,2);
                double mouseAngle=(_handsMotion.RX*.85+_handsMotion.RY*.3)*strength;
                _handsRotations[2].Angle=_handsRotations[4].Angle=mouseAngle;
                _handsOffsets[3].Y=_handsMotion.LeftPress*2.2*amount;
                _handsOffsets[4].Y=_handsMotion.RightPress*1.4*amount;
            }else{
                _handsOffsets[3].X=_handsMotion.LX*1.2*amount;_handsOffsets[3].Y=(_handsMotion.LY*.8+_handsMotion.LeftPress*1.4)*amount;
                _handsOffsets[4].X=_handsMotion.RX*1.2*amount;_handsOffsets[4].Y=(_handsMotion.RY*.8+_handsMotion.RightPress*1.4)*amount;
            }
            return;
        }
        _handsOffsets[0].X=(_handsMotion.LX+_handsMotion.RX)*.4*amount;_handsOffsets[0].Y=(_handsMotion.LeftPress+_handsMotion.RightPress)*.45*amount;
        for(int side=0;side<2;side++){
            double x=side==0?_handsMotion.LX:_handsMotion.RX,y=side==0?_handsMotion.LY:_handsMotion.RY,press=side==0?_handsMotion.LeftPress:_handsMotion.RightPress;
            // Finger translations overlap their neutral cut; large movements would reveal the source seam.
            _handsOffsets[side+1].X=x*.7*amount;_handsOffsets[side+1].Y=press*.6*amount;
            _handsOffsets[side+3].X=x*2*amount;_handsOffsets[side+3].Y=(y*1.2+press*1.8)*amount;
        }
    }
    private void SwitchHandsPose()
    {
                if(RuntimeRig.LayeredArms){_handsMotion.Reset();LoadHandsRig();_handsFade=1;_handsPrevious.Opacity=0;_handsHost.Opacity=1;return;}
                _handsPrevious.Width=_handsHost.Width;_handsPrevious.Height=_handsHost.Height;_handsPrevious.RenderTransform=_handsHost.RenderTransform.Clone();
                for(int i=0;i<5;i++){var im=_handsPreviousImages[i];im.Source=_handsImages[i].Source is System.Windows.Media.Imaging.WriteableBitmap mutable?mutable.Clone():_handsImages[i].Source;im.Width=_handsImages[i].Width;im.Height=_handsImages[i].Height;im.RenderTransform=_handsImages[i].RenderTransform.Clone();}
                _handsMotion.Reset();LoadHandsRig();_handsFade=0;_handsPrevious.Opacity=1;
        _handsHost.Opacity=0;
    }
    public void TestHands(){if(IsHandsEditing||!_handsReady)return;_handsTest=6;RefreshHands();}
    public void StopHandsTest(){_handsTest=0;_handsMotion.Reset();foreach(var tr in _handsOffsets){tr.X=tr.Y=0;}foreach(var r in _handsRotations)r.Angle=0;foreach(var bend in _handsBends)bend.AngleX=0;RefreshHands();}
    public bool BeginHandsPlacement(Action save)
    {
        if(_character==null||!RigReady(_character.Hands.Current))return false;
        EndHandsPlacement(false);EndAccessoryPlacement(false);EndVoiceLightsPlacement(false);EndRobotPlacement(false);EndPlacement();EndMouthCurveEditing(false);EndLightEditing();EditEyeRegion(null);EndScreenRegionEditing(false);
        _handsAuto.Reset(_character.Hands.DeskMode);_handsPrevious.Opacity=0;_handsFade=1;
        _handsDraft=_character.Hands.Current.Copy();_handsSave=save;_handsWasPaused=_editingPaused;SetEditingPaused(true);_handsTest=0;
        foreach(var tr in _handsOffsets){tr.X=tr.Y=0;}foreach(var r in _handsRotations)r.Angle=0;foreach(var bend in _handsBends)bend.AngleX=0;
        _handsHost.IsHitTestVisible=true;_handsGrip=new Thumb{Focusable=true,Cursor=Cursors.SizeAll,Background=new SolidColorBrush(Color.FromArgb(25,0,255,255))};
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,_handsGrip.Background);border.SetValue(Border.BorderBrushProperty,Brushes.Turquoise);border.SetValue(Border.BorderThicknessProperty,new Thickness(2));_handsGrip.Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};
        _handsGrip.PreviewMouseLeftButtonDown+=(_,_)=>_handsGrip.Focus();
        _handsGrip.DragDelta+=(_,e)=>NudgeHands(e.HorizontalChange,e.VerticalChange);
        _handsGrip.PreviewKeyDown+=(_,e)=>{double step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?10:1;switch(e.Key){case Key.Left:NudgeHands(-step,0);break;case Key.Right:NudgeHands(step,0);break;case Key.Up:NudgeHands(0,-step);break;case Key.Down:NudgeHands(0,step);break;default:return;}e.Handled=true;};
        _handsHost.Children.Add(_handsGrip);RefreshHands();return true;
    }
    public void NudgeHands(double x,double y){if(_handsDraft is {} r){r.X=Math.Clamp(r.X+x/Artwork.Width,-2,2);r.Y=Math.Clamp(r.Y+y/Artwork.Width,-2,2);LayoutHands();}}
    public void ShapeHands(double scale,double rotation){if(_handsDraft is {} r){r.Scale=Math.Clamp(scale,.1,3);r.Rotation=Math.Clamp(rotation,-90,90);LayoutHands();}}
    public void EndHandsPlacement(bool apply)
    {
        if(_handsDraft==null)return;var draft=_handsDraft;var save=_handsSave;
        if(_handsGrip!=null){_handsGrip.ReleaseMouseCapture();_handsHost.Children.Remove(_handsGrip);}_handsGrip=null;_handsHost.IsHitTestVisible=false;_handsDraft=null;_handsSave=null;
        if(apply&&_character is {} c){if(c.Hands.DeskMode)c.Hands.Desk=draft;else c.Hands.Gamepad=draft;}
        SetEditingPaused(_handsWasPaused);RefreshHands();if(apply)save?.Invoke();
    }
}
