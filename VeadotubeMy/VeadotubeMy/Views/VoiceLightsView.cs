using System.Windows;
using System.Windows.Controls;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public sealed class VoiceLightsView:UserControl
{
    public VoiceLightsView(AvatarEmotion character,AvatarView scene,Action save)
    {
        var o=character.VoiceLights;var panel=new StackPanel();Content=panel;
        var settings=new StackPanel();panel.Children.Add(settings);
        void Changed(){scene.RefreshVoiceLights();scene.UpdateRenderingSubscription();save();}
        var enabled=new CheckBox{Content="Включить индикаторы голоса",IsChecked=o.Enabled};settings.Children.Add(enabled);enabled.Click+=(_,_)=>{o.Enabled=enabled.IsChecked==true;Changed();};
        settings.Children.Add(EditorControls.Label("Шесть огоньков поверх точек на корпусе: слева и справа загораются 1 → 2 → 3 по громкости. PNG вырезать не нужно. Положение задаётся на голове, независимо от области экрана."));
        string[] colors=["#20EFFF","#4080FF","#BC60FF","#FF6030","#60FF80","#FFFFFF"];
        string[] colorNames=["Бирюзовый","Синий","Фиолетовый","Оранжевый","Зелёный","Белый"];
        var editing=new StackPanel{Visibility=Visibility.Collapsed};panel.Children.Add(editing);
        editing.Children.Add(EditorControls.Label("Выберите точку на модели или в списке. Стрелки — точное перемещение, Shift + стрелки — крупный шаг. Остальные точки приглушены. Размер и цвет выбранной точки меняются сразу."));
        bool syncing=false;
        var selected=new ComboBox{ItemsSource=new[]{"Л1","Л2","Л3","П1","П2","П3"},SelectedIndex=0};editing.Children.Add(selected);
        selected.SelectionChanged+=(_,_)=>{if(!syncing&&selected.SelectedIndex>=0)scene.SelectVoiceLight(selected.SelectedIndex);};
        var arrows=new System.Windows.Controls.Primitives.UniformGrid{Columns=4};editing.Children.Add(arrows);
        EditorControls.Button(arrows,"←",()=>scene.NudgeVoiceLight(-1,0));EditorControls.Button(arrows,"↑",()=>scene.NudgeVoiceLight(0,-1));EditorControls.Button(arrows,"↓",()=>scene.NudgeVoiceLight(0,1));EditorControls.Button(arrows,"→",()=>scene.NudgeVoiceLight(1,0));
        var pointSize=EditorControls.Slider(editing,"Размер выбранной точки (%)",100,25,400,v=>{if(!syncing)scene.ResizeVoiceLight(v/100);},"0");
        editing.Children.Add(EditorControls.Label("Цвет выбранной точки"));
        var pointColor=new ComboBox{ItemsSource=colorNames,SelectedIndex=0};editing.Children.Add(pointColor);pointColor.SelectionChanged+=(_,_)=>{if(!syncing&&pointColor.SelectedIndex>=0)scene.ColorVoiceLight(colors[pointColor.SelectedIndex]);};
        void SyncSelection(object? sender,EventArgs e){syncing=true;selected.SelectedIndex=scene.SelectedVoiceLight;pointSize.Value=scene.SelectedVoiceLightSize*100;pointColor.SelectedIndex=Array.IndexOf(colors,scene.SelectedVoiceLightColor);syncing=false;}
        Loaded+=(_,_)=>scene.VoiceLightSelectionChanged+=SyncSelection;
        void End(bool apply){scene.EndVoiceLightsPlacement(apply);editing.Visibility=Visibility.Collapsed;settings.IsEnabled=true;}
        EditorControls.Button(editing,"Применить",()=>End(true));EditorControls.Button(editing,"Отмена",()=>End(false));
        EditorControls.Button(settings,"✥ Разместить шесть точек",()=>{scene.BeginVoiceLightsPlacement(save);SyncSelection(null,EventArgs.Empty);settings.IsEnabled=false;editing.Visibility=Visibility.Visible;editing.BringIntoView();});
        EditorControls.Slider(settings,"Общий размер (% ширины холста)",o.Diameter*100,.3,6,v=>{o.Diameter=v/100;Changed();},"0.0");
        EditorControls.Slider(settings,"Чувствительность",o.Gain,.5,20,v=>{o.Gain=v;Changed();},"0.0");
        EditorControls.Slider(settings,"Яркость (%)",o.Brightness*100,0,100,v=>{o.Brightness=v/100;Changed();},"0");
        EditorControls.Slider(settings,"Свечение",o.Glow,0,30,v=>{o.Glow=v;Changed();},"0");

        var choice=new ComboBox{ItemsSource=new[]{"Бирюзовый","Синий","Фиолетовый","Оранжевый","Зелёный","Белый"},SelectedIndex=Array.IndexOf(colors,o.Color)};settings.Children.Add(choice);choice.SelectionChanged+=(_,_)=>{if(choice.SelectedIndex>=0){o.Color=colors[choice.SelectedIndex];Changed();}};
        EditorControls.Button(settings,"▶ Тест без голоса · 6 секунд",scene.TestVoiceLights);EditorControls.Button(settings,"Остановить тест",scene.StopVoiceLightsTest);
        Unloaded+=(_,_)=>{scene.VoiceLightSelectionChanged-=SyncSelection;scene.EndVoiceLightsPlacement(false);scene.StopVoiceLightsTest();};
    }
}
