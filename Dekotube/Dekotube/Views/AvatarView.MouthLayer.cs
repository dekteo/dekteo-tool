using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dekotube.Services;

namespace Dekotube.Views;

public partial class AvatarView
{
    private readonly Canvas _mouthCanvas = new() { IsHitTestVisible = false };
    // Keep the 3D texture bounds fixed: otherwise WPF stretches the thin rest
    // contour to fill the entire mouth plane after loading a saved perspective.
    private readonly Grid _mouthSprite = new() { Background = Brushes.Transparent, RenderTransformOrigin = new Point(.5, .5) };
    private readonly Grid _mouthPoseHost=new(){RenderTransformOrigin=new Point(.5,.5)};
    private readonly HeadPerspectiveView _mouthProjection=new(){MaximumAngle=60};
    private readonly RotateTransform _mouthRotation=new();
    private bool _mouthProjected;
    private (double Width,double Height) _mouthProjectionSize;
    private readonly Image _layerClosed = new() { Stretch = Stretch.Fill };
    private readonly Image _layerOpen = new() { Stretch = Stretch.Fill, RenderTransformOrigin = new Point(.5, .5) };
    private readonly Image _layerShape = new() { Stretch = Stretch.Fill, RenderTransformOrigin = new Point(.5, .5) };
    private readonly ScaleTransform _mouthOpenScale = new();
    private readonly TranslateTransform _mouthAlignment = new();
    private readonly ScaleTransform _proceduralScale = new(1, .1);
    private readonly Border _proceduralMouth = new() { RenderTransformOrigin = new Point(.5, .5) };
    private readonly ProceduralMouthStrip _proceduralStrip = new();
    private double _mouthBend;
    private double[] _restMouthCurve = [0,0,.5,0,1,0], _openMouthCurve = [0,0,.5,0,1,0];
    private readonly double[] _blendedMouthCurve = [0,0,.5,0,1,0];
    private readonly MouthVowelPose[] _proceduralPoses = new MouthVowelPose[6];
    private ImageSource? _alignedOpen, _alignedClosed;
    private Rect _openInk = new(0, 0, 1, 1), _closedInk = new(.04, .44, .92, .12);
    private BitmapSource? _mouthBase, _mouthBlinkBase;
    private readonly Dictionary<VowelShape, BitmapSource?> _mouthVowels = new();
    private ImageSource? _shapeTarget;
    private double _shapeBlend;
    private double _mouthOpening;
    private int _mouthInputLevel;
    private bool LayerMouthActive => _character?.MouthLayer.Enabled == true && _mouthBase != null;
    private bool _mouthLayerPreview;
    public bool MouthLayerPreview
    {
        get => _mouthLayerPreview;
        set { if (_mouthLayerPreview == value) return; _mouthLayerPreview = value; SceneStateChanged?.Invoke(this, EventArgs.Empty); }
    }
    public bool IsMouthLayerPreviewing => _mouthLayerPreview;
    public bool IsMouthLayerActive => _character?.MouthLayer.Enabled == true;

