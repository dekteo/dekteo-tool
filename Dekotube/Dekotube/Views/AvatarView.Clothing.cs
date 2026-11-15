using System.Windows.Media.Imaging;
using Dekotube.Services;
namespace Dekotube.Views;
public partial class AvatarView
{
    private readonly Dictionary<(string,BitmapSource),BitmapSource> _clothingCache=new();
    private BitmapSource? Clothed(string path,BitmapSource? image)
    {
        var o=_character?.Clothing;
        if(image==null||o?.Enabled!=true||!o.Current.Layers.TryGetValue(path,out var layer))return image;
        if(_clothingCache.TryGetValue((path,image),out var cached))return cached;
        return _clothingCache[(path,image)]=ClothingRenderer.Apply(image,o.Current,layer,LoadImage(layer.PrintPath));
    }
    public void RefreshClothing()
    {
        _clothingCache.Clear();_handsCache.Clear();_litCache.Clear();_lightVersion++;
        RefreshVisualSettings();RefreshLighting();
    }
}
