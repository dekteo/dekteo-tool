using System.IO;
using System.Windows.Media.Imaging;
namespace VeadotubeMy.Services;

public static class PngFile
{
    // Explicit imports must read the current bytes, not WPF's URI cache.
    public static BitmapImage Read(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
        image.StreamSource=stream;image.EndInit();image.Freeze();return image;
    }
}