    private void InitializeMouthLayer()
    {
        var transform = new TransformGroup();
        transform.Children.Add(_mouthOpenScale); transform.Children.Add(_mouthAlignment);
        _layerOpen.RenderTransform = transform;
        _proceduralMouth.RenderTransform = _proceduralScale;
        _proceduralMouth.Child = _proceduralStrip;
        _mouthSprite.Children.Add(_layerOpen); _mouthSprite.Children.Add(_layerShape); _mouthSprite.Children.Add(_layerClosed);
        _mouthSprite.Children.Add(_proceduralMouth);
        _mouthPoseHost.RenderTransform=_mouthRotation;
        _mouthPoseHost.Children.Add(_mouthSprite);_mouthCanvas.Children.Add(_mouthPoseHost); Composite.Children.Add(_mouthCanvas);
        InitializeMouthCurveEditor();
        Artwork.SizeChanged += (_, _) => LayoutMouthLayer();
    }
    private void RefreshMouthLayer()
    {
        var m = _character!.MouthLayer;
        _proceduralMouth.Visibility = m.Procedural ? Visibility.Visible : Visibility.Collapsed;
        _layerClosed.Visibility = _layerOpen.Visibility = _layerShape.Visibility = m.Procedural ? Visibility.Collapsed : Visibility.Visible;
        Color color;
        try { color = (Color)ColorConverter.ConvertFromString(m.Color); } catch { color = Colors.Cyan; }
        _proceduralStrip.Configure(color, Math.Clamp(m.Roundness, 0, .5));
        _proceduralStrip.SetSurface(m.UseStripPng ? LightFile(m.StripPath) : null, m.HighlightEnabled && !m.UseStripPng);
        _restMouthCurve = MouthLayerOptions.CurveOrDefault(m.RestCurve); _openMouthCurve = MouthLayerOptions.CurveOrDefault(m.OpenCurve);
        for(int i=0;i<6;i++) _proceduralPoses[i] = m.VowelPoses != null && m.VowelPoses.TryGetValue((VowelShape)i,out var pose) && pose != null ? pose.Copy() : MouthVowelPose.Default((VowelShape)i);
        _proceduralMouth.Effect = m.GlowEnabled ? new System.Windows.Media.Effects.DropShadowEffect { Color = color, ShadowDepth = 0, BlurRadius = Math.Clamp(m.Glow, 0, 40), Opacity = m.Glow > 0 ? .85 : 0 } : null;
        UpdateLayer(_layerClosed, m.ClosedPath); UpdateLayer(_layerOpen, m.OpenPath);
        UpdateMouthAlignment();
        // Reuse frozen image cache; no file reads during animation.
        _mouthBase = LightFile(m.BasePath); _mouthBlinkBase = LightFile(m.BlinkBasePath);
        _mouthVowels.Clear();
        foreach (var item in m.VowelPaths) if (Enum.IsDefined(item.Key) && !string.IsNullOrWhiteSpace(item.Value)) _mouthVowels[item.Key] = LightFile(item.Value);
        _shapeTarget = null; _shapeBlend = 0; _layerShape.Source = null;
        _mouthCanvas.Visibility = LayerMouthActive ? Visibility.Visible : Visibility.Collapsed;
        LayoutMouthLayer();
    }
    private void LayoutMouthLayer()
    {
        if (_character == null) return;
        var m = _character.MouthLayer;
        _mouthSprite.Width = Math.Clamp(m.Width, .005, 1) * Artwork.Width;
        _mouthSprite.Height = Math.Clamp(m.Height, .005, 1) * Artwork.Height;
        _proceduralMouth.CornerRadius = new CornerRadius(Math.Min(_mouthSprite.Width, _mouthSprite.Height) * Math.Clamp(m.Roundness, 0, .5));
        _mouthPoseHost.Width=_mouthSprite.Width;_mouthPoseHost.Height=_mouthSprite.Height;
        Canvas.SetLeft(_mouthPoseHost, Math.Clamp(m.X, 0, 1) * Artwork.Width - _mouthSprite.Width / 2);
        Canvas.SetTop(_mouthPoseHost, Math.Clamp(m.Y, 0, 1) * Artwork.Height - _mouthSprite.Height / 2);
        _mouthRotation.Angle=IsMouthCurveEditing?0:Math.Clamp(m.Rotation,-90,90);
        bool projected=!IsMouthCurveEditing&&(Math.Abs(m.PerspectiveYaw)>.001||Math.Abs(m.PerspectivePitch)>.001);
        if(projected!=_mouthProjected){
            if(projected){_mouthPoseHost.Children.Remove(_mouthSprite);_mouthProjection.Attach(_mouthSprite);_mouthPoseHost.Children.Add(_mouthProjection);}
            else{_mouthProjection.Detach();_mouthPoseHost.Children.Remove(_mouthProjection);_mouthPoseHost.Children.Add(_mouthSprite);}
            _mouthProjected=projected;
        }
        var size=(_mouthSprite.Width,_mouthSprite.Height);
        if(projected&&_mouthProjectionSize!=size){_mouthProjection.Configure(size.Width,size.Height,.5,.5);_mouthProjectionSize=size;}
        _mouthProjection.Yaw=m.PerspectiveYaw;_mouthProjection.Pitch=m.PerspectivePitch;
    }
    private void RenderMouthLayer(double dt, double time)
    {
        if (!LayerMouthActive) return;
        var m = _character!.MouthLayer;
        double threshold = (Application.Current as App)?.MicrophoneService.Threshold ?? 20;
        double target = _mouthEnabled && AvatarService.Instance.IsTalking
            ? Math.Clamp((_mouthInputLevel - threshold) / Math.Max(1, m.FullVolume - threshold), 0, 1) : 0;
        if (MouthLayerPreview) target = Math.Max(0, Math.Sin(time * 5)) * .9;
        if (IsMouthCurveEditing) target = _mouthEditTesting ? Math.Max(0, Math.Sin(time * 3)) : _mouthEditOpen ? 1 : 0;
        _mouthOpening = MouthLayerOptions.Smooth(_mouthOpening, target, dt);
        if (IsMouthCurveEditing && !_mouthEditTesting) _mouthOpening = target;
        var vowel = _mouthEnabled && AvatarService.Instance.IsTalking ? (Application.Current as App)?.MicrophoneService.CurrentVowel ?? VowelShape.Unknown : VowelShape.Unknown;
        if (m.Procedural)
        {
            if (MouthLayerPreview) vowel = (VowelShape)(1+(int)(time/2)%5);
            if (IsMouthCurveEditing) vowel = _mouthEditVowel;
            else if (!m.ProceduralVowels) vowel = VowelShape.Unknown;
            var pose = IsMouthCurveEditing && vowel != VowelShape.Unknown ? _mouthPoseDrafts![(int)vowel] : _proceduralPoses[(int)vowel];
            var neutral = MouthLayerOptions.ProceduralShape(_mouthOpening, VowelShape.Unknown, m.RestThickness, m.MaxOpening);
            double influence = vowel == VowelShape.Unknown ? 0 : IsMouthCurveEditing ? 1 : Math.Clamp(m.VowelInfluence, 0, 1), life = IsMouthCurveEditing ? 0 : Math.Clamp(m.Liveliness, 0, 1);
            double micro=IsMouthCurveEditing || MouthLayerPreview ? 0 : _idleExpression.Mouth;
            double width = 1-_idleExpression.Narrowing*.22 + (pose.Width-1)*_mouthOpening*influence + (m.StretchEnabled ? .10*life*_mouthOpening : 0);
            double poseHeight = Math.Clamp(m.RestThickness,.02,.4)+(Math.Max(Math.Clamp(m.RestThickness,.02,.4),pose.Opening ?? Math.Clamp(m.MaxOpening,.02,1)*pose.Height)-Math.Clamp(m.RestThickness,.02,.4))*_mouthOpening;
            double height = neutral.Height + (poseHeight-neutral.Height)*influence;
            // The speech envelope drives motion, not an independent idle oscillation.
            double motionBlend = 1 - Math.Exp(-Math.Clamp(dt, 0, .1)/.075);
            if (IsMouthCurveEditing && !_mouthEditTesting) motionBlend = 1;
            _proceduralScale.ScaleX += (width-_proceduralScale.ScaleX)*motionBlend;
            _proceduralScale.ScaleY += (height-_proceduralScale.ScaleY)*motionBlend;
            _mouthBend += ((m.BendEnabled ? life*_mouthOpening*.22 : 0)-_mouthBend)*motionBlend;
            var rest = _mouthRestDraft ?? _restMouthCurve; var opened = _mouthOpenDraft ?? _openMouthCurve;
            for (int i=0;i<6;i++)
            {
                double openPoint = opened[i]+(pose.Curve[i]-opened[i])*influence;
                double point = m.BendEnabled || IsMouthCurveEditing ? rest[i]+(openPoint-rest[i])*_mouthOpening : (i%2==0 ? i/4.0 : 0);
                if(i==3)point+=micro*.85;
                if(i==1)point-=Math.Max(0,_idleExpression.Asymmetry)*.9;
                if(i==5)point-=Math.Max(0,-_idleExpression.Asymmetry)*.9;
                _blendedMouthCurve[i] += (point-_blendedMouthCurve[i])*motionBlend;
            }
            _proceduralStrip.SetCurve(_blendedMouthCurve);
            _proceduralStrip.Bend(_mouthBend * _mouthSprite.Height);
            if (IsMouthCurveEditing) UpdateMouthCurveHandles();
            return;
        }
        if(vowel==VowelShape.E && !_mouthVowels.ContainsKey(vowel)) vowel=VowelShape.IE;
        var wantedShape = _mouthVowels.TryGetValue(vowel, out var vowelImage) ? vowelImage : null;
        var currentShape = _shapeTarget;
        if (!ReferenceEquals(wantedShape, currentShape)) { _shapeTarget = wantedShape; _shapeBlend = 0; _layerShape.Source = wantedShape; UpdateMouthAlignment(); }
        _shapeBlend = MouthLayerOptions.Smooth(_shapeBlend, _shapeTarget == null ? 0 : 1, dt);
        // Match the visible closed contour before the textures exchange opacity.
        // Bounds are measured on image changes only, never on animation frames.
        double transition = Math.Clamp(_mouthOpening / .35, 0, 1);
        double blend = transition * transition * (3 - 2 * transition);
        _layerOpen.Opacity = _layerOpen.Source == null ? 0 : blend * (1 - _shapeBlend);
        _layerShape.Opacity = _layerShape.Source == null ? 0 : blend * _shapeBlend;
        _layerClosed.Opacity = _layerOpen.Source == null && _layerShape.Source == null ? 1 : 1 - blend;
        if (_layerClosed.Source == null) _layerOpen.Opacity = 1;
        double sx = _openInk.Width > 0 ? Math.Clamp(_closedInk.Width / _openInk.Width, .02, 4) : .92;
        double sy = _openInk.Height > 0 ? Math.Clamp(_closedInk.Height / _openInk.Height, .02, 4) : .12;
        _mouthOpenScale.ScaleY = sy + (1 - sy) * _mouthOpening;
        _mouthOpenScale.ScaleX = sx + (1 - sx) * _mouthOpening;
        double ox = _openInk.X + _openInk.Width / 2, oy = _openInk.Y + _openInk.Height / 2;
        double cx = _closedInk.X + _closedInk.Width / 2, cy = _closedInk.Y + _closedInk.Height / 2;
        double centerX = cx + (ox - cx) * _mouthOpening;
        double centerY = cy + (oy - cy) * _mouthOpening;
        _mouthAlignment.X = (centerX - (.5 + (ox - .5) * _mouthOpenScale.ScaleX)) * _mouthSprite.Width;
        _mouthAlignment.Y = (centerY - (.5 + (oy - .5) * _mouthOpenScale.ScaleY)) * _mouthSprite.Height;
    }

