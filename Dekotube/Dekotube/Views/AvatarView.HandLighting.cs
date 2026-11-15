using System.Windows.Media.Imaging;
using Dekotube.Services;
namespace Dekotube.Views;

public partial class AvatarView
{
    private readonly BitmapSource?[] _handsShownSources=new BitmapSource?[5];
    private BitmapSource? HandLitImage(HandsRig rig,int index)
    {
        var source=_handsCache.GetValueOrDefault(rig.Layers.ElementAtOrDefault(index)??"") as BitmapSource;
        var path=rig.NormalPath(index);
        if(source==null||_character?.Lighting.Enabled!=true||_character.Lighting.LightTarget=="head"||string.IsNullOrWhiteSpace(path))return source;
        return _litCache.GetValueOrDefault((source,path))??source;
    }
    private void UpdateHandLighting()
    {
        if(_character==null)return;
        var rig=_handsDraft??RuntimeRig;
        for(int i=0;i<5;i++){
            var source=HandLitImage(rig,i);
            if(ReferenceEquals(source,_handsShownSources[i]))continue;
            _handsShownSources[i]=source;
            int side=rig.WholeHands?i-(rig.Layers.Count==3?1:3):-1;
            if(side is 0 or 1 && source!=null && _handWarps[side]!=null){
                _handWarps[side]!.ReplaceSource(source);_handsImages[i].Source=_handWarps[side]!.Image;
            }else _handsImages[i].Source=source;
        }
    }
}
