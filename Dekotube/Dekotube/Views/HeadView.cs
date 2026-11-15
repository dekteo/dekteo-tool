using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Dekotube.Services;

namespace Dekotube.Views;

public sealed class HeadView : UserControl
{
    public HeadView(AvatarEmotion character, AvatarView scene, Action save, bool imagesInSidebar = false)
    {
        var panel = new StackPanel { Margin = new Thickness(12) }; Content = panel;
        var h = character.Head;
        void Changed() { scene.RefreshVisualSettings(); save(); }
        panel.Children.Add(EditorControls.Label("Голова и тело — отдельные PNG на одинаковом прозрачном холсте. Загрузите их в панели «Изображения и слои». Глаза и рот двигаются вместе с головой."));
        var enabled = new CheckBox { Content = "Двигать голову отдельно", IsChecked = h.Enabled, Margin = new Thickness(0,8,0,8) };
        enabled.Checked += (_,_) => { h.Enabled = true; Changed(); };
        enabled.Unchecked += (_,_) => { h.Enabled = false; Changed(); };
        panel.Children.Add(enabled);
        var uploadPanel = new StackPanel { Visibility = imagesInSidebar ? Visibility.Collapsed : Visibility.Visible }; panel.Children.Add(uploadPanel);
        var name = EditorControls.Label(string.IsNullOrEmpty(h.BodyImagePath) ? "Тело ещё не загружено — движение выключено" : System.IO.Path.GetFileName(h.BodyImagePath));
        uploadPanel.Children.Add(name);
        EditorControls.Button(uploadPanel,"Загрузить PNG тела",()=> {
            var dialog = new OpenFileDialog { Filter = "PNG|*.png" };
            if(dialog.ShowDialog()!=true) return;
            try {
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
                image.CreateOptions=BitmapCreateOptions.IgnoreImageCache;image.UriSource=new Uri(dialog.FileName); image.EndInit();
                scene.ReloadImageFiles();
                h.BodyImagePath=dialog.FileName; name.Text=System.IO.Path.GetFileName(dialog.FileName); Changed();
            } catch { MessageBox.Show("Не удалось открыть PNG. Выберите другой файл."); }
        });
        EditorControls.Button(uploadPanel,"Убрать тело",()=>{h.BodyImagePath=""; name.Text="Тело ещё не загружено — движение выключено"; Changed();});
        var motion = EditorControls.Section(panel,"Движение головы при речи",true);
        EditorControls.Slider(motion,"Наклон головы (°)",h.Angle,0,12,v=>{h.Angle=v;Changed();},"0.0");
        EditorControls.Slider(motion,"Движение при речи (%)",h.SpeechMotion*100,0,300,v=>{h.SpeechMotion=v/100;Changed();},"0");
        EditorControls.Slider(motion,"Скорость",h.Speed,.1,3,v=>{h.Speed=v;Changed();},"0.0");
        EditorControls.Button(motion,"Проверить движение при речи",scene.PreviewSpeech);
        var gaze = EditorControls.Section(panel,"Движение головы за взглядом");
        var follow = new CheckBox { Content = "Наклонять голову вслед за взглядом", IsChecked = h.FollowGaze, Margin = new Thickness(0,8,0,4) };
        follow.Checked += (_,_) => { h.FollowGaze=true; Changed(); };
        follow.Unchecked += (_,_) => { h.FollowGaze=false; Changed(); };
        gaze.Children.Add(follow);
        gaze.Children.Add(EditorControls.Label("Работает с отдельными зрачками: голова наклоняется, когда взгляд приближается к краю области глаза."));
        EditorControls.Slider(gaze,"Наклон за взглядом (°)",h.GazeAngle,0,8,v=>{h.GazeAngle=v;Changed();},"0.0");
        EditorControls.Slider(gaze,"Начинать у края области (%)",h.GazeThreshold*100,0,95,v=>{h.GazeThreshold=v/100;Changed();},"0");
        var perspectivePanel = EditorControls.Section(panel,"Поворот влево / вправо · перспектива");
        var perspective = new CheckBox { Content="Поворачивать голову в глубину", IsChecked=h.PerspectiveEnabled, Margin=new Thickness(0,4,0,8) };
        perspective.Checked += (_,_) => { h.PerspectiveEnabled=true; Changed(); };
        perspective.Unchecked += (_,_) => { h.PerspectiveEnabled=false; Changed(); };
        perspectivePanel.Children.Add(perspective);
        perspectivePanel.Children.Add(EditorControls.Label("Для отдельной головы с PNG тела. Дальняя сторона рисунка становится меньше, глаза и рот поворачиваются вместе с ней. Это имитация объёма из плоского PNG: начните с 5–10°, чтобы избежать эффекта картонки."));
        var direction = new ComboBox { ItemsSource=new[] { "Вслед за взглядом", "Автоматически в обе стороны" }, SelectedIndex=h.PerspectiveMode=="auto"?1:0 };
        direction.SelectionChanged += (_,_)=>{h.PerspectiveMode=direction.SelectedIndex==1?"auto":"gaze";Changed();};
        perspectivePanel.Children.Add(direction);
        perspectivePanel.Children.Add(EditorControls.Label("Вслед за взглядом — нужны отдельные зрачки с подвижным взглядом. Автоматически — работает без мыши и отдельных зрачков."));
        EditorControls.Slider(perspectivePanel,"Угол поворота (°)",h.PerspectiveAngle,0,18,v=>{h.PerspectiveAngle=v;Changed();},"0.0");
        var pivot = EditorControls.Section(panel,"Точка поворота головы");
        var nodPanel=EditorControls.Section(panel,"Наклон головы за мышью",true);
        var nod=new CheckBox{Content="Вращать по часовой / против часовой",IsChecked=h.MouseNodEnabled,Margin=new Thickness(0,5,0,5)};
        nodPanel.Children.Add(nod);nod.Click+=(_,_)=>{h.MouseNodEnabled=nod.IsChecked==true;Changed();};
        nodPanel.Children.Add(EditorControls.Label("Мышь вверх — против часовой, вниз — по часовой. Для отдельной головы и тела; работает независимо от зрачков. Глаза, рот и аксессуары головы наклоняются вместе с ней. Точку поворота установите у шеи."));
        EditorControls.Slider(nodPanel,"Сила наклона (°)",h.MouseNodAngle,0,18,v=>{h.MouseNodAngle=v;Changed();},"0.0");
        EditorControls.Slider(nodPanel,"Плавность наклона (мс)",h.MouseNodSmoothingMs,30,800,v=>{h.MouseNodSmoothingMs=v;Changed();},"0");
        var inverse=new CheckBox{Content="Обратить направление наклона",IsChecked=h.MouseNodInvert,Margin=new Thickness(0,5,0,5)};
        nodPanel.Children.Add(inverse);inverse.Click+=(_,_)=>{h.MouseNodInvert=inverse.IsChecked==true;Changed();};
        pivot.Children.Add(EditorControls.Label("Место соединения головы и шеи. Координаты считаются от левого верхнего угла полного холста."));
        EditorControls.Slider(pivot,"Точка поворота · X (%)",h.PivotX*100,0,100,v=>{h.PivotX=v/100;Changed();},"0");
        EditorControls.Slider(pivot,"Точка поворота · Y (%)",h.PivotY*100,0,100,v=>{h.PivotY=v/100;Changed();},"0");
    }
}
