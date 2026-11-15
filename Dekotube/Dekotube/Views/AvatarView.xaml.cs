using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Dekotube.Services;

namespace Dekotube.Views;

public partial class AvatarView : UserControl
{
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    public bool IsWideMouth => _wideMouth && _character?.AdvancedMouth == true && !_character.UseVowels;
    private bool _wideMouth;
    private BitmapSource? _lastHeadDiffuse;
    public VowelShape CurrentVowel { get; private set; }
    private bool _subscribedToRendering;
    private bool _editingPaused;
    private TimeSpan _lastRenderTime;
    private bool _eyeLayoutDirty = true;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastFrame, _speechStarted = -100;
    private double _previewUntil;
    private bool _previewing;
    private CharacterEffect? _previewEffect;
    private bool _cleanScene, _mouthEnabled = true, _eyesClosed;
    private AvatarEmotion? _character;
    private BlinkService? _blink;
    private readonly BlinkService _pupilBlink = new(AvatarService.Instance);
    private bool _pupilEyesClosed;
    private string? _leftClosedPath, _rightClosedPath;
    private string? _leftMaskPath, _rightMaskPath;
    private BitmapImage? _leftMask, _rightMask;
    public BlinkService PupilBlinkService => _pupilBlink;
    private Vector _gaze;
    private double _headSpeech, _headGaze, _perspectiveYaw, _breathPhase;
    private double _mouseNod;
    private readonly HeadPerspectiveView _perspective = new();
    private bool _perspectiveAttached;
    private (double Width, double Height, double X, double Y) _projectionLayout;
    private Vector _gazeVelocity;
    private double _blinkChanged = -100, _mouthFadeStart = -100, _mouthFadeDuration;
    private bool _lastStateEyes;
    private bool MouthFading => _mouthFadeDuration > 0 && _clock.Elapsed.TotalSeconds < _mouthFadeStart + _mouthFadeDuration;
    private bool BlinkTransition => _character?.Pupils.SmoothBlink == true && _clock.Elapsed.TotalSeconds - _blinkChanged < .07;
    private readonly AutomaticGaze _automaticGaze = new();
    private string? _lastGazeMode;
    private readonly MouseIdleGaze _mouseIdle = new();
    private string? _leftPath, _rightPath;
    private int? _editingEye;
    private Point? _drawStart;
    private EyeRegion? _regionDraft;
    private Rect _dragBounds;
    private int _regionHandle = -2;
    private readonly List<Rectangle> _regionGrips = new();
    public bool IsBlinkActive => _blink?.IsBlinking == true;
    public bool IsPupilsTracking => _character?.Pupils.Enabled == true;
    public bool IsEyesClosed => !IsPupilsTracking && _eyesClosed;
    public BlinkService? BlinkService => _blink;
    public event EventHandler? SceneStateChanged;
    public event EventHandler? RegionChanged;
    public event EventHandler<bool>? RegionEditingChanged;

    public AvatarView()
    {
        InitializeComponent();
        InitializeMouthLayer();
        InitializeIdleExpression();
        InitializeDigitalBlink();
        InitializePlacement();
        InitializeAccessories();InitializeHands();
        InitializeScreenLight();
        InitializeScreenDepth();
        InitializeScreenSurface();InitializeVoiceLights();
        _pupilBlink.BlinkStateChanged += (_, closed) => { if (_pupilEyesClosed != closed) _blinkChanged = _clock.Elapsed.TotalSeconds; _pupilEyesClosed = closed; UpdateRenderingSubscription(); };
        for (int i = 0; i < 8; i++)
        {
            var grip = new Rectangle { Fill = Brushes.White, Stroke = Brushes.DarkCyan, StrokeThickness = 1, IsHitTestVisible = false };
            _regionGrips.Add(grip); RegionEditor.Children.Add(grip);
        }

        Loaded += (_, _) =>
        {
            if (_blink != null) return;
            var profile = ConfigurationService.LoadOrCreateDefault();
            _blink = new BlinkService(AvatarService.Instance)
            {
                MinBlinkIntervalMs = profile.MinBlinkIntervalMs,
                MaxBlinkIntervalMs = profile.MaxBlinkIntervalMs,
                CloseDurationMs = profile.BlinkCloseDurationMs
            };
            _blink.BlinkStateChanged += (_, closed) => { _eyesClosed = closed; if (!IsPupilsTracking) UpdateScene(); };
            if (profile.BlinkEnabled) _blink.Start();
            AvatarService.Instance.IsTalkingChanged += TalkingChanged;
            if (Application.Current is App app) { app.AudioStateService.IsTalkingChanged += AudioChanged; app.MicrophoneService.LevelUpdated += MouthLevelChanged; }
            SetMouthEnabled(_mouthEnabled);
            SyncPupilBlink();
            RefreshFromService();
            UpdateRenderingSubscription();
        };
        Unloaded += (_, _) =>
        {
            ResetHands();EndMouthCurveEditing(false);
            StopRendering(); _pupilBlink.Dispose(); _blink?.Dispose(); _blink = null;
            AvatarService.Instance.IsTalkingChanged -= TalkingChanged;
            if (Application.Current is App app) { app.AudioStateService.IsTalkingChanged -= AudioChanged; app.MicrophoneService.LevelUpdated -= MouthLevelChanged; }
        };
    }

