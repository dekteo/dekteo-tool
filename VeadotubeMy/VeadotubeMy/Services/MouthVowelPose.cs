namespace VeadotubeMy.Services;

public sealed class MouthVowelPose
{
    public double Width { get; set; } = 1;
    public double Height { get; set; } = 1;
    public double? Opening { get; set; }
    public double[] Curve { get; set; } = [0,0,.5,0,1,0];
    public MouthVowelPose Copy() => new() { Width=double.IsFinite(Width)?Math.Clamp(Width,.2,1.5):1, Height=double.IsFinite(Height)?Math.Clamp(Height,.1,1):1, Opening=Opening is double value && double.IsFinite(value)?Math.Clamp(value,.02,1):null, Curve=MouthLayerOptions.CurveOrDefault(Curve) };
    public static MouthVowelPose Default(VowelShape shape) => shape switch
    {
        VowelShape.A => new() { Width=1.08, Height=1 },
        VowelShape.O => new() { Width=.48, Height=.95 },
        VowelShape.U => new() { Width=.28, Height=.65 },
        VowelShape.IE => new() { Width=1.25, Height=.25, Curve=[0,-.18,.5,.04,1,-.18] },
        VowelShape.E => new() { Width=.95, Height=.6, Curve=[0,-.08,.5,.12,1,-.08] },
        _ => new()
    };
}
