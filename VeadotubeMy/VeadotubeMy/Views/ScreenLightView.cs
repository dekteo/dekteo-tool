using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public sealed class ScreenLightView : UserControl
{
    public ScreenLightView(AvatarEmotion character,AvatarView scene,Action save)
    {
        var o=character.ScreenLight??=new();var panel=new StackPanel{Margin=new Thickness(12)};Content=panel;
        void Changed(){scene.RefreshScreenLight();save();}
        var enabled=new CheckBox{Content="Свет от экрана",IsChecked=o.Enabled,Margin=new Thickness(0,6,0,8)};panel.Children.Add(enabled);enabled.Click+=(_,_)=>{o.Enabled=enabled.IsChecked==true;Changed();};
        panel.Children.Add(EditorControls.Label("Цветная подсветка из левой и правой половины области. Выберите игру без самого персонажа. Работает локально, изображения не сохраняются. Для игры предпочтителен оконный режим без рамки."));
        var source=EditorControls.Section(panel,"Источник света",true);var region=EditorControls.Label("");source.Children.Add(region);
        void RegionText()=>region.Text=o.Width>0?$"Область: {o.X}, {o.Y} · {o.Width}×{o.Height}":"Область не выбрана";
        void Set(ScreenLightCapture.Rect r){o.X=r.Left;o.Y=r.Top;o.Width=r.Right-r.Left;o.Height=r.Bottom-r.Top;RegionText();Changed();}
        source.Children.Add(EditorControls.Label("Весь экран — выберите монитор:"));
        var monitors=ScreenLightCapture.Monitors();var monitor=new ComboBox{ItemsSource=monitors.Select((r,i)=>$"Экран {i+1} · {r.Right-r.Left}×{r.Bottom-r.Top}").ToArray(),SelectedIndex=-1};source.Children.Add(monitor);
        monitor.SelectionChanged+=(_,_)=>{if(monitor.SelectedIndex>=0)Set(monitors[monitor.SelectedIndex]);};
        EditorControls.Button(source,"Выбрать область мышью",()=>{var picker=new ScreenAreaPicker();if(picker.ShowDialog()==true&&picker.Selection is {} r)Set(r);});RegionText();
        var settings=EditorControls.Section(panel,"Подсветка",true);
        var protect=new CheckBox{Content="Сохранять яркость экрана персонажа",IsChecked=o.ProtectScreen};settings.Children.Add(protect);protect.Click+=(_,_)=>{o.ProtectScreen=protect.IsChecked==true;Changed();};
        settings.Children.Add(EditorControls.Label("Исключает из подсветки и затемнения область из «Эффектов экрана», вместе с глазами и ртом внутри неё. Корпус реагирует на игру. Форму области настройте в эффектах экрана; включать блик не нужно."));
        var darken=new CheckBox{Content="Затемнять в тёмных сценах",IsChecked=o.Darken};settings.Children.Add(darken);darken.Click+=(_,_)=>{o.Darken=darken.IsChecked==true;Changed();};
        EditorControls.Slider(settings,"Максимальное затемнение (%)",o.DarkenStrength*100,0,90,v=>{o.DarkenStrength=v/100;Changed();},"0");
        var target=new ComboBox{ItemsSource=new[]{"Весь персонаж","Основа / голова","Тело"},SelectedIndex=o.Target=="head"?1:o.Target=="body"?2:0};settings.Children.Add(target);target.SelectionChanged+=(_,_)=>{o.Target=new[]{"all","head","body"}[target.SelectedIndex];Changed();};
        EditorControls.Slider(settings,"Сила и предел подсветки (%)",o.Strength*100,0,100,v=>{o.Strength=v/100;Changed();},"0");
        EditorControls.Slider(settings,"Плавность (сек.)",o.SmoothSeconds,.05,3,v=>{o.SmoothSeconds=v;Changed();},"0.00");
        var fps=new ComboBox{ItemsSource=new[]{2,5,10,15},SelectedItem=o.Fps};settings.Children.Add(EditorControls.Label("Обновлений захвата в секунду"));settings.Children.Add(fps);fps.SelectionChanged+=(_,_)=>{if(fps.SelectedItem is int value){o.Fps=value;Changed();}};
        panel.Children.Add(EditorControls.Label("Цвет слева                                      Цвет справа"));
        var swatches=new UniformGrid{Columns=2,Height=28};var left=new Border{BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)};var right=new Border{BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)};swatches.Children.Add(left);swatches.Children.Add(right);panel.Children.Add(swatches);
        var status=EditorControls.Label("");panel.Children.Add(status);
        void Update(object? sender,EventArgs e){status.Text=scene.ScreenLightStatus;var colors=scene.ScreenLightColors;left.Background=new SolidColorBrush(colors.Left);right.Background=new SolidColorBrush(colors.Right);}
        Loaded+=(_,_)=>{scene.ScreenLightChanged+=Update;Update(null,EventArgs.Empty);};Unloaded+=(_,_)=>scene.ScreenLightChanged-=Update;
    }
}
