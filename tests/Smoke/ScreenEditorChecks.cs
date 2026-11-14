using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

internal static class ScreenEditorChecks
{
    public static void Run(Action<bool,string> check)
    {
        var c = new AvatarEmotion(); c.ScreenEffect.Enabled = true;
        var original = c.ScreenEffect.Copy();
        var scene = new AvatarView(); scene.ConfigureCharacter(c);
        int saves = 0;
        scene.BeginScreenRegionEditing(()=>saves++);
        c.ScreenEffect.X = .3; c.ScreenEffect.Width = .35;
        c.ScreenEffect.CornerRadius = 65; c.ScreenEffect.SkewX = 8;
        scene.RefreshScreenEditor();
        var outline = (System.Windows.Shapes.Path)scene.FindName("ScreenRegionOutline");
        var effect = (Canvas)scene.FindName("ScreenEffectCanvas");
        check(outline.Width == 350 && Canvas.GetLeft(outline) == 300 && !outline.Data.FillContains(new Point(1,1)), "Preview immediately reflects size, position and rounding while paused");
        check(((SkewTransform)outline.RenderTransform).AngleX == 8 && ((SkewTransform)effect.RenderTransform).AngleX == 8, "Preview and rendered screen share skew immediately");
        var actions=(StackPanel)scene.FindName("ScreenEditorActions");
        check(actions.Visibility==Visibility.Visible, "Apply and Cancel are shown above the scene");
        c.ScreenEffect.CurveTop=.2; c.ScreenEffect.CurveRight=-.2; scene.RefreshScreenEditor();
        check(outline.Data.FillContains(new Point(175,-10)) && !outline.Data.FillContains(new Point(345,outline.Height/2)), "Outward top and inward right curves change the filled zone");
        var shapeBefore=effect.Clip;
        scene.RefreshScreenEditor();
        check(ReferenceEquals(shapeBefore,effect.Clip), "Unchanged screen geometry is reused");
        ((Button)actions.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!scene.IsScreenEditing && c.ScreenEffect.X==original.X && c.ScreenEffect.Width==original.Width && c.ScreenEffect.CornerRadius==original.CornerRadius && c.ScreenEffect.SkewX==original.SkewX, "Cancel restores geometry and deformation");
        check(c.ScreenEffect.CurveTop==0 && c.ScreenEffect.CurveRight==0,"Cancel also restores side curvature");
        scene.BeginScreenRegionEditing(()=>saves++);
        c.ScreenEffect.Width=.42; c.ScreenEffect.SkewY=-6; scene.RefreshScreenEditor();
        ((Button)actions.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!scene.IsScreenEditing && c.ScreenEffect.Width==.42 && c.ScreenEffect.SkewY==-6 && saves==2, "Apply retains edits and commits once");
        scene.BeginScreenRegionEditing(()=>saves++);
        c.ScreenEffect.Width=.2; scene.RefreshScreenEditor();
        scene.ConfigureCharacter(new AvatarEmotion());
        check(c.ScreenEffect.Width==.42 && actions.Visibility==Visibility.Collapsed, "Character switch cancels unfinished screen editing");
    }
}
