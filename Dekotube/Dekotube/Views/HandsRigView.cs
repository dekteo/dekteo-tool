using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Win32;
using Dekotube.Services;
namespace Dekotube.Views;
public sealed class HandsRigView:UserControl
{
    public HandsRigView(AvatarEmotion character,AvatarView scene,Action save,Action? createSample=null)
    {
        scene.HandsSettingsOpen=true;
        character.Hands??=new();var o=character.Hands;var panel=new StackPanel{Margin=new Thickness(12)};Content=panel;
        var status=EditorControls.Label(scene.HandsStatus);
        void Changed(){scene.RefreshHands();save();}
        void Build()
        {
            panel.Children.Clear();status.Text=scene.HandsStatus;panel.Children.Add(status);
            panel.Children.Add(EditorControls.Label("Руки и устройство — отдельный набор поверх одежды. Голова, рот, глаза и аксессуары сохраняют свои настройки. Для другой одежды нужны рукава, подходящие к манжетам этой позы."));
            var settings=new StackPanel();panel.Children.Add(settings);
            var modes=new UniformGrid{Columns=2,Margin=new Thickness(0,0,0,8)};panel.Children.Insert(0,modes);
            foreach(var choice in new[]{("Клавиатура и мышь",true),("Геймпад",false)}){var button=new Button{Content=choice.Item1,Margin=new Thickness(2),IsEnabled=o.DeskMode!=choice.Item2};modes.Children.Add(button);button.Click+=(_,_)=>{scene.StopHandsTest();o.DeskMode=choice.Item2;scene.SelectHandsSet(o.DeskMode);Changed();Build();};}
            var enable=new CheckBox{Content="Показывать и реагировать на устройство",IsChecked=o.Enabled,Margin=new Thickness(0,8,0,8)};settings.Children.Add(enable);enable.Click+=(_,_)=>{o.Enabled=enable.IsChecked==true;Changed();};
            var automatic=new CheckBox{Content="Автоматически менять позу по вводу",IsChecked=o.AutoSwitch,Margin=new Thickness(0,5,0,5)};settings.Children.Add(automatic);automatic.Click+=(_,_)=>{o.AutoSwitch=automatic.IsChecked==true;Changed();};
            settings.Children.Add(EditorControls.Label("Нужны оба набора PNG. Подключение устройства без нажатий не меняет позу. При одновременном вводе сохраняется текущая поза. Пока это меню открыто, автосмена приостановлена."));
            EditorControls.Slider(settings,"Плавность смены (сек.)",o.TransitionSeconds,.1,1,v=>{o.TransitionSeconds=v;save();},"0.0");
            var files=new StackPanel{Margin=new Thickness(0,8,0,8)};settings.Children.Add(files);
            Action placeHands=()=>{};
            if(!o.Current.WholeHands&&!o.Current.LayeredArms)files.Children.Add(EditorControls.Label(o.DeskMode?"Нужна отдельная поза: клавиатура с мышью, две кисти и два подвижных фрагмента пальцев. Реакция на клавиши и клики; текст не записывается.":"Порядок слоёв: геймпад, кисть слева, кисть справа, большой палец слева, большой палец справа. Все PNG — на одном полном холсте."));
            var labels=new[]{o.DeskMode?"Клавиатура и мышь":"Геймпад",o.DeskMode?"Кисть на клавиатуре":"Кисть слева",o.DeskMode?"Кисть на мыши":"Кисть справа",o.DeskMode?"Палец на клавиатуре":"Палец слева",o.DeskMode?"Палец на мыши":"Палец справа"};
            if(o.Current.LayeredArms)labels=new[]{"Руки и рукава"};
            if(o.Current.WholeHands){
                labels=o.DeskMode?new[]{"Коврик","Клавиатура","Мышь","Цельная кисть клавиатуры","Цельная кисть мыши"}:new[]{"Геймпад","Цельная левая кисть","Цельная правая кисть"};
                files.Children.Add(EditorControls.Label("Цельные кисти: пальцы сгибаются деформацией PNG. Для другой геометрии кисти нужен набор с подходящей областью деформации."));
            }
            for(int i=0;i<labels.Length;i++){
                int index=i;
                string path=o.Current.Layers.ElementAtOrDefault(i)??"";bool loaded=!string.IsNullOrEmpty(path);
                var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(loaded?112:0)});grid.ColumnDefinitions.Add(new ColumnDefinition());
                var card=new Border{Child=grid,Padding=new Thickness(loaded?8:0),Margin=new Thickness(0,3,0,7),CornerRadius=new CornerRadius(8),Background=loaded?new SolidColorBrush(Color.FromRgb(26,30,34)):Brushes.Transparent};files.Children.Add(card);
                if(loaded){var preview=new Image{Height=96,Stretch=Stretch.Uniform,Margin=new Thickness(4,0,12,0),ToolTip=labels[i]};try{preview.Source=PngFile.Read(path);}catch{preview.ToolTip="Файл недоступен: "+path;}grid.Children.Add(preview);}
                var right=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(right,1);grid.Children.Add(right);
                var upload=EditorControls.Button(right,loaded?Path.GetFileName(path):"+ Добавить PNG · "+labels[i],()=>{
                    var d=new OpenFileDialog{Filter="PNG (*.png)|*.png"};if(d.ShowDialog()!=true)return;
                    try{
                        var im=PngFile.Read(d.FileName);var r=o.Current;
                        if(r.Layers.Any(x=>!string.IsNullOrEmpty(x))&&(im.PixelWidth!=r.CanvasWidth||im.PixelHeight!=r.CanvasHeight))throw new InvalidDataException("Все PNG должны иметь одинаковый размер полного холста.");
                        r.CanvasWidth=im.PixelWidth;r.CanvasHeight=im.PixelHeight;
                        while(r.Layers.Count<labels.Length)r.Layers.Add("");r.Layers[index]=d.FileName;
                        scene.ReloadImageFiles();Changed();Build();
                    }catch(Exception ex){MessageBox.Show(ex.Message);}
                });
                upload.ToolTip=loaded?labels[i]+"\n"+path+"\nНажмите, чтобы заменить PNG":"Добавить PNG · "+labels[i];upload.Height=38;
                if(loaded){
                    var tools=new UniformGrid{Columns=4,Margin=new Thickness(0,6,0,0)};right.Children.Add(tools);
                    var eye=new ToggleButton{Style=(Style)FindResource("LayerVisibilityToggle"),Width=double.NaN,Margin=new Thickness(0,0,3,0),IsChecked=o.Enabled};tools.Children.Add(eye);
                    eye.ToolTip="Показать / скрыть руки и устройство";eye.Click+=(_,_)=>{o.Enabled=eye.IsChecked==true;Changed();Build();};
                    void Icon(string symbol,string hint,Action action){var button=new Button{Content=symbol,Height=30,Margin=new Thickness(3,0,0,0),Padding=new Thickness(0),ToolTip=hint};tools.Children.Add(button);button.Click+=(_,_)=>action();}
                    Icon("✥","Переместить руки и устройство",()=>placeHands());
                    Icon("↻","Обновить PNG с диска после сохранения под тем же именем",()=>{scene.ReloadImageFiles();Changed();Build();});
                    Icon("×","Убрать PNG. Файл останется на диске",()=>{o.Current.Layers[index]="";Changed();Build();});
                }
            }
            var movement=EditorControls.Section(settings,"Движение",true);
            if(o.DeskMode){
                var follow=new ComboBox{ItemsSource=new[]{"По положению курсора · сохранять позу","По скорости мыши · возврат в центр"},SelectedIndex=o.MouseFollowsPosition?0:1};movement.Children.Add(follow);
                follow.SelectionChanged+=(_,_)=>{o.MouseFollowsPosition=follow.SelectedIndex==0;Changed();};
            }
            if(o.Current.LayeredArms)files.Children.Add(EditorControls.Label("Цельные рукава с неподвижными плечами. Для этой позы используется один прозрачный слой рук; тело и голова настраиваются отдельно."));
            EditorControls.Slider(movement,"Выразительность (%)",o.Current.Amount*100,0,200,v=>{o.Current.Amount=v/100;Changed();},"0");
            EditorControls.Button(movement,"▶ Тест без устройства · 6 секунд",()=>scene.TestHands());
            EditorControls.Button(movement,"■ Остановить тест",()=>scene.StopHandsTest());
            if(o.Current.LayeredArms){
                EditorControls.Button(movement,"✎ Разметить движение этого PNG",()=>{
                    var rig=o.Current;var path=rig.Layers.FirstOrDefault();
                    if(string.IsNullOrWhiteSpace(path)){MessageBox.Show("Сначала загрузите PNG рук.");return;}
                    try{
                        scene.StopHandsTest();BitmapSource? body=null;
                        if(File.Exists(character.Head.BodyImagePath))body=PngFile.Read(character.Head.BodyImagePath);
                        var window=new ArmMotionWindow(PngFile.Read(path),rig.MotionMap,o.DeskMode,rig.MotionMaps.Where(p=>p.Key!=path),body){Owner=Window.GetWindow(this)};
                        if(window.ShowDialog()==true){rig.MotionMaps[path]=window.Result;Changed();Build();}
                    }catch(Exception ex){MessageBox.Show("Не удалось открыть редактор: "+ex.Message);}
                });
                if(o.Current.MotionMap!=null)EditorControls.Button(movement,"Отключить разметку · старое движение",()=>{o.Current.MotionMap.Enabled=false;Changed();Build();});
                movement.Children.Add(EditorControls.Label("Разметка хранится отдельно для каждого PNG и каждой позы. Новому костюму можно скопировать разметку другого PNG и подвинуть точки."));
            }
            var edit=new StackPanel{Visibility=Visibility.Collapsed};panel.Children.Add(edit);
            void End(bool apply){scene.EndHandsPlacement(apply);Build();}
            EditorControls.Button(edit,"Применить",()=>End(true));EditorControls.Button(edit,"Отмена",()=>End(false));
            edit.Children.Add(EditorControls.Label("Перетащите рамку на персонаже. После клика по рамке работают стрелки; Shift — крупный шаг. Одежда остаётся на месте."));
            var arrows=new UniformGrid{Columns=4};edit.Children.Add(arrows);
            foreach(var a in new[]{("←",-1d,0d),("↑",0d,-1d),("↓",0d,1d),("→",1d,0d)}){var b=new Button{Content=a.Item1,Margin=new Thickness(2)};arrows.Children.Add(b);b.Click+=(_,_)=>scene.NudgeHands(a.Item2,a.Item3);}
            double scale=o.Current.Scale,rotation=o.Current.Rotation;
            EditorControls.Slider(edit,"Размер (%)",scale*100,10,300,v=>{scale=v/100;scene.ShapeHands(scale,rotation);},"0");
            EditorControls.Slider(edit,"Поворот (°)",rotation,-45,45,v=>{rotation=v;scene.ShapeHands(scale,rotation);},"0");
            placeHands=()=>{if(!scene.BeginHandsPlacement(save)){MessageBox.Show("Сначала загрузите PNG для выбранной позы.");return;}settings.IsEnabled=false;edit.Visibility=Visibility.Visible;edit.BringIntoView();};
        }
        void Status(object? s,EventArgs e)=>status.Text=scene.HandsStatus;
        scene.HandsStatusChanged+=Status;Build();Unloaded+=(_,_)=>{scene.HandsSettingsOpen=false;scene.HandsStatusChanged-=Status;scene.EndHandsPlacement(false);scene.StopHandsTest();};
    }
}
