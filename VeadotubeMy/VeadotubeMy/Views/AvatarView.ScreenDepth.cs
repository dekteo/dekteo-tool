using System.Windows.Media;
namespace VeadotubeMy.Views;
public partial class AvatarView
{
    private readonly TranslateTransform _depthEyes=new(),_depthMouth=new(),_depthGlass=new();
    private double _depthOffset;
    private double _depthTest;
    public void TestScreenDepth(){_depthTest=6;UpdateRenderingSubscription();}
    public void StopScreenDepthTest(){_depthTest=0;_depthOffset=0;_depthEyes.X=_depthMouth.X=_depthGlass.X=0;UpdateRenderingSubscription();}
    private void InitializeScreenDepth(){PupilCanvas.RenderTransform=_depthEyes;_mouthCanvas.RenderTransform=_depthMouth;ScreenReflection.RenderTransform=_depthGlass;}
    private void RenderScreenDepth(double dt)
    {
        var s=_character!.ScreenEffect;
        bool active=(s.DepthEnabled || _depthTest>0) && _editingEye==null && !IsMouthCurveEditing && !_placingMouth && !IsAccessoryEditing && !_screenEditing;
        double direction=Math.Clamp(_perspectiveYaw/18+HeadRotation.Angle/24,-1,1);
        if(_depthTest>0){_depthTest=Math.Max(0,_depthTest-dt);direction=Math.Sin((6-_depthTest)*Math.PI/3);}
        double target=active?direction*Math.Clamp(s.DepthAmount,0,16):0;
        _depthOffset=active?_depthOffset+(target-_depthOffset)*(1-Math.Exp(-dt/.12)):0;
        _depthEyes.X=_depthMouth.X=-_depthOffset;_depthGlass.X=_depthOffset*.6;
    }
}
