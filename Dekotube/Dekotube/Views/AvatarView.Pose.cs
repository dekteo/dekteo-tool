using Dekotube.Services;
namespace Dekotube.Views;
public partial class AvatarView
{
    private readonly PoseMotion _poseMotion=new();
    public void PreviewPose(){_poseMotion.Preview();UpdateRenderingSubscription();}
    public void StopPosePreview(){_poseMotion.CancelPreview();UpdateRenderingSubscription();}
    private void RenderPose(double dt,bool headActive)
    {
        var h=_character!.Head;
        var pose=_poseMotion.Step(_editingEye!=null?0:dt,h.PoseEnabled && _previewEffect==null,h.PoseAmount);
        PoseScale.ScaleX=PoseScale.ScaleY=1+pose.Body*.018;
        PoseTranslation.Y=-pose.Body*3;
        if(headActive) HeadTranslation.Y-=pose.Head*5;
    }
}