    private void UpdateMouthAlignment()
    {
        if (ReferenceEquals(_alignedOpen, _layerOpen.Source) && ReferenceEquals(_alignedClosed, _layerClosed.Source)) return;
        _alignedOpen = _layerOpen.Source; _alignedClosed = _layerClosed.Source;
        _openInk = MouthInkBounds(_layerOpen.Source as BitmapSource);
        _closedInk = MouthInkBounds(_layerClosed.Source as BitmapSource);
        if (_openInk.IsEmpty) _openInk = new Rect(0, 0, 1, 1);
        if (_closedInk.IsEmpty) _closedInk = new Rect(.04, .44, .92, .12);
    }

    internal static Rect MouthInkBounds(BitmapSource? source)
    {
        if (source == null) return Rect.Empty;
        var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bitmap.PixelWidth, h = bitmap.PixelHeight;
        var row = new byte[w * 4];
        int left = w, right = -1, top = h, bottom = -1;
        for (int y = 0; y < h; y++)
        {
            bitmap.CopyPixels(new Int32Rect(0, y, w, 1), row, row.Length, 0);
            for (int x = 0; x < w; x++) if (row[x * 4 + 3] >= 32)
            { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = y; }
        }
        return right < left ? Rect.Empty : new Rect((double)left / w, (double)top / h, (double)(right-left+1)/w, (double)(bottom-top+1)/h);
    }
}
