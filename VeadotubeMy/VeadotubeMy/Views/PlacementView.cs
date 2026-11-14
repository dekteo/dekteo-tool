using System.Windows;
using System.Windows.Controls;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views;

public sealed class PlacementView : UserControl
{
    public PlacementView(AvatarEmotion character, AvatarView scene, Action save, bool initiallyHead = false, bool body=false)
    {
        var snapshot=(character.SceneX,character.SceneY,character.SceneScale,character.HeadX,character.HeadY,character.HeadScale,character.BodyX,character.BodyY,character.BodyScale);
        var persist=save; save=()=>{};
        bool applied=false;
        void Restore(){(character.SceneX,character.SceneY,character.SceneScale,character.HeadX,character.HeadY,character.HeadScale,character.BodyX,character.BodyY,character.BodyScale)=snapshot;scene.RefreshPlacement();}
        var panel = new StackPanel { Margin = new Thickness(12) }; Content = panel;
        panel.Children.Add(EditorControls.Label("Перетаскивайте мышью. Колесо — размер."));
        var modes = new ComboBox { Margin = new Thickness(0, 8, 0, 8) };
        modes.Items.Add(new ComboBoxItem { Content = "Весь персонаж" });
        modes.Items.Add(new ComboBoxItem { Content = "Только голова", IsEnabled = character.Head.Enabled && !string.IsNullOrWhiteSpace(character.Head.BodyImagePath) });
        modes.SelectedIndex = initiallyHead ? 1 : 0;
        double Size()=>initiallyHead?character.HeadScale:body?character.BodyScale:character.SceneScale;
        bool syncing = false;
        var scale = EditorControls.Slider(panel, "Размер (%)", Size()*100, 10, 400, v =>
        {
            if (syncing) return;
            if(initiallyHead) character.HeadScale=v/100; else if(body)character.BodyScale=v/100;else character.SceneScale=v/100; scene.RefreshPlacement(); save();
        }, "0");
        scale.IsEnabled=true;
        void Updated(object? sender, EventArgs e) { syncing = true; scale.Value = Size()*100; syncing = false; }
        EditorControls.Button(panel, "Сбросить", () =>
        {
            if (modes.SelectedIndex == 1) { character.HeadX = character.HeadY = 0; character.HeadScale=1; }
            else if(body){character.BodyX=character.BodyY=0;character.BodyScale=1;}
            else { character.SceneX = character.SceneY = 0; character.SceneScale = 1; }
            scene.RefreshPlacement(); save();
        });
        EditorControls.Button(panel, "Применить", () => {scene.EndPlacement();applied=true;persist();IsEnabled=false;});
        EditorControls.Button(panel, "Отмена", () => {scene.EndPlacement();Restore();applied=true;IsEnabled=false;});
        Loaded += (_, _) => { scene.PlacementChanged += Updated; scene.BeginPlacement(initiallyHead, save, body); };
        Unloaded += (_, _) => { scene.PlacementChanged -= Updated; scene.EndPlacement(); if(!applied)Restore(); };
    }
}
