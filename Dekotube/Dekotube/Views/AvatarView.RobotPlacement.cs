using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Dekotube.Services;
namespace Dekotube.Views;
public partial class AvatarView
{
    private RobotScreenOptions? _robotDraft;
    private Canvas? _robotHandles;
    private bool _robotWasPaused;
    private Action? _robotSave;
    public bool IsRobotEditing=>_robotDraft!=null;
    public void BeginRobotPlacement(string key,Action save)
    {
        EndRobotPlacement(false);EndAccessoryPlacement(false);EndPlacement();EndMouthCurveEditing(false);EndLightEditing();EditEyeRegion(null);EndScreenRegionEditing(false);
        _robotDraft=_character!.RobotScreen.Copy();_robotSave=save;_robotWasPaused=_editingPaused;SetEditingPaused(true);SyncPerspective();
        var o=_robotDraft.Placement(key);var s=_character.ScreenEffect;
        double x=s.X*Artwork.Width,y=s.Y*Artwork.Height,w=s.Width*Artwork.Width,h=s.Height*Artwork.Height;
        double bw=key=="processing"?.15:key=="bars"?.12:1,bh=key=="processing"?.08:key=="bars"?.18:.5;
        var handles=_robotHandles=new Canvas{IsHitTestVisible=true,RenderTransform=new SkewTransform(s.SkewX,s.SkewY,x+w/2,y+h/2)};Composite.Children.Add(handles);Panel.SetZIndex(handles,40);
        Thumb Handle(Brush fill){var t=new Thumb{Background=fill,Cursor=System.Windows.Input.Cursors.SizeAll};var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BorderBrushProperty,Brushes.Cyan);border.SetValue(Border.BorderThicknessProperty,new Thickness(2));border.SetValue(Border.BackgroundProperty,fill);t.Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};handles.Children.Add(t);return t;}
        var move=Handle(new SolidColorBrush(Color.FromArgb(50,0,240,255)));move.ToolTip="Потяните область, чтобы переместить эффект";
        var size=Handle(Brushes.White);size.Width=size.Height=20;size.ToolTip="Размер эффекта";
        void Refresh()
        {
            double width=w*bw*o.Size,height=h*bh*o.Size;
            move.Width=width;move.Height=height;Canvas.SetLeft(move,x+w*o.X-width/2);Canvas.SetTop(move,y+h*o.Y-height/2);
            Canvas.SetLeft(size,x+w*o.X+width/2-10);Canvas.SetTop(size,y+h*o.Y+height/2-10);
            _robotVisual.Configure(Artwork.Width,Artwork.Height,s,StateImage.Source,_robotDraft,_faceBrush);
            var preview=new RobotScreenMotion();preview.Test(key);for(int i=0;i<5;i++)preview.Step(.1,1,_robotDraft,false,.5);_robotVisual.Animate(preview,1);
        }
        move.DragDelta+=(_,e)=>{o.X=Math.Clamp(o.X+e.HorizontalChange/w,0,1);o.Y=Math.Clamp(o.Y+e.VerticalChange/h,0,1);Refresh();};
        size.DragDelta+=(_,e)=>{o.Size=Math.Clamp(o.Size+e.HorizontalChange/(w*bw),.25,3);Refresh();};Refresh();
    }
    public void EndRobotPlacement(bool apply)
    {
        if(_robotDraft==null)return;
        if(apply){_character!.RobotScreen.DotsPlacement=_robotDraft.DotsPlacement.Copy();_character.RobotScreen.BarsPlacement=_robotDraft.BarsPlacement.Copy();_character.RobotScreen.GlitchPlacement=_robotDraft.GlitchPlacement.Copy();}
        if(_robotHandles!=null){foreach(var t in _robotHandles.Children.OfType<Thumb>())t.ReleaseMouseCapture();Composite.Children.Remove(_robotHandles);}
        var save=_robotSave;_robotDraft=null;_robotHandles=null;_robotSave=null;_robotMotion.Reset();SyncPerspective();SetEditingPaused(_robotWasPaused);RenderRobotScreen(0,0);if(apply)save?.Invoke();
    }
}