    public void ConfigureCharacter(AvatarEmotion character)
    {
        ResetHands();
        EndVoiceLightsPlacement(false);EndRobotPlacement(false);EndAccessoryPlacement(false);_accessoryItems.Clear();_accessoryFiles.Clear();_accessoryDrive.Clear();_accessoryAnimations.Clear();
        EndMouthCurveEditing(false);
        EndScreenRegionEditing(false);
        EndPlacement();
        EndLightEditing();
        _litCache.Clear(); _lightFiles.Clear(); _lightVersion++; _lastHeadDiffuse=null;
        _pupilBlink.Stop();
        _character = character;
        _character.VoiceLights??=new();_character.VoiceLights.Normalize();_voiceLightsMotion.Reset();
        _character.RobotScreen??=new();_robotMotion.Reset();_badgeTests.Clear();
        _character.ScreenLight??=new();_character.IdleExpression??=new();ResetIdleExpression();
        _depthTest=0;_depthOffset=0;_depthEyes.X=_depthMouth.X=_depthGlass.X=0;
        _screenLightReady=false;_screenLightTimer.Stop();_screenLightVersion++;_screenLeft=_screenRight=_targetLeft=_targetRight=Colors.Black;
        _smoothLeft=_smoothRight=new();
        _poseMotion.Reset();PoseScale.ScaleX=PoseScale.ScaleY=1;PoseTranslation.Y=0;
        _mouthOpening = 0; _mouthInputLevel = 0; MouthLayerPreview = false;
        _clothingCache.Clear();
        _wideMouth = false; CurrentVowel = VowelShape.Unknown;
        _gaze = new Vector(); _automaticGaze.Reset(); _mouseIdle.Reset();
        _speechStarted = -100; _perspectiveYaw = 0; _breathPhase = 0; _headSpeech = 0; _headGaze = 0; _gazeVelocity = new();
        _mouseNod=0;_perspective.Pitch=0;
        _mouthFadeDuration = 0; StateImage.Source = null; PreviousStateImage.Source = null; _blinkChanged = -100;
        RefreshVisualSettings();
        RefreshScreenLight();
    }

