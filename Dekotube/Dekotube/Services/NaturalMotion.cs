using System.Windows;
namespace Dekotube.Services;

public static class NaturalMotion
{
    public static double Breath(double phase, double amount) =>
        (1-Math.Cos(phase))*.5 * (.9+.1*Math.Sin(phase*.173)) * Math.Clamp(amount,0,3);
    public static double HeadTarget(double gazeX, double threshold, double angle)
    {
        double u = Math.Clamp((Math.Abs(gazeX) - Math.Clamp(threshold, 0, .95)) / (1 - Math.Clamp(threshold, 0, .95)), 0, 1);
        return Math.Sign(gazeX) * u * u * (3 - 2 * u) * Math.Clamp(angle, 0, 8);
    }
    // Exact critically damped spring step; no overshoot for a stationary target from rest.
    public static Vector Smooth(Vector position, Vector target, ref Vector velocity, double dt, double milliseconds)
    {
        if (milliseconds <= 0) { velocity = new(); return target; }
        double omega = 4 / (milliseconds / 1000), decay = Math.Exp(-omega * Math.Max(0, dt));
        var offset = position - target;
        var impulse = (velocity + offset * omega) * Math.Max(0, dt);
        velocity = (velocity - impulse * omega) * decay;
        var next = target + (offset + impulse) * decay;
        if (next.Length > 1) { next.Normalize(); velocity = new(); }
        return next;
    }
    // A deliberately discrete intermediate drawing, not a generated deformation.
    public static int BlinkPhase(bool closed, double elapsed, double durationMs) => closed
        ? (elapsed < Math.Min(.045, Math.Clamp(durationMs,30,1000)/2500) ? 1 : 2)
        : (elapsed < .07 ? 1 : 0);
}
