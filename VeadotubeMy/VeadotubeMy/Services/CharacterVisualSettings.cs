using System.Windows;

namespace VeadotubeMy.Services;

public sealed class EyeRegion
{
    public List<string> HiddenLayers { get; set; } = new();
    public bool IsLayerVisible(string key) => HiddenLayers?.Contains(key) != true;
    public void SetLayerVisible(string key, bool visible)
    {
        HiddenLayers ??= new();
        HiddenLayers.RemoveAll(x => x == key);
        if (!visible) HiddenLayers.Add(key);
    }
    public string VisiblePath(string key, string path) => IsLayerVisible(key) ? path : "";
    public string HalfClosedImagePath { get; set; } = "";
    public string WhiteImagePath { get; set; } = "";
    public string MaskImagePath { get; set; } = "";
    public string ForegroundImagePath { get; set; } = "";
    public string ClosedEyeImagePath { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public double X { get; set; } = .45;
    public double Y { get; set; } = .35;
    public double Width { get; set; } = .10;
    public double Height { get; set; } = .07;
    public double RotationDegrees { get; set; }
    public bool PerspectiveEnabled { get; set; }
    public double PerspectiveYaw { get; set; } = 25;
    public double PerspectiveSlope { get; set; }
    public double PerspectiveTravel { get; set; } = 1;
    public double PupilSize { get; set; } = .035;
    public void CopySizeFrom(EyeRegion source) { Width = source.Width; Height = source.Height; PupilSize = source.PupilSize; }
    public EyeRegion Copy() { var copy = (EyeRegion)MemberwiseClone(); copy.HiddenLayers = HiddenLayers?.ToList() ?? new(); return copy; }
}

public sealed class PupilOptions
{
    public bool LinkSizes { get; set; }
    public bool Enabled { get; set; }
    public string? GazeMode { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveGazeMode => GazeMode is "forward" or "mouse" or "auto" or "hybrid" ? GazeMode : FollowMouse ? "mouse" : "forward";
    public double AutoGazeStrength { get; set; } = 1;
    public double MouseIdleSeconds { get; set; } = 3;
    public bool FollowMouse { get; set; } = true;
    public bool SoftGaze { get; set; }
    public bool SmoothBlink { get; set; }
    public string BlinkStyle { get; set; } = "images";
    public bool DigitalBlinkMask { get; set; }
    public double DigitalBlinkSpeedMs { get; set; } = 450;
    public double DigitalBlinkCompression { get; set; } = .62;
    public bool BlinkEnabled { get; set; } = true;
    public int BlinkIntervalMs { get; set; } = 4000;
    public int MaxBlinkIntervalMs { get; set; } = 8000;
    public int BlinkDurationMs { get; set; } = 150;
    public EyeRegion Left { get; set; } = new();
    public EyeRegion Right { get; set; } = new() { X = .55 };
    public double Range { get; set; } = .8;
    public double SmoothingMs { get; set; } = 90;
    public PupilOptions Copy() => new() { Enabled = Enabled, LinkSizes = LinkSizes, SoftGaze = SoftGaze, SmoothBlink = SmoothBlink, BlinkStyle = BlinkStyle, DigitalBlinkMask = DigitalBlinkMask, DigitalBlinkSpeedMs = DigitalBlinkSpeedMs, DigitalBlinkCompression = DigitalBlinkCompression, GazeMode = GazeMode, AutoGazeStrength = AutoGazeStrength, MouseIdleSeconds = MouseIdleSeconds, FollowMouse = FollowMouse, BlinkEnabled = BlinkEnabled, BlinkIntervalMs = BlinkIntervalMs, MaxBlinkIntervalMs = MaxBlinkIntervalMs, BlinkDurationMs = BlinkDurationMs, Left = Left.Copy(), Right = Right.Copy(), Range = Range, SmoothingMs = SmoothingMs };
}

public sealed class CharacterEffect
{
    public string Kind { get; set; } = "jump";
    public bool Enabled { get; set; } = true;
    public string When { get; set; } = "talk";
    public double Amount { get; set; } = .3;
    public double Speed { get; set; } = 1;
    public CharacterEffect Copy() => (CharacterEffect)MemberwiseClone();
}

public sealed class ScreenEffectOptions
{
    public bool DepthEnabled { get; set; }
    public double DepthAmount { get; set; } = 5;
    public bool Added { get; set; }
    public bool Enabled { get; set; }
    public double X { get; set; } = .25;
    public double Y { get; set; } = .16;
    public double Width { get; set; } = .5;
    public double Height { get; set; } = .42;
    public double Opacity { get; set; } = .16;
    public double Speed { get; set; } = .08;
    public bool Scanlines { get; set; }
    public bool Glow { get; set; }
    public bool Flicker { get; set; }
    public double ScanlineOpacity { get; set; } = .12;
    public double ScanlineSpeed { get; set; } = 18;
    public double FlickerAmount { get; set; } = .12;
    public double CornerRadius { get; set; } = 18;
    public double SkewX { get; set; }
    public double SkewY { get; set; }
    public double CurveTop { get; set; }
    public double CurveBottom { get; set; }
    public double CurveLeft { get; set; }
    public double CurveRight { get; set; }
    public ScreenEffectOptions Copy() => (ScreenEffectOptions)MemberwiseClone();
}

/// <summary>Настройки будущего normal-map освещения персонажа.</summary>
public sealed class LightingOptions
{
    public bool Added { get; set; }
    public bool Enabled { get; set; }
    public string BodyNormalPath { get; set; } = "";
    public string HeadNormalPath { get; set; } = "";
    public string LightTarget { get; set; } = "scene";
    public double LightX { get; set; } = .5;
    public double LightY { get; set; } = .35;
    public double LightHeight { get; set; } = .5;
    public double Intensity { get; set; } = 1;
    public double Radius { get; set; } = .8;
    public string Color { get; set; } = "#FFF4D6";
    public LightingOptions Copy() => (LightingOptions)MemberwiseClone();
}

public static class GazeMath
{
    // The displacement remains inside an ellipse even at the screen corners.
    public static Vector Constrain(Vector direction, double radiusX, double radiusY, double range)
    {
        if (direction.Length > 1) direction.Normalize();
        return new Vector(direction.X * Math.Max(0, radiusX) * Math.Clamp(range, 0, 1),
                          direction.Y * Math.Max(0, radiusY) * Math.Clamp(range, 0, 1));
    }
    public static double SmoothingFactor(double seconds, double milliseconds) =>
        milliseconds <= 0 ? 1 : 1 - Math.Exp(-Math.Max(0, seconds) * 1000 / milliseconds);
}

public sealed class HeadOptions
{
    public bool PoseEnabled { get; set; }
    public double PoseAmount { get; set; } = 1;
    public bool PerspectiveEnabled { get; set; }
    public bool MouseNodEnabled { get; set; }
    public double MouseNodAngle { get; set; } = 8;
    public double MouseNodSmoothingMs { get; set; } = 180;
    public bool MouseNodInvert { get; set; }
    public double PerspectiveAngle { get; set; } = 10;
    public string PerspectiveMode { get; set; } = "gaze";
    public bool BreathingEnabled { get; set; }
    public double BreathingAmount { get; set; } = 1;
    public double BreathingRate { get; set; } = 12;
    public bool Enabled { get; set; }
    public string BodyImagePath { get; set; } = "";
    public bool FollowGaze { get; set; }
    public double GazeAngle { get; set; } = 3;
    public double GazeThreshold { get; set; } = .7;
    public double PivotX { get; set; } = .5;
    public double PivotY { get; set; } = .65;
    public double Angle { get; set; } = 3;
    public double SpeechMotion { get; set; } = 1;
    public double Speed { get; set; } = 1;
    public HeadOptions Copy() => (HeadOptions)MemberwiseClone();
}