    public void RefreshVisualSettings()
    {
        if (_character == null) return;
        RefreshPlacement();RefreshVoiceLights();RefreshHands();
        RefreshMouthLayer();
        RefreshAccessories();
        RefreshColorCorrection();
        bool VisiblePart(string key) => _character.HiddenParts?.Contains(key) != true;
        CurrentStateLayer.Visibility = PreviousStateLayer.Visibility = VisiblePart("base") ? Visibility.Visible : Visibility.Hidden;
        BodyImage.Visibility = BodyShade.Visibility = VisiblePart("body") ? Visibility.Visible : Visibility.Hidden;
        _mouthCanvas.Opacity = VisiblePart("mouth") ? 1 : 0;
        var p = _character.Pupils;
        UpdateLayer(LeftHalfEyeImage, p.Left.VisiblePath("half", p.Left.HalfClosedImagePath)); UpdateLayer(RightHalfEyeImage, p.Right.VisiblePath("half", p.Right.HalfClosedImagePath));
        UpdateLayer(BodyImage, _character.Head.BodyImagePath);
        ApplyBodyLighting();
        BodyImage.Visibility = !_character.Head.Enabled ? Visibility.Collapsed : VisiblePart("body") ? Visibility.Visible : Visibility.Hidden;
        BodyShade.Visibility = BodyImage.Visibility;
        HeadSurface.RenderTransformOrigin = new Point(Math.Clamp(_character.Head.PivotX, 0, 1), Math.Clamp(_character.Head.PivotY, 0, 1));
        if (_leftPath != p.Left.VisiblePath("pupil", p.Left.ImagePath)) { _leftPath = p.Left.VisiblePath("pupil", p.Left.ImagePath); LeftPupilOverlay.Source = LoadImage(_leftPath); }
        if (_rightPath != p.Right.VisiblePath("pupil", p.Right.ImagePath)) { _rightPath = p.Right.VisiblePath("pupil", p.Right.ImagePath); RightPupilOverlay.Source = LoadImage(_rightPath); }
        if (_leftClosedPath != p.Left.VisiblePath("closed", p.Left.ClosedEyeImagePath)) { _leftClosedPath = p.Left.VisiblePath("closed", p.Left.ClosedEyeImagePath); LeftClosedEyeImage.Source = LoadImage(_leftClosedPath); }
        if (_rightClosedPath != p.Right.VisiblePath("closed", p.Right.ClosedEyeImagePath)) { _rightClosedPath = p.Right.VisiblePath("closed", p.Right.ClosedEyeImagePath); RightClosedEyeImage.Source = LoadImage(_rightClosedPath); }
        UpdateLayer(LeftEyeWhite, p.Left.VisiblePath("white", p.Left.WhiteImagePath)); UpdateLayer(RightEyeWhite, p.Right.VisiblePath("white", p.Right.WhiteImagePath));
        UpdateLayer(LeftEyeForegroundImage, p.Left.VisiblePath("contour", p.Left.ForegroundImagePath)); UpdateLayer(RightEyeForegroundImage, p.Right.VisiblePath("contour", p.Right.ForegroundImagePath));
        if (_leftMaskPath != p.Left.VisiblePath("mask", p.Left.MaskImagePath)) { _leftMaskPath = p.Left.VisiblePath("mask", p.Left.MaskImagePath); _leftMask = LoadImage(_leftMaskPath); }
        if (_rightMaskPath != p.Right.VisiblePath("mask", p.Right.MaskImagePath)) { _rightMaskPath = p.Right.VisiblePath("mask", p.Right.MaskImagePath); _rightMask = LoadImage(_rightMaskPath); }
        LightImage.Source = null; LightImage.Opacity = 0;
        SyncPupilBlink();
        if (_lastHeadDiffuse != null) StateImage.Source = ApplyHeadLighting(_lastHeadDiffuse);
        _eyeLayoutDirty = true;
        UpdateScene();
        UpdateRenderingSubscription();
        SceneStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncPupilBlink()
    {
        var p = _character?.Pupils;
        if (p == null) return;
        _pupilBlink.MinBlinkIntervalMs = Math.Clamp(p.BlinkIntervalMs, 500, 30000);
        _pupilBlink.MaxBlinkIntervalMs = Math.Max(_pupilBlink.MinBlinkIntervalMs, Math.Clamp(p.MaxBlinkIntervalMs, 500, 30000));
        _pupilBlink.CloseDurationMs = Math.Clamp(p.BlinkDurationMs, 30, 1000);
        if (IsLoaded && p.Enabled && p.BlinkEnabled) _pupilBlink.Start();
        else if (_pupilBlink.IsBlinking || _pupilEyesClosed) _pupilBlink.Stop();
    }
    public void SetCleanScene(bool clean) { _cleanScene = clean; UpdatePlaceholder(); }
    public void SetMouthEnabled(bool enabled)
    {
        _mouthEnabled = enabled;
        RefreshMouthAnalysis();
        AvatarService.Instance.IsTalking = enabled && (Application.Current as App)?.AudioStateService.IsTalking == true;
        UpdateScene();
    }
    public void SetEyesEnabled(bool enabled) { if (enabled) _blink?.Start(); else _blink?.Stop(); }
    public void SetPupilsEnabled(bool enabled)
    {
        if (_character == null) return;
        _character.Pupils.Enabled = enabled; RefreshVisualSettings();
    }
    public void SetHandsActive(bool active) { } // Legacy two-state hands API; the new per-character rig uses HandsOptions.
    public void RefreshFromService()
    {
        RefreshMouthAnalysis();
        CurrentVowel = VowelShape.Unknown; UpdateScene();
    }
    private void RefreshMouthAnalysis()
    {
        if (Application.Current is not App app) return;
        var m = _character?.MouthLayer;
        app.MicrophoneService.AnalyzeVowels = _mouthEnabled && (m?.Enabled == true
            ? (m.Procedural ? m.ProceduralVowels : m.VowelPaths.Count > 0)
            : _character?.AdvancedMouth == true && _character.UseVowels && Enum.GetValues<VowelShape>().Any(_character.HasVowel));
    }
    public void PreviewEffect(CharacterEffect effect) { _previewEffect = effect; PreviewSpeech(); }
    public void CancelEffectPreview() { _previewEffect = null; _previewing = false; _previewEffect = null; UpdateScene(); }
    public void PreviewSpeech()
    {
        _previewing = true; _speechStarted = _clock.Elapsed.TotalSeconds;
        _previewUntil = _speechStarted + 2;
        UpdateScene();
    }

    private void MouthLevelChanged(object? sender, int level)
    {
        _mouthInputLevel = level;
        bool next = MouthLevelSelection.IsWide(_character?.AdvancedMouth == true && _mouthEnabled && AvatarService.Instance.IsTalking,
            level, _character?.WideMouthThreshold ?? 65, _wideMouth);
        var vowel = ((_character?.AdvancedMouth == true && _character.UseVowels) || (_character?.MouthLayer.Enabled == true && _character.MouthLayer.VowelPaths.Count > 0)) && _mouthEnabled && AvatarService.Instance.IsTalking
            ? (Application.Current as App)?.MicrophoneService.CurrentVowel ?? VowelShape.Unknown : VowelShape.Unknown;
        if (vowel == VowelShape.E) vowel = VowelShape.IE; // Legacy full-image slots share И/Э.
        if (_character?.HasVowel(vowel) != true) vowel = VowelShape.Unknown;
        if (next == _wideMouth && vowel == CurrentVowel) return;
        _wideMouth = next; CurrentVowel = vowel; UpdateScene();
    }
    private void AudioChanged(object? sender, bool talking) => AvatarService.Instance.IsTalking = _mouthEnabled && talking;
    private void TalkingChanged(object? sender, bool talking)
    {
        if (talking) _speechStarted = _clock.Elapsed.TotalSeconds;
        UpdateScene();
    }
    private void UpdateScene()
    {
        if (_editingPaused) return;
        var s = AvatarService.Instance;
        var preferred = (s.IsTalking || (_previewing && _previewEffect?.When != "idle"), IsEyesClosed) switch
        {
            (true, true) => s.MouthOpenEyesClosed,
            (true, false) => s.MouthOpenEyesOpen,
            (false, true) => s.MouthClosedEyesClosed,
            _ => s.MouthClosedEyesOpen
        };
        if (IsWideMouth && (s.IsTalking || (_previewing && _previewEffect?.When != "idle")))
            preferred = (IsEyesClosed ? s.MouthWideEyesClosed : s.MouthWideEyesOpen) ?? preferred;
        if (_character?.AdvancedMouth == true && _character.UseVowels && s.IsTalking && _character.HasVowel(CurrentVowel))
            preferred = s.VowelImages[((int)CurrentVowel - 1) * 2 + (IsEyesClosed ? 1 : 0)] ?? preferred;
        BitmapSource? nextImage = preferred ?? (s.IsTalking ? s.MouthOpenEyesOpen : s.MouthClosedEyesOpen)
            ?? s.MouthClosedEyesOpen ?? s.MouthOpenEyesOpen ?? s.MouthClosedEyesClosed ?? s.MouthOpenEyesClosed;
        if (LayerMouthActive) nextImage = IsEyesClosed ? _mouthBlinkBase ?? _mouthBase : _mouthBase;
        if (!ReferenceEquals(_lastHeadDiffuse, nextImage))
        {
            double duration = Math.Clamp(_character?.MouthTransitionMs ?? 0, 0, 120) / 1000;
            bool fade = duration > 0 && StateImage.Source != null && nextImage != null && _lastStateEyes == IsEyesClosed && _editingEye == null;
            PreviousStateImage.Source = fade ? (MouthFading && CurrentStateLayer.Opacity < .5 ? PreviousStateImage.Source : StateImage.Source) : null;
            _lastHeadDiffuse = nextImage;
            StateImage.Source = ApplyHeadLighting(_lastHeadDiffuse);
            _mouthFadeDuration = fade ? duration : 0; _mouthFadeStart = _clock.Elapsed.TotalSeconds;
        }
        if ((_character?.MouthTransitionMs ?? 0) <= 0 || _editingEye != null) _mouthFadeDuration = 0;
        _lastStateEyes = IsEyesClosed;
        // Use a stable character canvas across all four states.
        BitmapSource? reference = s.MouthClosedEyesOpen ?? s.MouthOpenEyesOpen ?? s.MouthClosedEyesClosed ?? s.MouthOpenEyesClosed;
        if (LayerMouthActive) reference = _mouthBase;
        double canvasHeight = reference is { PixelWidth: > 0 } ? 1000.0 * reference.PixelHeight / reference.PixelWidth : 1000;
        if (Artwork.Height != canvasHeight) { Artwork.Height = canvasHeight; _eyeLayoutDirty = true; }
        UpdatePlaceholder(); RenderFrame();
        UpdateRenderingSubscription();
        SceneStateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void UpdatePlaceholder() => PlaceholderBox.Visibility = StateImage.Source == null && !_cleanScene ? Visibility.Visible : Visibility.Collapsed;

    private void RenderFrame()
    {
        if (_editingPaused) return;
        double t = _clock.Elapsed.TotalSeconds, dt = Math.Clamp(t - _lastFrame, 0, .1); _lastFrame = t;
        if (_previewing && t >= _previewUntil) { _previewing = false; _previewEffect = null; UpdateScene(); }
        if (_character == null) return;
        SyncPerspective();
        var p = _character.Pupils;
        Vector target = new();
        if (_lastGazeMode != p.EffectiveGazeMode) { _lastGazeMode = p.EffectiveGazeMode; _automaticGaze.Reset(); _mouseIdle.Reset(); _gaze = new(); _gazeVelocity = new(); }
        if (p.Enabled && p.EffectiveGazeMode == "auto" && _editingEye == null) target = _automaticGaze.Target(t, p.AutoGazeStrength);
        // PointFromScreen converts physical desktop pixels into this image's local, DPI-aware coordinates.
        if (p.Enabled && (p.EffectiveGazeMode == "mouse" || p.EffectiveGazeMode == "hybrid") && _editingEye == null && IsLoaded && PresentationSource.FromVisual(Artwork) != null && GetCursorPos(out var cursor))
        {
            var local = HeadSurface.PointFromScreen(new Point(cursor.X, cursor.Y));
            var normalized = new Point(local.X / Artwork.Width, local.Y / Artwork.Height);
            if (_perspectiveAttached) normalized = _perspective.Unproject(normalized);
            target = new Vector((normalized.X - .5) * 2, (normalized.Y - .5) * 2);
            if (target.Length > 1) target.Normalize();
            if (p.EffectiveGazeMode == "hybrid" && _mouseIdle.IsIdle(new Point(cursor.X, cursor.Y), t, p.MouseIdleSeconds))
                target = _automaticGaze.Target(t, p.AutoGazeStrength);
        }
        if (p.EffectiveGazeMode == "forward") { _gaze = new Vector(); _gazeVelocity = new(); }
        double smoothing = p.EffectiveGazeMode is "auto" or "hybrid" ? Math.Max(120, p.SmoothingMs) : p.SmoothingMs;
        if (p.SoftGaze) _gaze = NaturalMotion.Smooth(_gaze, target, ref _gazeVelocity, dt, smoothing);
        else { _gazeVelocity = new(); _gaze += (target - _gaze) * GazeMath.SmoothingFactor(dt, smoothing); }
        if (_eyeLayoutDirty)
        {
            ConfigureEyeLayout(_editingEye == 0 && _regionDraft != null ? _regionDraft : p.Left, LeftEyeClip, LeftPupilSprite, LeftRegionOutline);
            ConfigureEyeLayout(_editingEye == 1 && _regionDraft != null ? _regionDraft : p.Right, RightEyeClip, RightPupilSprite, RightRegionOutline);
            ConfigureClosedEye(_editingEye == 0 && _regionDraft != null ? _regionDraft : p.Left, LeftEyeForeground);
            ConfigureClosedEye(_editingEye == 1 && _regionDraft != null ? _regionDraft : p.Right, RightEyeForeground);
            ConfigureClosedEye(p.Left, LeftHalfEye); ConfigureClosedEye(p.Right, RightHalfEye);
            ConfigureClosedEye(p.Left, LeftClosedEye);
            ConfigureClosedEye(p.Right, RightClosedEye);
            UpdateRegionGrips();
            _eyeLayoutDirty = false;
        }
        void EyePhase(EyeRegion eye, Canvas clip, Image pupil, TranslateTransform offset, Grid foreground, Image foregroundImage, Grid closed, Image closedImage, Grid half, Image halfImage)
        {
            bool phased = p.SmoothBlink && halfImage.Source != null && closedImage.Source != null;
            int phase = phased ? NaturalMotion.BlinkPhase(_pupilEyesClosed, t - _blinkChanged, p.BlinkDurationMs) : (_pupilEyesClosed ? 2 : 0);
            if (_editingEye != null) phase = 0;
            bool shown = p.Enabled || _editingEye != null;
            closed.Visibility = shown && p.BlinkStyle != "digital" && phase == 2 && closedImage.Source != null ? Visibility.Visible : Visibility.Collapsed;
            half.Visibility = shown && p.BlinkStyle != "digital" && phase == 1 ? Visibility.Visible : Visibility.Collapsed;
            foreground.Visibility = shown && p.BlinkStyle != "digital" && phase == 0 && foregroundImage.Source != null ? Visibility.Visible : Visibility.Collapsed;
            MoveEye(eye, clip, pupil, offset, shown && (p.BlinkStyle == "digital" || phase != 2));
            if (p.BlinkStyle == "digital") ApplyDigitalBlink(clip, pupil, phase, t - _blinkChanged, p.BlinkDurationMs, eye == p.Left);
            else HideDigitalBlink();
        }
        EyePhase(p.Left, LeftEyeClip, LeftPupilOverlay, LeftPupilOffset, LeftEyeForeground, LeftEyeForegroundImage, LeftClosedEye, LeftClosedEyeImage, LeftHalfEye, LeftHalfEyeImage);
        EyePhase(p.Right, RightEyeClip, RightPupilOverlay, RightPupilOffset, RightEyeForeground, RightEyeForegroundImage, RightClosedEye, RightClosedEyeImage, RightHalfEye, RightHalfEyeImage);
        double fadeProgress = MouthFading ? Math.Clamp((t - _mouthFadeStart) / _mouthFadeDuration, 0, 1) : 1;
        // Keep the outgoing opaque silhouette beneath the fade, avoiding a whole-avatar opacity dip.
        CurrentStateLayer.Opacity = fadeProgress; PreviousStateLayer.Opacity = fadeProgress < 1 ? 1 : 0;
        if (fadeProgress >= 1) PreviousStateImage.Source = null;
        double x = 0, y = 0, angle = 0, opacity = 1;
        if (_editingEye == null)
        foreach (var effect in _character.Effects)
        {
            if (_previewEffect != null && !ReferenceEquals(effect, _previewEffect)) continue;
            if (!effect.Enabled && !ReferenceEquals(effect, _previewEffect)) continue;
            bool talking = AvatarService.Instance.IsTalking || _previewing;
            bool active = ReferenceEquals(effect, _previewEffect) || effect.When == "always" || effect.When == "talk" && talking ||
                effect.When == "idle" && !talking || effect.When == "start" && t - _speechStarted < 1 / Math.Clamp(effect.Speed, .1, 5);
            if (!active) continue;
            double amount = Math.Clamp(effect.Amount, 0, 1), speed = Math.Clamp(effect.Speed, .1, 5);
            double phase = (effect.When == "start" ? t - _speechStarted : t) * speed * Math.PI * 2;
            switch (effect.Kind)
            {
                case "darken": opacity *= 1 - amount * .8; break;
                case "jump": y -= Math.Abs(Math.Sin(phase / 2)) * amount * 90; break;
                case "random": x += Noise(t * speed * 4) * amount * 35; y += Noise(t * speed * 4 + 712) * amount * 25; break;
                case "wave": x += Math.Sin(phase) * amount * 55; y += Math.Cos(phase) * amount * 15; break;
                case "rotate": angle += Math.Sin(phase) * amount * 15; break;
            }
        }
        var head = _character.Head;
        bool headActive = head.Enabled && BodyImage.Source != null && _editingEye == null && _previewEffect == null;
        _headSpeech += ((AvatarService.Instance.IsTalking || _previewing ? 1.0 : 0) - _headSpeech) * GazeMath.SmoothingFactor(dt, 180);
        double headPhase = t * Math.Clamp(head.Speed, .1, 3);
        double headTarget = headActive && head.FollowGaze && p.Enabled ? NaturalMotion.HeadTarget(_gaze.X * Math.Clamp(p.Range, 0, 1), head.GazeThreshold, head.GazeAngle) : 0;
        _headGaze += (headTarget - _headGaze) * GazeMath.SmoothingFactor(dt, 220);
        if (!headActive) _headGaze = 0;
        HeadRotation.Angle = headActive ? Math.Sin(headPhase * .8) * Math.Clamp(head.Angle, 0, 12) + _headGaze : 0;
        HeadTranslation.Y = headActive ? Math.Sin(headPhase * 5) * Math.Clamp(head.SpeechMotion, 0, 3) * 4 * _headSpeech : 0;
        double yawTarget = headActive && head.PerspectiveEnabled
            ? (head.PerspectiveMode == "auto" ? Math.Sin(t * .45) : p.Enabled ? _gaze.X * Math.Clamp(p.Range,0,1) : 0) * Math.Clamp(head.PerspectiveAngle,0,18) : 0;
        _perspectiveYaw += (yawTarget - _perspectiveYaw) * GazeMath.SmoothingFactor(dt,250);
        if (!headActive) _perspectiveYaw=0;
        _perspective.Yaw=_perspectiveYaw;
        double nodTarget=0;
        if(headActive&&head.MouseNodEnabled&&IsLoaded&&PresentationSource.FromVisual(Artwork)!=null&&GetCursorPos(out var headCursor)){
            var cursorPoint=Artwork.PointFromScreen(new Point(headCursor.X,headCursor.Y));
            nodTarget=HeadNodMotion.Target(cursorPoint.Y/Math.Max(1,Artwork.Height),head);
        }
        _mouseNod+=(nodTarget-_mouseNod)*GazeMath.SmoothingFactor(dt,Math.Clamp(head.MouseNodSmoothingMs,30,800));
        if(!headActive||!head.MouseNodEnabled)_mouseNod=0;
        HeadRotation.Angle += _mouseNod;
        _perspective.Pitch=0;
        bool breathing = head.BreathingEnabled && _editingEye == null && _previewEffect == null;
        if (breathing) _breathPhase += dt * Math.Clamp(head.BreathingRate,4,24) * Math.PI * 2 / 60;
        double breath = breathing ? NaturalMotion.Breath(_breathPhase, head.BreathingAmount) : 0;
        BreathScale.ScaleX = 1 + breath * .003; BreathScale.ScaleY = 1 + breath * .008;
        RenderPose(dt,headActive);
        RenderScreenLight(dt);
        RenderScreenDepth(dt);
        BodyShade.Opacity = 1 - opacity;
        EffectTranslation.X = x; EffectTranslation.Y = y; EffectRotation.Angle = angle;
        if(_editingEye==null){RenderAccessories(dt);RenderHands(dt);}
        RenderScreenEffect(t);
        RenderRobotScreen(dt,t);RenderVoiceLights(dt);
        // Darkening is applied as a black silhouette mask, preserving alpha and scene colour.
        DarkenOverlay.Opacity = PreviousStateShade.Opacity = LeftHalfShade.Opacity = RightHalfShade.Opacity = 1 - opacity;
        RenderIdleExpression(dt);
        RenderMouthLayer(dt, t);
        LeftPupilShade.Opacity = RightPupilShade.Opacity = LeftClosedEyeShade.Opacity = RightClosedEyeShade.Opacity = LeftWhiteShade.Opacity = RightWhiteShade.Opacity = LeftForegroundShade.Opacity = RightForegroundShade.Opacity = 1 - opacity;
    }

    private void SyncPerspective()
    {
        if (_character == null) return;
        var h = _character.Head;
        bool enabled = h.Enabled && h.PerspectiveEnabled && BodyImage.Source != null && _editingEye == null && !IsMouthCurveEditing && !_placingMouth && !IsAccessoryEditing && !IsRobotEditing && !IsVoiceLightsEditing;
        var layout = (Artwork.Width, Artwork.Height, h.PivotX, h.PivotY);
        if (enabled && _projectionLayout != layout)
        {
            _projectionLayout = layout;
            Composite.Width = Artwork.Width; Composite.Height = Artwork.Height;
            _perspective.Configure(Artwork.Width,Artwork.Height,h.PivotX,h.PivotY);
        }
        if (enabled == _perspectiveAttached) return;
        if (enabled)
        {
            HeadSurface.Children.Remove(Composite);
            _perspective.Attach(Composite); HeadSurface.Children.Add(_perspective);
        }
        else
        {
            _perspective.Detach(); HeadSurface.Children.Remove(_perspective);
            Composite.Width=double.NaN; Composite.Height=double.NaN;
            HeadSurface.Children.Add(Composite); _projectionLayout=default;
        }
        _perspectiveAttached=enabled;
    }

    private static double Noise(double time)
    {
        double index = Math.Floor(time), u = time - index;
        u = u * u * (3 - 2 * u);
        static double Sample(double n) { double value = Math.Sin(n * 127.1 + 311.7) * 43758.5453; return (value - Math.Floor(value)) * 2 - 1; }
        return Sample(index) * (1 - u) + Sample(index + 1) * u;
    }

    private void ConfigureClosedEye(EyeRegion eye, Grid image)
    {
        image.Width = Math.Clamp(eye.Width, .005, 1) * Artwork.Width;
        image.Height = Math.Clamp(eye.Height, .005, 1) * Artwork.Height;
        Canvas.SetLeft(image, Math.Clamp(eye.X, 0, 1) * Artwork.Width - image.Width / 2);
        Canvas.SetTop(image, Math.Clamp(eye.Y, 0, 1) * Artwork.Height - image.Height / 2);
        image.RenderTransform = EyePlaneTransform(eye,image.Width,image.Height);
    }
    private void ConfigureEyeLayout(EyeRegion eye, Canvas clip, Grid sprite, Ellipse outline)
    {
        double w = Math.Clamp(eye.Width, .005, 1) * Artwork.Width, h = Math.Clamp(eye.Height, .005, 1) * Artwork.Height;
        double cx = Math.Clamp(eye.X, 0, 1) * Artwork.Width, cy = Math.Clamp(eye.Y, 0, 1) * Artwork.Height;
        double size = Math.Clamp(eye.PupilSize, .002, .5) * Artwork.Width;
        clip.Width = w; clip.Height = h;
        clip.RenderTransform = EyePlaneTransform(eye,w,h);
        outline.RenderTransform = new RotateTransform(eye.RotationDegrees, w / 2, h / 2);
        var maskImage = ReferenceEquals(clip, LeftEyeClip) ? _leftMask : _rightMask;
        Geometry mask = maskImage == null ? new EllipseGeometry(new Rect(0, 0, w, h)) : new RectangleGeometry(new Rect(0, 0, w, h));
        if (maskImage != null) { var brush = new ImageBrush(maskImage) { Stretch = Stretch.Fill }; brush.Freeze(); clip.OpacityMask = brush; }
        else clip.OpacityMask = null;
        mask.Freeze(); clip.Clip = mask;
        Canvas.SetLeft(clip, cx - w / 2); Canvas.SetTop(clip, cy - h / 2);
        sprite.Width = sprite.Height = size;
        Canvas.SetLeft(sprite, (w - size) / 2); Canvas.SetTop(sprite, (h - size) / 2);
        outline.Width = w; outline.Height = h;
        Canvas.SetLeft(outline, cx - w / 2); Canvas.SetTop(outline, cy - h / 2);
    }

    private Transform EyePlaneTransform(EyeRegion eye,double width,double height) =>
        eye.PerspectiveEnabled&&_editingEye==null ? new MatrixTransform(EyePerspective.Plane(eye,width,height)) : new RotateTransform(eye.RotationDegrees,width/2,height/2);

    private void MoveEye(EyeRegion eye, Canvas clip, Image image, TranslateTransform translation, bool visible)
    {
        clip.Visibility = visible && (image.Source != null || (ReferenceEquals(clip, LeftEyeClip) ? LeftEyeWhite.Source : RightEyeWhite.Source) != null) ? Visibility.Visible : Visibility.Collapsed;
        if (clip.Visibility != Visibility.Visible) return;
        var direction = EyePerspective.Direction(eye,_gaze);
        var offset = GazeMath.Constrain(_editingEye != null ? new Vector() : direction, clip.Width / 2, clip.Height / 2, _character!.Pupils.Range);
        translation.X = offset.X; translation.Y = offset.Y;
    }

    // The animation clock follows actual composition frames instead of competing with them through a UI timer.
    private void OnRendering(object? sender, EventArgs e)
    {
        if (_editingPaused) return;
        if (e is RenderingEventArgs rendering)
        {
            if (rendering.RenderingTime == _lastRenderTime) return;
            _lastRenderTime = rendering.RenderingTime;
        }
        if (Window.GetWindow(this)?.WindowState == WindowState.Minimized) return;
        RenderFrame();
        if (!MouthFading && !BlinkTransition) UpdateRenderingSubscription(false);
    }

    public void UpdateRenderingSubscription(bool render = true)
    {
        bool needsAnimation = !_editingPaused && IsLoaded && _character != null &&
            (_character.Hands.Enabled || _handsTest>0 || _character.VoiceLights.Enabled || _voiceLightsMotion.Moving || RobotScreenActive || _badgeTests.Count>0 || _character.IdleExpression.Enabled || _idleExpression.Moving || _depthTest>0 || LayerMouthActive || MouthFading || BlinkTransition || _previewing || _character.ScreenLight.Enabled || _character.ScreenEffect.DepthEnabled || _accessoryItems.Values.Any(a=>a.Options.Visible && (a.Options.Motion || a.Options.PulseGlow || a.Swing.Moving || _accessoryAnimations.ContainsKey(a.Options.Id))) || _character.Head.PoseEnabled || _poseMotion.Moving || _character.ScreenEffect.Enabled || _character.ScreenEffect.Scanlines || _character.ScreenEffect.Flicker || (_character.Head.Enabled && BodyImage.Source != null) || _character.Head.BreathingEnabled || (_character.Pupils.Enabled && _character.Pupils.EffectiveGazeMode != "forward") || _character.Effects.Any(e => e.Enabled && e.Kind != "darken"));
        if (needsAnimation && !_subscribedToRendering)
        {
            _lastFrame = _clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += OnRendering;
            _subscribedToRendering = true;
        }
        else if (!needsAnimation) StopRendering();
        if (render) RenderFrame();
    }
    private void StopRendering()
    {
        if (!_subscribedToRendering) return;
        CompositionTarget.Rendering -= OnRendering;
        _subscribedToRendering = false;
    }

    public void EditEyeRegion(int? eye)
    {
        if (eye != null) EndMouthCurveEditing(false);
        _editingEye = eye; _drawStart = null; RegionEditor.ReleaseMouseCapture();
        _regionDraft = eye != null && _character != null ? (eye == 0 ? _character.Pupils.Left : _character.Pupils.Right).Copy() : null;
        if (_regionDraft != null)
        {
            var bounds = EyeRegionGeometry.Bounds(_regionDraft);
            EyeRegionGeometry.Assign(_regionDraft, EyeRegionGeometry.Draw(bounds.TopLeft, bounds.BottomRight));
        }
        RegionEditor.Visibility = eye != null ? Visibility.Visible : Visibility.Collapsed;
        LeftRegionOutline.Visibility = eye == 0 ? Visibility.Visible : Visibility.Collapsed;
        RightRegionOutline.Visibility = eye == 1 ? Visibility.Visible : Visibility.Collapsed;
        RegionEditor.Cursor = Cursors.Cross;
        _eyeLayoutDirty = true;
        RegionEditingChanged?.Invoke(this, eye != null);
        RenderFrame();
    }
    public void ApplyEyeRegion()
    {
        if (_regionDraft == null || _character == null || _editingEye == null) return;
        var target = _editingEye == 0 ? _character.Pupils.Left : _character.Pupils.Right;
        EyeRegionGeometry.Assign(target, EyeRegionGeometry.Bounds(_regionDraft));
        target.RotationDegrees = _regionDraft.RotationDegrees;
        EditEyeRegion(null);
        RegionChanged?.Invoke(this, EventArgs.Empty);
    }
    private Point RotateRegionPoint(Point point, double angle, bool dragOrigin = false)
    {
        if (_regionDraft == null) return point;
        double cx = dragOrigin ? _dragBounds.X + _dragBounds.Width / 2 : _regionDraft.X;
        double cy = dragOrigin ? _dragBounds.Y + _dragBounds.Height / 2 : _regionDraft.Y;
        var rotation = new RotateTransform(angle, cx * Artwork.Width, cy * Artwork.Height);
        var rotated = rotation.Transform(new Point(point.X * Artwork.Width, point.Y * Artwork.Height));
        return new Point(rotated.X / Artwork.Width, rotated.Y / Artwork.Height);
    }
    private void Region_Wheel(object sender, MouseWheelEventArgs e)
    {
        if (_regionDraft == null || _drawStart != null) return;
        double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 5;
        _regionDraft.RotationDegrees = ((_regionDraft.RotationDegrees + Math.Sign(e.Delta) * step + 540) % 360) - 180;
        _eyeLayoutDirty = true; RenderFrame(); e.Handled = true;
    }
    private Point RegionPoint(MouseEventArgs e)
    {
        var point = e.GetPosition(RegionEditor);
        return new Point(Math.Clamp(point.X / Artwork.Width, 0, 1), Math.Clamp(point.Y / Artwork.Height, 0, 1));
    }
    private Point[] GripPoints(Rect b) => new[] { b.TopLeft, new Point(b.X + b.Width / 2, b.Top), b.TopRight,
        new Point(b.Right, b.Y + b.Height / 2), b.BottomRight, new Point(b.X + b.Width / 2, b.Bottom), b.BottomLeft, new Point(b.Left, b.Y + b.Height / 2) };
    private double GripSize()
    {
        if (!IsLoaded) return 12;
        var scale = (Artwork.TransformToAncestor(this).Transform(new Point(1, 0)) - Artwork.TransformToAncestor(this).Transform(new Point())).Length;
        return 10 / Math.Max(.05, scale);
    }
    private void UpdateRegionGrips()
    {
        if (_regionDraft == null) return;
        var points = GripPoints(EyeRegionGeometry.Bounds(_regionDraft)).Select(point => RotateRegionPoint(point, _regionDraft.RotationDegrees)).ToArray();
        double size = GripSize();
        for (int i = 0; i < 8; i++)
        {
            var grip = _regionGrips[i]; grip.Width = grip.Height = size;
            Canvas.SetLeft(grip, points[i].X * Artwork.Width - size / 2);
            Canvas.SetTop(grip, points[i].Y * Artwork.Height - size / 2);
        }
    }
    private int HitRegion(Point point)
    {
        if (_regionDraft == null) return -2;
        point = RotateRegionPoint(point, -_regionDraft.RotationDegrees);
        var bounds = EyeRegionGeometry.Bounds(_regionDraft); var points = GripPoints(bounds);
        double tolerance = GripSize();
        for (int i = 0; i < 8; i++)
            if (Math.Abs((point.X - points[i].X) * Artwork.Width) <= tolerance && Math.Abs((point.Y - points[i].Y) * Artwork.Height) <= tolerance) return i;
        return bounds.Contains(point) ? -1 : -2;
    }
    private static Cursor HandleCursor(int handle) => handle switch
    {
        -1 => Cursors.SizeAll, 0 or 4 => Cursors.SizeNWSE, 2 or 6 => Cursors.SizeNESW,
        1 or 5 => Cursors.SizeNS, 3 or 7 => Cursors.SizeWE, _ => Cursors.Cross
    };
    private void Region_Down(object sender, MouseButtonEventArgs e)
    {
        if (_screenEditing)
        {
            _drawStart = ScreenPoint(e); _dragBounds = new Rect(_screenDraft.X * Artwork.Width, _screenDraft.Y * Artwork.Height, _screenDraft.Width * Artwork.Width, _screenDraft.Height * Artwork.Height);
            var point = _drawStart.Value; double tolerance = GripSize(); _regionHandle = -1;
            var grips = GripPoints(_dragBounds); for (int i=0;i<grips.Length;i++) if ((grips[i]-point).Length <= tolerance*1.5) { _regionHandle=i; break; }
            RegionEditor.CaptureMouse(); e.Handled=true; return;
        }
        if (_regionDraft == null || StateImage.Source == null) return;
        _drawStart = RegionPoint(e); _dragBounds = EyeRegionGeometry.Bounds(_regionDraft);
        _regionHandle = HitRegion(_drawStart.Value);
        if (_regionHandle == -2) _regionDraft.RotationDegrees = 0;
        RegionEditor.CaptureMouse(); e.Handled = true;
    }
    private void Region_Move(object sender, MouseEventArgs e)
    {
        if (_screenEditing)
        {
            if (_drawStart is not Point screenStart || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = ScreenPoint(e) - screenStart;
            var screenBounds = _regionHandle == -1 ? new Rect(_dragBounds.X + delta.X, _dragBounds.Y + delta.Y, _dragBounds.Width, _dragBounds.Height) : ResizeScreenPixels(_dragBounds, ScreenPoint(e), _regionHandle);
            _screenDraft = new Rect(screenBounds.X / Artwork.Width, screenBounds.Y / Artwork.Height, screenBounds.Width / Artwork.Width, screenBounds.Height / Artwork.Height);
            ApplyScreenEditor(); RefreshScreenEditor(); e.Handled=true; return;
        }
        var point = RegionPoint(e);
        if (_drawStart is not Point start || _regionDraft == null || e.LeftButton != MouseButtonState.Pressed)
        { RegionEditor.Cursor = HandleCursor(HitRegion(point)); return; }
        var bounds = _regionHandle switch
        {
            -1 => EyeRegionGeometry.Move(_dragBounds, point - start),
            -2 => EyeRegionGeometry.Draw(start, point),
            _ => EyeRegionGeometry.Resize(_dragBounds, RotateRegionPoint(point, -_regionDraft.RotationDegrees, true), _regionHandle)
        };
        if (_regionHandle >= 0 && Math.Abs(_regionDraft.RotationDegrees) > .001)
        {
            var localCenter = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var worldCenter = RotateRegionPoint(localCenter, _regionDraft.RotationDegrees, true);
            bounds = new Rect(worldCenter.X - bounds.Width / 2, worldCenter.Y - bounds.Height / 2, bounds.Width, bounds.Height);
        }
        EyeRegionGeometry.Assign(_regionDraft, bounds);
        _eyeLayoutDirty = true; RenderFrame(); e.Handled = true;
    }
    private void Region_Up(object sender, MouseButtonEventArgs e)
    {
        if (_screenEditing)
        {
            if (_drawStart == null) return;
            Region_Move(sender,e); _drawStart=null; RegionEditor.ReleaseMouseCapture(); ApplyScreenEditor(); e.Handled=true; return;
        }
        if (_drawStart == null) return;
        Region_Move(sender, e);
        _drawStart = null; RegionEditor.ReleaseMouseCapture(); e.Handled = true;
    }

    private static void UpdateLayer(Image image, string path)
    {
        if (Equals(image.Tag, path)) return;
        image.Tag = path; image.Source = LoadImage(path);
    }

    private Rect ResizeScreenPixels(Rect initial, Point point, int handle)
    {
        double left=initial.Left, right=initial.Right, top=initial.Top, bottom=initial.Bottom;
        double maxW=Math.Max(1, Artwork.Width), maxH=Math.Max(1, Artwork.Height);
        if (handle is 0 or 6 or 7) left=Math.Clamp(point.X,0,right-5);
        if (handle is 2 or 3 or 4) right=Math.Clamp(point.X,left+5,maxW);
        if (handle is 0 or 1 or 2) top=Math.Clamp(point.Y,0,bottom-5);
        if (handle is 4 or 5 or 6) bottom=Math.Clamp(point.Y,top+5,maxH);
        return new Rect(new Point(left,top),new Point(right,bottom));
    }

    public void SetEditingPaused(bool paused)
    {
        _editingPaused = paused;
        if (paused) {ResetIdleExpression();_depthOffset=0;_depthEyes.X=_depthMouth.X=_depthGlass.X=0;StopRendering();}
        else { _lastFrame=_clock.Elapsed.TotalSeconds; UpdateScene(); UpdateRenderingSubscription(); }
    }

    private void ApplyBodyLighting()
    {
        if (_character == null) return;
        BodyImage.Source = LitImage(Clothed(_character.Head.BodyImagePath,LightFile(_character.Head.BodyImagePath)),true);
    }

    private BitmapSource? ApplyHeadLighting(BitmapSource? diffuse)
    {
        return LitImage(diffuse,false);
    }
    private static BitmapImage? LoadImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        catch { return null; }
    }
}
