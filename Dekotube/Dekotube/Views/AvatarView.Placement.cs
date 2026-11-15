using System.Windows;
using System.Windows.Input;

namespace Dekotube.Views;

public partial class AvatarView
{
    private bool _placing, _placingHead, _pauseBeforePlacement;
    private bool _placingMouth;
    private bool _placingBody;
    private readonly System.Windows.Media.ScaleTransform _bodyScale = new();
    private readonly System.Windows.Media.TranslateTransform _bodyOffset = new();
    private readonly System.Windows.Controls.Border _mouthPlacementOutline = new() { BorderBrush=System.Windows.Media.Brushes.Turquoise,BorderThickness=new Thickness(2),IsHitTestVisible=false,Visibility=Visibility.Collapsed };
    private Point? _placementStart;
    private double _placementX, _placementY;
    private Action? _savePlacement;
    public event EventHandler? PlacementChanged;

    private void InitializePlacement()
    {
        var bodyTransform = new System.Windows.Media.TransformGroup();
        bodyTransform.Children.Add(_bodyScale);bodyTransform.Children.Add(_bodyOffset);
        BodyImage.RenderTransform = BodyShade.RenderTransform = bodyTransform;
        RegionEditor.RenderTransform = HeadSurface.RenderTransform;
        Artwork.Children.Remove(RegionEditor);
        LivingRig.Children.Add(RegionEditor);
        System.Windows.Controls.Panel.SetZIndex(RegionEditor,100);
        RegionEditor.SetBinding(RenderTransformOriginProperty,new System.Windows.Data.Binding("RenderTransformOrigin"){Source=HeadSurface});
        SizeChanged += (_, _) => RefreshPlacement();
        Artwork.SizeChanged += (_, _) => RefreshPlacement();
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!_placing || _character == null) return;
            _placementStart = e.GetPosition(_placingMouth ? _mouthCanvas : (_placingHead || _placingBody) ? LivingRig : this);
            _placementX = _placingMouth ? _character.MouthLayer.X : _placingHead ? _character.HeadX : _character.SceneX;
            _placementY = _placingMouth ? _character.MouthLayer.Y : _placingHead ? _character.HeadY : _character.SceneY;
            if(_placingBody){_placementX=_character.BodyX;_placementY=_character.BodyY;}
            CaptureMouse(); e.Handled = true;
        };
        PreviewMouseMove += (_, e) =>
        {
            if (_placementStart is not Point start || _character == null) return;
            var delta = e.GetPosition(_placingMouth ? _mouthCanvas : (_placingHead || _placingBody) ? LivingRig : this) - start;
            if (_placingMouth)
            {
                _character.MouthLayer.X=Math.Clamp(_placementX+delta.X/Artwork.Width,0,1);
                _character.MouthLayer.Y=Math.Clamp(_placementY+delta.Y/Artwork.Height,0,1);
                LayoutMouthLayer();
            }
            else if (_placingBody)
            {
                _character.BodyX=Math.Clamp(_placementX+delta.X/Artwork.Width,-2,2);
                _character.BodyY=Math.Clamp(_placementY+delta.Y/Artwork.Height,-2,2);
            }
            else if (_placingHead)
            {
                _character.HeadX = Math.Clamp(_placementX + delta.X / Artwork.Width, -2, 2);
                _character.HeadY = Math.Clamp(_placementY + delta.Y / Artwork.Height, -2, 2);
            }
            else
            {
                _character.SceneX = Math.Clamp(_placementX + delta.X / Math.Max(1, ActualWidth), -2, 2);
                _character.SceneY = Math.Clamp(_placementY + delta.Y / Math.Max(1, ActualHeight), -2, 2);
            }
            RefreshPlacement(); e.Handled = true;
        };
        PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (_placementStart == null) return;
            _placementStart = null; ReleaseMouseCapture(); _savePlacement?.Invoke(); e.Handled = true;
        };
        LostMouseCapture += (_, _) => { if (_placementStart != null) { _placementStart = null; _savePlacement?.Invoke(); } };
        PreviewMouseWheel += (_, e) =>
        {
            if (!_placing || _character == null) return;
            if(_placingMouth)
            {
                var m=_character.MouthLayer;double factor=Math.Pow(1.1,e.Delta/120.0);
                m.Width=Math.Clamp(m.Width*factor,.01,1);m.Height=Math.Clamp(m.Height*factor,.01,1);
                LayoutMouthLayer();PlacementChanged?.Invoke(this,EventArgs.Empty);e.Handled=true;return;
            }
            double zoom=Math.Pow(1.1,e.Delta/120.0);
            if(_placingHead)_character.HeadScale=Math.Clamp(_character.HeadScale*zoom,.1,4);
            else if(_placingBody)_character.BodyScale=Math.Clamp(_character.BodyScale*zoom,.1,4);
            else _character.SceneScale = Math.Clamp(_character.SceneScale * zoom, .1, 4);
            RefreshPlacement(); _savePlacement?.Invoke(); e.Handled = true;
        };
    }

    public void RefreshPlacement()
    {
        if (_character == null) return;
        double Valid(double value, double fallback) => double.IsFinite(value) ? value : fallback;
        PlacementScale.ScaleX = PlacementScale.ScaleY = Math.Clamp(Valid(_character.SceneScale, 1), .1, 4);
        PlacementTranslation.X = Math.Clamp(Valid(_character.SceneX, 0), -2, 2) * ActualWidth;
        PlacementTranslation.Y = Math.Clamp(Valid(_character.SceneY, 0), -2, 2) * ActualHeight;
        HeadPlacement.X = _character.Head.Enabled ? Math.Clamp(Valid(_character.HeadX, 0), -2, 2) * Artwork.Width : 0;
        HeadPlacement.Y = _character.Head.Enabled ? Math.Clamp(Valid(_character.HeadY, 0), -2, 2) * Artwork.Height : 0;
        HeadScaleTransform.ScaleX = HeadScaleTransform.ScaleY = _character.Head.Enabled ? Math.Clamp(Valid(_character.HeadScale, 1), .1, 4) : 1;
        _bodyScale.ScaleX=_bodyScale.ScaleY=Math.Clamp(Valid(_character.BodyScale,1),.1,4);
        _bodyOffset.X=Valid(_character.BodyX,0)*Artwork.Width;
        _bodyOffset.Y=Valid(_character.BodyY,0)*Artwork.Height;
        PlacementChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BeginPlacement(bool head, Action save, bool body=false)
    {
        EndMouthCurveEditing(false);
        EndPlacement(); EndLightEditing(); EditEyeRegion(null);
        if (_character == null || (head && (!_character.Head.Enabled || string.IsNullOrWhiteSpace(_character.Head.BodyImagePath)))) return;
        _pauseBeforePlacement = _editingPaused;
        _placing = true; _placingHead = head; _savePlacement = save;
        _placingBody=body;
        SetEditingPaused(true); Cursor = Cursors.SizeAll;
        PreviousStateImage.Source = null; PreviousStateLayer.Opacity = 0; CurrentStateLayer.Opacity = 1;
    }

    public void EndPlacement()
    {
        if (!_placing) return;
        _placing = false; _placingBody=false; _placementStart = null; ReleaseMouseCapture(); Cursor = null;
        _placingMouth=false;_mouthPlacementOutline.Visibility=Visibility.Collapsed;SyncPerspective();
        _savePlacement?.Invoke(); _savePlacement = null;
        SetEditingPaused(_pauseBeforePlacement);
    }
    public void BeginMouthPlacement()
    {
        BeginPlacement(false,()=>{});
        if(!_placing)return;
        _placingMouth=true;
        SyncPerspective();
        if(!_mouthSprite.Children.Contains(_mouthPlacementOutline))_mouthSprite.Children.Add(_mouthPlacementOutline);
        _mouthPlacementOutline.Visibility=Visibility.Visible;
    }
    public void RefreshMouthPlacement(){LayoutMouthLayer();PlacementChanged?.Invoke(this,EventArgs.Empty);}
}
