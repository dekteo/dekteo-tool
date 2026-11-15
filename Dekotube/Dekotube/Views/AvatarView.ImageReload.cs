using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Dekotube.Views;

public partial class AvatarView
{
    // Called only by explicit file selection/reload, never by the render loop.
    // Invalidate derived images as well as filename guards, preserving calibration.
    public void ReloadImageFiles()
    {
        _clothingCache.Clear();
        _lightVersion++;_lightFiles.Clear();_litCache.Clear();_lightPending=true;
        _handsCache.Clear();_accessoryFiles.Clear();
        _leftPath=_rightPath=_leftClosedPath=_rightClosedPath=_leftMaskPath=_rightMaskPath=null;
        void ClearTags(DependencyObject node)
        {
            if(node is Image image)image.Tag=null;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)ClearTags(VisualTreeHelper.GetChild(node,i));
        }
        ClearTags(Artwork);
        _handsFade=1;_handsPrevious.Opacity=0;
        _eyeLayoutDirty=true;
        RefreshVisualSettings();
        RefreshLighting();
    }
}
