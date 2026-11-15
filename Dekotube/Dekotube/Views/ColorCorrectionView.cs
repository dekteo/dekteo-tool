using System.Windows;
using System.Windows.Controls;
using Dekotube.Services;

namespace Dekotube.Views;

public sealed class ColorCorrectionView : UserControl
{
    public ColorCorrectionView(AvatarEmotion character, AvatarView scene, Action save)
    {
        character.ColorCorrections ??= new();
        var panel=new StackPanel { Margin=new Thickness(12) }; Content=panel;
        panel.Children.Add(EditorControls.Label("Выберите слой. Общая коррекция применяется поверх настроек отдельных частей. Исходные PNG не изменяются."));
        var targets=new List<(string Key,string Name)> { ("all","Персонаж целиком"),("base",character.Head.Enabled?"Голова · основа":"Основа персонажа") };
        if(character.Head.Enabled)targets.Add(("body","Тело"));
        if(character.MouthLayer.Enabled)targets.Add(("mouth","Отдельный рот"));
        if(character.Pupils.Enabled)targets.AddRange(new[]{("leftPupil","Левый зрачок"),("rightPupil","Правый зрачок"),("leftWhite","Левый белок"),("rightWhite","Правый белок"),("leftLids","Левые веки"),("rightLids","Правые веки")});
        var selector=new ComboBox { ItemsSource=targets.Select(t=>t.Name).ToArray(), SelectedIndex=0 };panel.Children.Add(selector);
        var content=new StackPanel();panel.Children.Add(content);
        void Build()
        {
            content.Children.Clear();
            string key=targets[selector.SelectedIndex].Key;
            if(!character.ColorCorrections.TryGetValue(key,out var o))character.ColorCorrections[key]=o=new();
            void Changed(){scene.RefreshColorCorrection();save();}
            var enabled=new CheckBox {Content="Применять коррекцию",IsChecked=o.Enabled,Margin=new Thickness(0,12,0,8)};content.Children.Add(enabled);
            var controls=EditorControls.Section(content,"Яркость и цвет",true);controls.IsEnabled=o.Enabled;
            enabled.Checked+=(_,_)=>{o.Enabled=true;controls.IsEnabled=true;Changed();};
            enabled.Unchecked+=(_,_)=>{o.Enabled=false;controls.IsEnabled=false;Changed();};
            EditorControls.Slider(controls,"Яркость (%)",o.Brightness*100,-100,100,v=>{o.Brightness=v/100;Changed();},"0");
            EditorControls.Slider(controls,"Контрастность (%)",o.Contrast*100,0,300,v=>{o.Contrast=v/100;Changed();},"0");
            EditorControls.Slider(controls,"Гамма",o.Gamma,.2,3,v=>{o.Gamma=v;Changed();},"0.00");
            EditorControls.Slider(controls,"Сдвиг оттенка (°)",o.Hue,-180,180,v=>{o.Hue=v;Changed();},"0");
            EditorControls.Button(content,"Сбросить выбранный слой",()=>{character.ColorCorrections[key]=new();Changed();Build();});
        }
        selector.SelectionChanged+=(_,_)=>Build();Build();
    }
}
