using System.Windows;
using System.Windows.Media;
namespace VeadotubeMy.Services;

public static class EyePerspective
{
    public static Matrix Plane(EyeRegion eye, double width, double height, bool editing=false)
    {
        var m=Matrix.Identity;
        if(eye.PerspectiveEnabled && !editing)
            m.ScaleAt(Math.Cos(Math.Clamp(eye.PerspectiveYaw,-60,60)*Math.PI/180),1,width/2,height/2);
        m.RotateAt(eye.RotationDegrees+(eye.PerspectiveEnabled&&!editing?Math.Clamp(eye.PerspectiveSlope,-45,45):0),width/2,height/2);
        return m;
    }
    public static Vector Direction(EyeRegion eye, Vector gaze)
    {
        if(eye.PerspectiveEnabled){
            double depth=1+Math.Clamp(gaze.X,-1,1)*Math.Sin(Math.Clamp(eye.PerspectiveYaw,-60,60)*Math.PI/180)*.2;
            return gaze*(Math.Clamp(eye.PerspectiveTravel,0,1)/depth);
        }
        double r=-eye.RotationDegrees*Math.PI/180;
        return new(gaze.X*Math.Cos(r)-gaze.Y*Math.Sin(r),gaze.X*Math.Sin(r)+gaze.Y*Math.Cos(r));
    }
}
