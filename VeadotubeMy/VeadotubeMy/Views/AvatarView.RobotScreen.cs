using VeadotubeMy.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace VeadotubeMy.Views;
public partial class AvatarView
{
    private readonly Grid _faceContent=new();
    private readonly Grid _screenSurface=new(){IsHitTestVisible=false};
    private VisualBrush? _faceBrush;
    private void InitializeScreenSurface()
    {
        Composite.Children.Remove(ScreenEffectCanvas);
        foreach(UIElement child in Composite.Children.Cast<UIElement>().ToArray()){Composite.Children.Remove(child);_faceContent.Children.Add(child);}
        Composite.Children.Add(_faceContent);Composite.Children.Add(_screenSurface);Panel.SetZIndex(_screenSurface,20);
        _screenSurface.Children.Add(ScreenEffectCanvas);_screenSurface.Children.Add(_robotVisual);Panel.SetZIndex(_robotVisual,21);
        _faceBrush=new VisualBrush(_faceContent){ViewboxUnits=BrushMappingMode.Absolute,ViewportUnits=BrushMappingMode.Absolute,Stretch=Stretch.Fill};
        Artwork.SizeChanged+=(_,_)=>UpdateFaceBrush();UpdateFaceBrush();
    }
    private void UpdateFaceBrush(){if(_faceBrush!=null){var bounds=new Rect(0,0,Artwork.Width,Artwork.Height);_faceBrush.Viewbox=bounds;_faceBrush.Viewport=bounds;}}
    private readonly RobotScreenMotion _robotMotion=new();
    private readonly RobotScreenVisual _robotVisual=new(){IsHitTestVisible=false};
    public void TestRobotScreen(string kind){_robotMotion.Test(kind);UpdateRenderingSubscription();}
    public void StopRobotScreenTest(){_robotMotion.Reset();_robotVisual.Animate(_robotMotion,0);UpdateRenderingSubscription();}
    private bool RobotScreenActive=>_character!=null&&(_character.RobotScreen.Processing||_character.RobotScreen.Glitch||_character.RobotScreen.VoiceBars||_robotMotion.Testing||_robotMotion.Glitch>0||_robotMotion.Dots>0||_robotMotion.Voice>0);
    private void RenderRobotScreen(double dt,double time)
    {
        if(!RobotScreenActive){_robotVisual.Animate(_robotMotion,time);return;}
        _robotVisual.Configure(Artwork.Width,Artwork.Height,_character!.ScreenEffect,StateImage.Source,_robotDraft??_character.RobotScreen,_faceBrush);
        if(_editingEye!=null||_screenEditing||IsMouthCurveEditing||IsAccessoryEditing){_robotMotion.Reset();}
        else _robotMotion.Step(dt,time,_character.RobotScreen,AvatarService.Instance.IsTalking,_mouthInputLevel/100.0);
        _robotVisual.Animate(_robotMotion,time);
    }
}
