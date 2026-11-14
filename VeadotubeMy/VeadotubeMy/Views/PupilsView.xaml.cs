using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views;

public partial class PupilsView : UserControl
{
    public PupilsView(AvatarEmotion character, AvatarView scene, Action save, bool imagesInSidebar = false, int selectedEye = 0, string? sectionFilter = null)
    {
        InitializeComponent();
        var p = character.Pupils;
        void Changed() { scene.RefreshVisualSettings(); save(); }
        var mode = new ComboBox { ItemsSource = new[] { "Глаза на PNG персонажа", "Отдельные слои глаз" }, SelectedIndex = p.Enabled ? 1 : 0 };
        Editor.Children.Add(EditorControls.Label("Как устроены глаза"));
        Editor.Children.Add(mode);
        if(sectionFilter!=null){mode.Visibility=Visibility.Collapsed;Editor.Children[0].Visibility=Visibility.Collapsed;}
        var hint = EditorControls.Label("Глаза нарисованы на PNG персонажа. Моргание настраивается отдельной кнопкой на главном экране.");
        Editor.Children.Add(hint);
        if(imagesInSidebar)hint.Visibility=Visibility.Collapsed;
        var details = new StackPanel();
        Editor.Children.Add(details);
        void ShowMode()
        {
            details.Visibility = p.Enabled ? Visibility.Visible : Visibility.Collapsed;
            hint.Text = p.Enabled
                ? "Выберите глаз и добавьте нужные PNG кнопкой +. Можно использовать только зрачки. Значок видимости скрывает слой, а − убирает PNG из проекта."
                : "Глаза нарисованы на PNG персонажа. Моргание настраивается отдельной кнопкой на главном экране.";
        }
        mode.SelectionChanged += (_, _) => { scene.EditEyeRegion(null); p.Enabled = mode.SelectedIndex == 1; Changed(); ShowMode(); };
        ShowMode();
        var side = new ComboBox { ItemsSource = new[] { "Левый глаз", "Правый глаз" }, SelectedIndex = selectedEye, Margin = new Thickness(0,8,0,12) };
        details.Children.Add(side);
        if(imagesInSidebar)side.Visibility=Visibility.Collapsed;
        var rows = new StackPanel();
        details.Children.Add(rows);
        StackPanel Section(Panel parent, string title)
        {
            var body=EditorControls.Section(parent,title,sectionFilter!=null);
            if(sectionFilter!=null){bool show=sectionFilter switch {"Размеры"=>title=="Область глаза и размеры","Перспектива"=>title.StartsWith("Перспектива"),_=>title.StartsWith("Движение")};if(!show)parent.Children[parent.Children.Count-1].Visibility=Visibility.Collapsed;}
            return body;
        }
        var expanded = new HashSet<string>();
        void Rebuild()
        {
            rows.Children.Clear();
            var eye = side.SelectedIndex == 0 ? p.Left : p.Right;
            var other = side.SelectedIndex == 0 ? p.Right : p.Left;
            void Layer(string key, string title, Func<string> get, Action<string> set)
            {
                if(imagesInSidebar)return;
                var body = new StackPanel { Margin = new Thickness(10,4,10,10) };
                var file = EditorControls.Label(string.IsNullOrEmpty(get()) ? "PNG не добавлен" : Path.GetFileName(get()));
                file.ToolTip = get(); body.Children.Add(file);
                var expander = new Expander { Header = title, Content = body, IsExpanded = expanded.Contains(key), Foreground = Brushes.White, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                expander.Expanded += (_,_) => expanded.Add(key);
                expander.Collapsed += (_,_) => expanded.Remove(key);
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
                grid.Children.Add(expander);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                Grid.SetColumn(actions,1); grid.Children.Add(actions);
                var visible = new ToggleButton { Content = "◉", IsChecked = eye.IsLayerVisible(key), Width=32, Height=28, Padding=new Thickness(0), ToolTip="Показывать слой на персонаже", IsEnabled=!string.IsNullOrEmpty(get()) };
                visible.Checked += (_,_) => { eye.SetLayerVisible(key,true); Changed(); };
                visible.Unchecked += (_,_) => { eye.SetLayerVisible(key,false); Changed(); };
                actions.Children.Add(visible);
                var add = new Button { Content="+", Width=32, Height=28, Padding=new Thickness(0), Margin=new Thickness(4,0,0,0), ToolTip="Загрузить или заменить PNG" };
                add.Click += (_,_) =>
                {
                    var dialog = new OpenFileDialog { Filter="PNG (*.png)|*.png", Title=title };
                    if (dialog.ShowDialog()!=true) return;
                    try {
                        using var stream=File.OpenRead(dialog.FileName);
                        var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.StreamSource=stream; image.EndInit(); image.Freeze();
                        set(dialog.FileName); eye.SetLayerVisible(key,true); scene.ReloadImageFiles(); Changed(); Rebuild();
                    } catch { file.Text="Не удалось открыть PNG. Выберите другой файл."; expander.IsExpanded=true; }
                };
                actions.Children.Add(add);
                var remove = new Button { Content="−", Width=32, Height=28, Padding=new Thickness(0), Margin=new Thickness(4,0,0,0), ToolTip="Убрать PNG из проекта. Файл останется на диске.", IsEnabled=!string.IsNullOrEmpty(get()) };
                remove.Click += (_,_) => { set(""); Changed(); Rebuild(); };
                actions.Children.Add(remove);
                if (imagesInSidebar) { add.Visibility = remove.Visibility = Visibility.Collapsed; grid.ColumnDefinitions[1].Width = new GridLength(36); }
                body.Children.Add(EditorControls.Label(key=="mask" ? "Непрозрачная часть маски ограничивает белок и зрачок. Без маски используется овал." : "Слой размещается в общей области выбранного глаза."));
                EditorControls.Button(body,"Разместить область глаза",()=>scene.EditEyeRegion(side.SelectedIndex));
                if(key=="pupil") EditorControls.Slider(body,"Размер зрачка (%)",eye.PupilSize*100,.2,20,v=>{eye.PupilSize=v/100;if(p.LinkSizes)other.PupilSize=eye.PupilSize;Changed();},"0.0");
                rows.Children.Add(new Border { Child=grid, Padding=new Thickness(8), Margin=new Thickness(0,0,0,6), CornerRadius=new CornerRadius(8), Background=new SolidColorBrush(Color.FromRgb(30,36,44)) });
            }
            Layer("white","Белок / глазное яблоко",()=>eye.WhiteImagePath,v=>eye.WhiteImagePath=v);
            Layer("pupil","Зрачок",()=>eye.ImagePath,v=>eye.ImagePath=v);
            Layer("contour","Открытые веки / контур",()=>eye.ForegroundImagePath,v=>eye.ForegroundImagePath=v);
            Layer("half","Полуприкрытое веко",()=>eye.HalfClosedImagePath,v=>eye.HalfClosedImagePath=v);
            Layer("closed","Закрытое веко",()=>eye.ClosedEyeImagePath,v=>eye.ClosedEyeImagePath=v);
            Layer("mask","Маска области",()=>eye.MaskImagePath,v=>eye.MaskImagePath=v);
            var area=Section(rows,"Область глаза и размеры");
            if(imagesInSidebar)EditorControls.Slider(area,"Размер зрачка (%)",eye.PupilSize*100,.2,20,v=>{eye.PupilSize=v/100;if(p.LinkSizes)other.PupilSize=eye.PupilSize;Changed();},"0.0");
            area.Children.Add(EditorControls.Label("Общая область размещает белок, маску и веки. Внутри неё двигается зрачок."));
            var link=new CheckBox { Content="Связать размеры глаз", IsChecked=p.LinkSizes, Margin=new Thickness(0,6,0,8) };
            link.Checked+=(_,_)=>{p.LinkSizes=true;other.CopySizeFrom(eye);Changed();};
            link.Unchecked+=(_,_)=>{p.LinkSizes=false;Changed();};
            area.Children.Add(link);
            if(!imagesInSidebar)EditorControls.Button(area,"Изменить область на персонаже",()=>scene.EditEyeRegion(side.SelectedIndex));
            EditorControls.Slider(area,"Центр X (%)",eye.X*100,0,100,v=>{eye.X=v/100;Changed();},"0.0");
            EditorControls.Slider(area,"Центр Y (%)",eye.Y*100,0,100,v=>{eye.Y=v/100;Changed();},"0.0");
            EditorControls.Slider(area,"Ширина (%)",eye.Width*100,.5,50,v=>{eye.Width=v/100;if(p.LinkSizes)other.Width=eye.Width;Changed();},"0.0");
            EditorControls.Slider(area,"Высота (%)",eye.Height*100,.5,50,v=>{eye.Height=v/100;if(p.LinkSizes)other.Height=eye.Height;Changed();},"0.0");
            EditorControls.Slider(area,"Поворот (°)",eye.RotationDegrees,-180,180,v=>{eye.RotationDegrees=v;Changed();},"0");
            EditorControls.Button(area,"Скопировать размеры в другой глаз",()=>{other.CopySizeFrom(eye);Changed();});
            var perspective=Section(rows,"Перспектива выбранного глаза");
            var depth=new CheckBox{Content="Двигать взгляд в плоскости экрана",IsChecked=eye.PerspectiveEnabled};
            perspective.Children.Add(depth);depth.Click+=(_,_)=>{eye.PerspectiveEnabled=depth.IsChecked==true;Changed();};
            perspective.Children.Add(EditorControls.Label("Размер и положение сохраняются. Наклон задаёт направление движения по экрану; у дальнего глаза уменьшите размах. При размещении области перспектива временно отключается."));
            EditorControls.Slider(perspective,"Разворот в глубину (°)",eye.PerspectiveYaw,-60,60,v=>{eye.PerspectiveYaw=v;Changed();},"0");
            EditorControls.Slider(perspective,"Наклон плоскости (°)",eye.PerspectiveSlope,-45,45,v=>{eye.PerspectiveSlope=v;Changed();},"0");
            EditorControls.Slider(perspective,"Размах движения (%)",eye.PerspectiveTravel*100,0,100,v=>{eye.PerspectiveTravel=v/100;Changed();},"0");
            EditorControls.Button(perspective,"Пример CRT · оба глаза",()=>{
                p.Left.PerspectiveEnabled=p.Right.PerspectiveEnabled=true;
                p.Left.PerspectiveYaw=25;p.Right.PerspectiveYaw=35;
                p.Left.PerspectiveSlope=p.Right.PerspectiveSlope=10;
                p.Left.PerspectiveTravel=1;p.Right.PerspectiveTravel=.75;
                Changed();Rebuild();
            });
        }
        side.SelectionChanged+=(_,_)=>{scene.EditEyeRegion(null);Rebuild();};
        Rebuild();
        var gaze=Section(details,"Движение взгляда · оба глаза");
        string[] keys=["forward","mouse","auto","hybrid"];
        var direction=new ComboBox { ItemsSource=new[]{"Прямо","За мышью","Автоматически","Мышь + автоматический взгляд"},SelectedIndex=Array.IndexOf(keys,p.EffectiveGazeMode) };
        gaze.Children.Add(direction);
        var automatic=new StackPanel();gaze.Children.Add(automatic);
        EditorControls.Slider(automatic,"Сила автоматических движений (%)",p.AutoGazeStrength*100,0,300,v=>{p.AutoGazeStrength=v/100;Changed();},"0");
        var idle=new StackPanel();automatic.Children.Add(idle);
        EditorControls.Slider(idle,"Простой мыши до автовзгляда (с)",p.MouseIdleSeconds,.5,30,v=>{p.MouseIdleSeconds=v;Changed();},"0.0");
        void GazeVisibility(){automatic.Visibility=p.EffectiveGazeMode is "auto" or "hybrid"?Visibility.Visible:Visibility.Collapsed;idle.Visibility=p.EffectiveGazeMode=="hybrid"?Visibility.Visible:Visibility.Collapsed;}
        direction.SelectionChanged+=(_,_)=>{p.GazeMode=keys[direction.SelectedIndex];p.FollowMouse=p.GazeMode=="mouse";Changed();GazeVisibility();};GazeVisibility();
        EditorControls.Slider(gaze,"Диапазон движения",p.Range,0,1,v=>{p.Range=v;Changed();});
        EditorControls.Slider(gaze,"Плавность (мс)",p.SmoothingMs,0,400,v=>{p.SmoothingMs=v;Changed();},"0");
        var soft=new CheckBox { Content="Мягкое начало и остановка",IsChecked=p.SoftGaze };
        soft.Checked+=(_,_)=>{p.SoftGaze=true;Changed();};soft.Unchecked+=(_,_)=>{p.SoftGaze=false;Changed();};gaze.Children.Add(soft);
        void RegionUpdated(object? sender,EventArgs args){if(p.LinkSizes)(side.SelectedIndex==0?p.Right:p.Left).CopySizeFrom(side.SelectedIndex==0?p.Left:p.Right);Changed();Rebuild();}
        Loaded+=(_,_)=>scene.RegionChanged+=RegionUpdated;
        Unloaded+=(_,_)=>{scene.RegionChanged-=RegionUpdated;scene.EditEyeRegion(null);};
    }
}
