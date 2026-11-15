using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dekotube.Views;

public partial class AvatarView
{
    private bool _screenEditing;
    private Rect _screenDraft;
    private Dekotube.Services.ScreenEffectOptions? _screenOriginal;
    public bool IsScreenEditing => _screenEditing;
    public event EventHandler? ScreenEditingEnded;
    private Action? _screenSave;
    private bool _screenPauseBefore;

    public void BeginScreenRegionEditing(Action save)
    {
        EndMouthCurveEditing(false);
        if (_character == null) return;
        if (_screenEditing) return;
        EndPlacement(); EndLightEditing(); EditEyeRegion(null);
        var s = _character.ScreenEffect;
        _screenDraft = new Rect(Math.Clamp(s.X, 0, .95), Math.Clamp(s.Y, 0, .95), Math.Clamp(s.Width, .02, 1), Math.Clamp(s.Height, .02, 1));
        _screenOriginal = s.Copy();
        _screenEditing = true; _screenSave = save; _screenPauseBefore = _editingPaused;
        SetEditingPaused(true); RegionEditor.Visibility = Visibility.Visible; ScreenEditorActions.Visibility = Visibility.Visible; RegionEditor.Cursor = Cursors.SizeAll; ScreenRegionOutline.Visibility = Visibility.Visible;
        RegionEditor.RenderTransform = HeadSurface.RenderTransform;
        RegionEditor.SetBinding(RenderTransformOriginProperty,new System.Windows.Data.Binding("RenderTransformOrigin"){Source=HeadSurface});
        LeftRegionOutline.Visibility = RightRegionOutline.Visibility = Visibility.Collapsed;
        foreach (var grip in _regionGrips) { grip.Visibility = Visibility.Visible; grip.Fill = System.Windows.Media.Brushes.White; grip.Stroke = System.Windows.Media.Brushes.Black; grip.StrokeThickness = 2; }
        UpdateScreenEditor();
    }

    public void RefreshScreenEditor()
    {
        if (_character == null) return;
        var s = _character.ScreenEffect;
        if (_screenEditing)
        {
            _screenDraft = new Rect(s.X, s.Y, Math.Max(.01,s.Width), Math.Max(.01,s.Height));
            UpdateScreenEditor();
        }
        RenderScreenEffect(_clock.Elapsed.TotalSeconds);
    }

    public void EndScreenRegionEditing(bool apply = true)
    {
        if (!_screenEditing) return;
        if (apply) ApplyScreenEditor();
        else if (_character != null && _screenOriginal != null) _character.ScreenEffect = _screenOriginal.Copy();
        _screenEditing = false; _drawStart = null; RegionEditor.ReleaseMouseCapture(); RegionEditor.RenderTransform = HeadSurface.RenderTransform; ScreenRegionOutline.Visibility = Visibility.Collapsed; RegionEditor.Visibility = Visibility.Collapsed;
        LeftRegionOutline.Visibility = RightRegionOutline.Visibility = Visibility.Visible;
        ScreenEditorActions.Visibility = Visibility.Collapsed;
        _screenSave?.Invoke(); _screenSave = null; _screenOriginal = null;
        SetEditingPaused(_screenPauseBefore);
        RefreshScreenEditor(); ScreenEditingEnded?.Invoke(this, EventArgs.Empty);
    }

    private void ScreenEditorCancel_Click(object sender, RoutedEventArgs e) => EndScreenRegionEditing(false);
    private void ScreenEditorApply_Click(object sender, RoutedEventArgs e) => EndScreenRegionEditing(true);

    private void UpdateScreenEditor()
    {
        if (!_screenEditing) return;
        var r = new Rect(Math.Clamp(_screenDraft.X, 0, 1), Math.Clamp(_screenDraft.Y, 0, 1), Math.Clamp(_screenDraft.Width, .01, 1), Math.Clamp(_screenDraft.Height, .01, 1));
        ScreenRegionOutline.Width = r.Width * Artwork.Width; ScreenRegionOutline.Height = r.Height * Artwork.Height;
        Canvas.SetLeft(ScreenRegionOutline, r.X * Artwork.Width); Canvas.SetTop(ScreenRegionOutline, r.Y * Artwork.Height);
        var options = _character?.ScreenEffect;
        if (options != null) ScreenRegionOutline.Data = Dekotube.Services.ScreenZoneGeometry.Create(new Rect(0,0,ScreenRegionOutline.Width,ScreenRegionOutline.Height), options);
        ScreenRegionOutline.RenderTransformOrigin = new Point(0, 0);
        ScreenRegionOutline.RenderTransform = new SkewTransform(Math.Clamp(options?.SkewX ?? 0, -12, 12), Math.Clamp(options?.SkewY ?? 0, -12, 12), ScreenRegionOutline.Width / 2, ScreenRegionOutline.Height / 2);
        var points = GripPoints(new Rect(r.X * Artwork.Width, r.Y * Artwork.Height, r.Width * Artwork.Width, r.Height * Artwork.Height));
        double size = Math.Max(14, GripSize());
        for (int i=0;i<_regionGrips.Count;i++) { _regionGrips[i].Width=_regionGrips[i].Height=size; Canvas.SetLeft(_regionGrips[i],points[i].X-size/2); Canvas.SetTop(_regionGrips[i],points[i].Y-size/2); }
    }

    private void ApplyScreenEditor()
    {
        if (_character == null) return;
        var s = _character.ScreenEffect; s.X = Math.Clamp(_screenDraft.X, 0, .99); s.Y = Math.Clamp(_screenDraft.Y, 0, .99); s.Width = Math.Clamp(_screenDraft.Width, .01, 1); s.Height = Math.Clamp(_screenDraft.Height, .01, 1);
    }

    private bool ScreenHit(Point point)
    {
        var r = new Rect(_screenDraft.X * Artwork.Width, _screenDraft.Y * Artwork.Height, _screenDraft.Width * Artwork.Width, _screenDraft.Height * Artwork.Height);
        double tolerance = GripSize();
        foreach (var p in GripPoints(r)) if ((p - point).Length <= tolerance * 1.5) return true;
        return r.Contains(point);
    }

    private Point ScreenPoint(MouseEventArgs e) => e.GetPosition(RegionEditor);
}
