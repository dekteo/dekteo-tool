namespace Dekotube.Services;

public sealed class ColorCorrectionOptions
{
    public bool Enabled { get; set; }
    public double Brightness { get; set; }
    public double Contrast { get; set; } = 1;
    public double Gamma { get; set; } = 1;
    public double Hue { get; set; }
    public ColorCorrectionOptions Copy() => (ColorCorrectionOptions)MemberwiseClone();
}
