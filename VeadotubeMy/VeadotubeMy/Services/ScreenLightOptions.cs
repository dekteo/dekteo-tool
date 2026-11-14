namespace VeadotubeMy.Services;
public sealed class ScreenLightOptions
{
    public bool Enabled { get; set; }
    public bool Darken { get; set; }
    public double DarkenStrength { get; set; } = .5;
    public bool ProtectScreen { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Strength { get; set; } = .3;
    public double SmoothSeconds { get; set; } = .6;
    public int Fps { get; set; } = 5;
    public string Target { get; set; } = "all";
    public ScreenLightOptions Copy()=>(ScreenLightOptions)MemberwiseClone();
}
