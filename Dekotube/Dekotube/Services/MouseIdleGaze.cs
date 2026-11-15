using System.Windows;
namespace Dekotube.Services;
public sealed class MouseIdleGaze
{
    private Point? _position;
    private double _lastMovement;
    public void Reset() { _position = null; _lastMovement = 0; }
    public bool IsIdle(Point position, double now, double delay)
    {
        if (_position == null || (position - _position.Value).LengthSquared >= 4)
        { _position = position; _lastMovement = now; }
        return now - _lastMovement >= Math.Clamp(delay, .5, 30);
    }
}
