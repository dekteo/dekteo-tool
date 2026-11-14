using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

internal static class DigitalBlinkChecks
{
    public static void Run(Action<bool,string> check)
    {
        var p = new PupilOptions { BlinkStyle = "digital", DigitalBlinkMask = true };
        var copy = p.Copy(); copy.BlinkStyle = "images";
        check(p.BlinkStyle == "digital" && copy.BlinkStyle == "images", "Blink style persists independently");
        var scene = new AvatarView(); var c = new AvatarEmotion(); c.Pupils.Enabled = true; c.Pupils.BlinkStyle = "digital"; c.Pupils.DigitalBlinkMask = true; scene.ConfigureCharacter(c);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var left = (Canvas)scene.FindName("LeftEyeClip"); left.Width=100; left.Height=80;
        var method=typeof(AvatarView).GetMethod("ApplyDigitalBlink",flags)!;
        typeof(AvatarView).GetField("_pupilEyesClosed",flags)!.SetValue(scene,true);
        method.Invoke(scene,new object[]{left, scene.FindName("LeftPupilOverlay"), 1, .08, 150, true});
        var top=(Rectangle)typeof(AvatarView).GetField("_leftDigitalTop",flags)!.GetValue(scene)!;
        check(top.Visibility==Visibility.Collapsed, "Digital blink does not add a rectangular mask");
        var scale=(ScaleTransform)scene.FindName("LeftPupilBlinkScale");
        check(scale.ScaleY<1, "Digital blink smoothly compresses pupil");
        var half=(Grid)scene.FindName("LeftHalfEye"); var closed=(Grid)scene.FindName("LeftClosedEye");
        check(half.Visibility == Visibility.Collapsed && closed.Visibility == Visibility.Collapsed, "Digital blink does not show PNG eyelid layers");
    }
}
