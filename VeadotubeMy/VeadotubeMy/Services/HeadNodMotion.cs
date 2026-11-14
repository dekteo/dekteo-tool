namespace VeadotubeMy.Services;
public static class HeadNodMotion
{
    public static double Target(double normalizedY,HeadOptions options)=>
        double.IsFinite(normalizedY)?Math.Clamp((normalizedY-.5)*2,-1,1)*Math.Clamp(options.MouseNodAngle,0,18)*(options.MouseNodInvert?-1:1):0;
}
