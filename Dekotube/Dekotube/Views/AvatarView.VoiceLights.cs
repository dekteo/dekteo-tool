using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Input;
using Dekotube.Services;
namespace Dekotube.Views;
public partial class AvatarView
{
    private readonly Canvas _voiceLightsCanvas=new(){IsHitTestVisible=false};
    private readonly Ellipse[] _voiceLights=Enumerable.Range(0,6).Select(_=>new Ellipse{Opacity=0}).ToArray();
    private readonly VoiceLightsMotion _voiceLightsMotion=new();
    private VoiceLightsOptions? _lightsDraft;
    private Canvas? _lightsHandles;
    private Action? _lightsSave;
    private bool _lightsWasPaused;
    private Window? _lightsKeyWindow;
    public int SelectedVoiceLight {get;private set;}
    public event EventHandler? VoiceLightSelectionChanged;
    public double SelectedVoiceLightSize=>_lightsDraft?.Points[SelectedVoiceLight].Size??1;
    public string SelectedVoiceLightColor=>_lightsDraft?.Points[SelectedVoiceLight].Color??_lightsDraft?.Color??"#20EFFF";
    public void SelectVoiceLight(int index){if(_lightsDraft==null)return;SelectedVoiceLight=Math.Clamp(index,0,5);RefreshVoiceLights();VoiceLightSelectionChanged?.Invoke(this,EventArgs.Empty);}
    public void ResizeVoiceLight(double value){if(_lightsDraft==null)return;_lightsDraft.Points[SelectedVoiceLight].Size=Math.Clamp(value,.25,4);RefreshVoiceLights();VoiceLightSelectionChanged?.Invoke(this,EventArgs.Empty);}
    public void ColorVoiceLight(string value){if(_lightsDraft==null)return;_lightsDraft.Points[SelectedVoiceLight].Color=value;RefreshVoiceLights();VoiceLightSelectionChanged?.Invoke(this,EventArgs.Empty);}
    public void NudgeVoiceLight(double dx,double dy){if(_lightsDraft==null)return;var p=_lightsDraft.Points[SelectedVoiceLight];p.X=Math.Clamp(p.X+dx/Artwork.Width,0,1);p.Y=Math.Clamp(p.Y+dy/Artwork.Height,0,1);RefreshVoiceLights();}
    private void VoiceLights_KeyDown(object sender,KeyEventArgs e)
    {
        if(_lightsDraft==null||e.OriginalSource is TextBox or ComboBox or Slider)return;
        double step=(Keyboard.Modifiers&ModifierKeys.Shift)!=0?10:1;
        switch(e.Key){case Key.Left:NudgeVoiceLight(-step,0);break;case Key.Right:NudgeVoiceLight(step,0);break;case Key.Up:NudgeVoiceLight(0,-step);break;case Key.Down:NudgeVoiceLight(0,step);break;default:return;}e.Handled=true;
    }
    public bool IsVoiceLightsEditing=>_lightsDraft!=null;
    private void InitializeVoiceLights(){foreach(var light in _voiceLights)_voiceLightsCanvas.Children.Add(light);_faceContent.Children.Add(_voiceLightsCanvas);Panel.SetZIndex(_voiceLightsCanvas,10);Artwork.SizeChanged+=(_,_)=>RefreshVoiceLights();}
    public void RefreshVoiceLights()
    {
        if(_character==null)return;
        var o=_lightsDraft??_character.VoiceLights;o.Normalize();Color color;try{color=(Color)ColorConverter.ConvertFromString(o.Color);}catch{color=Colors.Cyan;}
        for(int i=0;i<6;i++)
        {
            var p=o.Points[i];try{color=(Color)ColorConverter.ConvertFromString(p.Color??o.Color);}catch{color=Colors.Cyan;}double d=Math.Clamp(o.Diameter,.003,.06)*Artwork.Width*Math.Clamp(p.Size,.25,4);
            var light=_voiceLights[i];light.Width=light.Height=d;light.Fill=new SolidColorBrush(color);light.Effect=new DropShadowEffect{Color=color,ShadowDepth=0,BlurRadius=Math.Clamp(o.Glow,0,30),Opacity=.9};
            Canvas.SetLeft(light,Math.Clamp(p.X,0,1)*Artwork.Width-d/2);Canvas.SetTop(light,Math.Clamp(p.Y,0,1)*Artwork.Height-d/2);
            if(_lightsDraft!=null)light.Opacity=Math.Clamp(o.Brightness,0,1)*(i==SelectedVoiceLight?1:.15);
            if(_lightsHandles!=null){var handle=(Thumb)_lightsHandles.Children[i];handle.Opacity=i==SelectedVoiceLight?1:.3;Panel.SetZIndex(handle,i==SelectedVoiceLight?1:0);double size=Math.Max(d+10,28);handle.Width=handle.Height=size;Canvas.SetLeft(handle,p.X*Artwork.Width-size/2);Canvas.SetTop(handle,p.Y*Artwork.Height-size/2);}
        }
    }
    private void RenderVoiceLights(double dt)
    {
        if(_lightsDraft!=null)return;
        _voiceLightsMotion.Step(dt,_character!.VoiceLights,AvatarService.Instance.IsTalking,_mouthInputLevel/100.0);
        for(int i=0;i<6;i++)_voiceLights[i].Opacity=_voiceLightsMotion.Levels[i%3]*Math.Clamp(_character.VoiceLights.Brightness,0,1);
    }
    public void TestVoiceLights(){_voiceLightsMotion.Test();UpdateRenderingSubscription();}
    public void StopVoiceLightsTest(){_voiceLightsMotion.Reset();foreach(var light in _voiceLights)light.Opacity=0;UpdateRenderingSubscription();}
    public void BeginVoiceLightsPlacement(Action save)
    {
        EndVoiceLightsPlacement(false);EndRobotPlacement(false);EndAccessoryPlacement(false);EndPlacement();EndMouthCurveEditing(false);EndLightEditing();EditEyeRegion(null);EndScreenRegionEditing(false);
        SelectedVoiceLight=0;_lightsDraft=_character!.VoiceLights.Copy();_lightsDraft.Normalize();_lightsSave=save;_lightsWasPaused=_editingPaused;SetEditingPaused(true);SyncPerspective();
        _lightsKeyWindow=Window.GetWindow(this);if(_lightsKeyWindow!=null)_lightsKeyWindow.PreviewKeyDown+=VoiceLights_KeyDown;
        _lightsHandles=new Canvas();Composite.Children.Add(_lightsHandles);Panel.SetZIndex(_lightsHandles,50);
        for(int i=0;i<6;i++)
        {
            int index=i;var handle=new Thumb{Focusable=true,Cursor=System.Windows.Input.Cursors.SizeAll,ToolTip=$"{(i<3?"Л":"П")}{i%3+1}: тянуть — положение; колесо — размер точки"};
            var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(25,0,255,255)));border.SetValue(Border.BorderBrushProperty,i<3?Brushes.Gold:Brushes.Cyan);border.SetValue(Border.BorderThicknessProperty,new Thickness(2));
            var label=new FrameworkElementFactory(typeof(TextBlock));label.SetValue(TextBlock.TextProperty,$"{(i<3?"Л":"П")}{i%3+1}");label.SetValue(TextBlock.ForegroundProperty,Brushes.White);label.SetValue(TextBlock.FontSizeProperty,12.0);border.AppendChild(label);handle.Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};
            handle.PreviewMouseLeftButtonDown+=(_,_)=>{SelectVoiceLight(index);handle.Focus();};
            handle.DragDelta+=(_,e)=>{var p=_lightsDraft!.Points[index];p.X=Math.Clamp(p.X+e.HorizontalChange/Artwork.Width,0,1);p.Y=Math.Clamp(p.Y+e.VerticalChange/Artwork.Height,0,1);RefreshVoiceLights();};
            handle.PreviewMouseWheel+=(_,e)=>{SelectVoiceLight(index);ResizeVoiceLight(SelectedVoiceLightSize*(e.Delta>0?1.1:1/1.1));e.Handled=true;};_lightsHandles.Children.Add(handle);
        }
        SelectVoiceLight(0);
    }
    public void EndVoiceLightsPlacement(bool apply)
    {
        if(_lightsDraft==null)return;
        if(_lightsKeyWindow!=null)_lightsKeyWindow.PreviewKeyDown-=VoiceLights_KeyDown;_lightsKeyWindow=null;
        if(apply)_character!.VoiceLights.Points=_lightsDraft.Points.Select(p=>p.Copy()).ToList();
        if(_lightsHandles!=null){foreach(var t in _lightsHandles.Children.OfType<Thumb>())t.ReleaseMouseCapture();Composite.Children.Remove(_lightsHandles);}
        var save=_lightsSave;_lightsDraft=null;_lightsHandles=null;_lightsSave=null;_voiceLightsMotion.Reset();RefreshVoiceLights();SyncPerspective();SetEditingPaused(_lightsWasPaused);RenderVoiceLights(0);if(apply)save?.Invoke();
    }
}
