using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dekotube.Services;

/// <summary>
/// CPU fallback for 2D normal-map lighting. It is deliberately isolated from
/// the scene so it can later be replaced by a PixelShader without changing the profile format.
/// </summary>
public static class NormalMapLighting
{
    public static BitmapSource? Apply(BitmapSource diffuse, BitmapSource normal, LightingOptions options)
    {
        if (diffuse.PixelWidth != normal.PixelWidth || diffuse.PixelHeight != normal.PixelHeight) return null;
        var color = new FormatConvertedBitmap(diffuse, PixelFormats.Bgra32, null, 0);
        var map = new FormatConvertedBitmap(normal, PixelFormats.Bgra32, null, 0);
        int stride = color.PixelWidth * 4, count = stride * color.PixelHeight;
        var source = new byte[count]; var normals = new byte[count]; var output = new byte[count];
        color.CopyPixels(source, stride, 0); map.CopyPixels(normals, stride, 0);
        double lx = Math.Clamp(options.LightX, 0, 1) * color.PixelWidth;
        double ly = Math.Clamp(options.LightY, 0, 1) * color.PixelHeight;
        double radius = Math.Clamp(options.Radius,.01,2) * Math.Max(color.PixelWidth, color.PixelHeight);
        double height = Math.Max(.01, options.LightHeight) * radius;
        double intensity = Math.Max(0, options.Intensity);
        Color tint=Colors.White;
        try { tint=(Color)ColorConverter.ConvertFromString(options.Color); } catch { }
        for (int y = 0; y < color.PixelHeight; y++) for (int x = 0; x < color.PixelWidth; x++)
        {
            int i = y * stride + x * 4;
            double nx = normals[i + 2] / 127.5 - 1, ny = normals[i + 1] / 127.5 - 1, nz = normals[i] / 127.5 - 1;
            double length=Math.Max(.001,Math.Sqrt(nx*nx+ny*ny+nz*nz));
            nx/=length;ny=-ny/length;nz/=length;
            double dx = lx - x, dy = ly - y, distance = Math.Sqrt(dx * dx + dy * dy + height * height);
            double diffuseLight = Math.Max(0, (nx * dx + ny * dy + nz * height) / Math.Max(1, distance));
            double falloff = Math.Max(0, 1 - Math.Sqrt(dx * dx + dy * dy) / radius);
            double factor = Math.Clamp(.35 + diffuseLight * falloff * intensity, 0, 2);
            output[i] = (byte)Math.Clamp(source[i] * factor * tint.B/255, 0, 255);
            output[i + 1] = (byte)Math.Clamp(source[i + 1] * factor * tint.G/255, 0, 255);
            output[i + 2] = (byte)Math.Clamp(source[i + 2] * factor * tint.R/255, 0, 255);
            output[i + 3] = source[i + 3];
        }
        var result = new WriteableBitmap(color.PixelWidth, color.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
        result.WritePixels(new System.Windows.Int32Rect(0, 0, color.PixelWidth, color.PixelHeight), output, stride, 0);
        result.Freeze();
        return result;
    }
}
