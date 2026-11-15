using System.Windows;
namespace Dekotube.Services;

/// <summary>Small shared eye targets, with pauses and regular returns to centre.</summary>
public sealed class AutomaticGaze
{
    private readonly Random _random;
    private double _next;
    private Vector _target;
    private bool _away;
    public AutomaticGaze(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : new Random();
    public void Reset() { _next = 0; _target = new(); _away = false; }
    public Vector Target(double seconds, double strength)
    {
        var target = Target(seconds) * Math.Clamp(strength, 0, 3);
        if (target.Length > 1) target.Normalize();
        return target;
    }
    public Vector Target(double seconds)
    {
        if (seconds < _next) return _target;
        _away = !_away;
        _target = _away ? new Vector((_random.NextDouble() - .5) * .7, (_random.NextDouble() - .5) * .35) : new Vector();
        _next = seconds + (_away ? 1.2 : 2.5) + _random.NextDouble() * 2.5;
        return _target;
    }
}
