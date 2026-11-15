namespace Dekotube.Services;

public sealed class MouthLayerOptions
{
    public bool Enabled { get; set; }
    public bool Procedural { get; set; }
    public bool ProceduralVowels { get; set; }
    public string Color { get; set; } = "#58F4FF";
    public double Glow { get; set; } = 12;
    public double RestThickness { get; set; } = .10;
    public double MaxOpening { get; set; } = .4;
    public double Roundness { get; set; } = .5;
    public double Liveliness { get; set; } = .45;
    public double VowelInfluence { get; set; } = .35;
    public bool BendEnabled { get; set; } = true;
    public bool StretchEnabled { get; set; } = true;
    public bool GlowEnabled { get; set; } = true;
    public bool HighlightEnabled { get; set; } = true;
    public bool UseStripPng { get; set; }
    public string StripPath { get; set; } = "";
    public double[] RestCurve { get; set; } = [0,0,.5,0,1,0];
    public double[] OpenCurve { get; set; } = [0,0,.5,0,1,0];
    public Dictionary<VowelShape, MouthVowelPose> VowelPoses { get; set; } = new();
    public string BasePath { get; set; } = "";
    public string BlinkBasePath { get; set; } = "";
    public string ClosedPath { get; set; } = "";
    public string OpenPath { get; set; } = "";
    public Dictionary<VowelShape, string> VowelPaths { get; set; } = new();
    public double X { get; set; } = .5;
    public double Y { get; set; } = .65;
    public double Width { get; set; } = .16;
    public double Height { get; set; } = .12;
    public double Rotation { get; set; }
    public double PerspectiveYaw { get; set; }
    public double PerspectivePitch { get; set; }
    public double FullVolume { get; set; } = 75;
    public MouthLayerOptions Copy()
    {
        var copy = (MouthLayerOptions)MemberwiseClone();
        copy.VowelPaths = (VowelPaths ?? new()).ToDictionary(x=>x.Key,x=>x.Value);
        copy.RestCurve = CurveOrDefault(RestCurve);
        copy.OpenCurve = CurveOrDefault(OpenCurve);
        copy.VowelPoses = (VowelPoses ?? new()).Where(x=>x.Value!=null).ToDictionary(x=>x.Key,x=>x.Value.Copy());
        return copy;
    }
    public static double[] CurveOrDefault(double[]? curve) => curve is { Length: 6 } && curve.All(double.IsFinite) ? (double[])curve.Clone() : [0,0,.5,0,1,0];
    public static (double Width, double Height) ProceduralShape(double opening, VowelShape vowel, double thickness, double maxOpening = 1)
    {
        opening = Math.Clamp(opening, 0, 1);
        thickness = Math.Clamp(thickness, .02, .4);
        double width = vowel switch { VowelShape.O => .6, VowelShape.U => .4, VowelShape.IE => 1.12, _ => 1 };
        double height = vowel switch { VowelShape.U => .65, VowelShape.IE => .45, _ => 1 };
        double maximum = Math.Max(thickness, Math.Clamp(maxOpening, .02, 1));
        return (1 + (width - 1) * opening, thickness + (Math.Max(thickness, height * maximum) - thickness) * opening);
    }
    public static double Smooth(double current, double target, double dt) =>
        current + (target - current) * (1 - Math.Exp(-Math.Clamp(dt, 0, .1) / (target > current ? .035 : .09)));
}
