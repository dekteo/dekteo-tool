using System.Windows;

namespace VeadotubeMy.Services;

public static class EyeRegionGeometry
{
    public static Rect Bounds(EyeRegion eye) => new(eye.X - eye.Width / 2, eye.Y - eye.Height / 2, eye.Width, eye.Height);
    public static Rect Move(Rect initial, Vector delta) => new(
        Math.Clamp(initial.X + delta.X, 0, Math.Max(0, 1 - initial.Width)),
        Math.Clamp(initial.Y + delta.Y, 0, Math.Max(0, 1 - initial.Height)), initial.Width, initial.Height);
    // Handle indices go clockwise from the top-left: NW, N, NE, E, SE, S, SW, W.
    public static Rect Resize(Rect initial, Point point, int handle)
    {
        double left = initial.Left, right = initial.Right, top = initial.Top, bottom = initial.Bottom;
        if (handle is 0 or 6 or 7) left = Math.Clamp(point.X, 0, right - .005);
        if (handle is 2 or 3 or 4) right = Math.Clamp(point.X, left + .005, 1);
        if (handle is 0 or 1 or 2) top = Math.Clamp(point.Y, 0, bottom - .005);
        if (handle is 4 or 5 or 6) bottom = Math.Clamp(point.Y, top + .005, 1);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }
    public static Rect Draw(Point start, Point end)
    {
        double left = Math.Clamp(Math.Min(start.X, end.X), 0, .995);
        double top = Math.Clamp(Math.Min(start.Y, end.Y), 0, .995);
        return new Rect(left, top, Math.Clamp(Math.Abs(end.X - start.X), .005, 1 - left),
            Math.Clamp(Math.Abs(end.Y - start.Y), .005, 1 - top));
    }
    public static void Assign(EyeRegion eye, Rect bounds)
    {
        eye.X = bounds.X + bounds.Width / 2; eye.Y = bounds.Y + bounds.Height / 2;
        eye.Width = bounds.Width; eye.Height = bounds.Height;
    }
}
