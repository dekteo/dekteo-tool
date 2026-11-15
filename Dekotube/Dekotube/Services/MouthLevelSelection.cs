namespace Dekotube.Services;

public static class MouthLevelSelection
{
    // A five-point dead band prevents rapid PNG changes near the boundary.
    public static bool IsWide(bool talking, double level, double threshold, bool wasWide) =>
        talking && level >= System.Math.Max(0, System.Math.Clamp(threshold, 0, 100) - (wasWide ? 5 : 0));
}
