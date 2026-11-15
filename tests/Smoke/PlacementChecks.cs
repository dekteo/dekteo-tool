using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Dekotube.Services;
using Dekotube.Views;

internal static class PlacementChecks
{
    public static void Run(Action<bool, string> check)
    {
        var character = new AvatarEmotion { SceneX = .2, SceneY = -.1, SceneScale = 1.6, HeadX = .1, HeadY = .05 };
        character.Head.Enabled = true; character.Head.BodyImagePath = "test.png";
        var restored = JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        var copy = character.Duplicate();
        check(restored.SceneScale == 1.6 && restored.HeadY == .05 && copy.SceneX == .2 && copy.HeadX == .1, "Placement survives serialization and character duplication");
        var scene = new AvatarView(); scene.ConfigureCharacter(character);
        scene.Measure(new Size(1000, 800)); scene.Arrange(new Rect(0, 0, 1000, 800)); scene.RefreshPlacement();
        var translation = (TranslateTransform)scene.FindName("PlacementTranslation");
        var head = (TranslateTransform)scene.FindName("HeadPlacement");
        var scale = (ScaleTransform)scene.FindName("PlacementScale");
        check(translation.X == 200 && translation.Y == -80 && scale.ScaleX == 1.6 && head.X == 100, "Scene and head have independent transforms");
        bool saved = false;
        scene.BeginPlacement(true, () => saved = true);
        scene.GetType().GetMethod("RenderFrame", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, null);
        check(head.X == 100 && translation.X == 200, "Animation does not overwrite manual placement");
        scene.EndPlacement(); check(saved, "Placement is saved on completion");
        var editor=new PlacementView(character,scene,()=>{},true);
        var panel=(System.Windows.Controls.StackPanel)editor.Content;
        check(!panel.Children.OfType<System.Windows.Controls.ComboBox>().Any(),"Layer placement has no redundant target selector");
        panel.Children.OfType<System.Windows.Controls.Slider>().Single().Value=77;
        check(character.HeadScale==.77&&character.SceneScale==1.6,"Head slider leaves scene scale unchanged");
        panel.Children.OfType<System.Windows.Controls.Button>().Single(b=>b.Content?.ToString()=="Отмена").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        check(character.HeadScale==1&&character.HeadX==.1,"Cancel restores head size and position");
        scene.ConfigureCharacter(new AvatarEmotion());
        check(translation.X == 0 && head.X == 0 && scale.ScaleX == 1, "Another character restores its own placement defaults");
    }
}
