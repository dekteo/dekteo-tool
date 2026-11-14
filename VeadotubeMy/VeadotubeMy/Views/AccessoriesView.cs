using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;

public sealed class AccessoriesView : UserControl
{
    public AccessoriesView(AvatarEmotion character,AvatarView scene,Action save)
    {
        var panel=new StackPanel { Margin=new Thickness(12) };Content=panel;
        character.Accessories??=new();
        string? Pick()
        {
            var dialog=new OpenFileDialog {Filter="PNG (*.png)|*.png"};if(dialog.ShowDialog()!=true)return null;
            try{PngFile.Read(dialog.FileName);scene.ReloadImageFiles();return dialog.FileName;}
            catch{MessageBox.Show("Не удалось прочитать PNG.");return null;}
        }
        void Changed(){scene.RefreshAccessories();save();}
        void Build()
        {
            panel.Children.Clear();
            void AddAttached(bool head){var path=Pick();if(path==null)return;scene.EndAccessoryPlacement(false);character.Accessories.Add(AccessoryOptions.AttachedPng(path,head,true));Changed();Build();}
            EditorControls.Button(panel,"+ PNG на голову",()=>AddAttached(true));
            EditorControls.Button(panel,"+ PNG на тело",()=>AddAttached(false));
            EditorControls.Button(panel,"+ Эмблема",()=>{var path=Pick();if(path==null)return;character.Accessories.Add(new AccessoryOptions{Path=path,Name="Эмблема",Motion=false,PulseGlow=true,Width=.08,X=.5,Y=.65});Changed();Build();});
            EditorControls.Button(panel,"+ Подвеска",()=>{var path=Pick();if(path==null)return;character.Accessories.Add(new AccessoryOptions{Path=path,Name="Подвеска"});Changed();Build();});
            if(scene.AccessoryLoadStatus.Length>0)panel.Children.Add(EditorControls.Label(scene.AccessoryLoadStatus));
            if(character.Accessories.Count==0)panel.Children.Add(EditorControls.Label("Аксессуаров пока нет. Добавьте PNG с прозрачным фоном."));
            foreach(var o in character.Accessories)
            {
                var body=EditorControls.Section(panel,o.Name,true);
                var fileRow=new Grid{Margin=new Thickness(0,0,0,8)};fileRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});fileRow.ColumnDefinitions.Add(new ColumnDefinition());body.Children.Add(fileRow);
                var preview=new Image{Height=96,Stretch=Stretch.Uniform,Margin=new Thickness(4,0,12,0)};try{preview.Source=PngFile.Read(o.Path);}catch{preview.ToolTip="PNG недоступен";}fileRow.Children.Add(preview);
                var fileTools=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(fileTools,1);fileRow.Children.Add(fileTools);
                var replace=EditorControls.Button(fileTools,System.IO.Path.GetFileName(o.Path),()=>{var path=Pick();if(path==null)return;o.Path=path;o.Frames.Clear();Changed();Build();});replace.ToolTip="Заменить PNG · "+o.Path;
                var toolbar=new UniformGrid{Columns=4};fileTools.Children.Add(toolbar);
                var eye=new ToggleButton{Style=(Style)FindResource("LayerVisibilityToggle"),Width=double.NaN,IsChecked=o.Visible,Margin=new Thickness(0,0,3,0)};toolbar.Children.Add(eye);eye.Click+=(_,_)=>{o.Visible=eye.IsChecked==true;Changed();};
                Button Icon(string symbol,string hint,Action action){var button=new Button{Content=symbol,Height=30,Padding=new Thickness(0),Margin=new Thickness(3,0,0,0),ToolTip=hint};toolbar.Children.Add(button);button.Click+=(_,_)=>action();return button;}
                var move=Icon("✥","Переместить слой",()=>{});
                Icon("↻","Обновить PNG с диска",()=>{scene.ReloadImageFiles();Changed();Build();});
                Icon("×","Удалить аксессуар. PNG останется на диске",()=>{scene.EndAccessoryPlacement(false);character.Accessories.Remove(o);Changed();Build();});
                var settings=new StackPanel();body.Children.Add(new Expander{Header="Настройки аксессуара",Content=settings});
                var name=new TextBox {Text=o.Name,MaxLength=50,ToolTip="Название аксессуара"};settings.Children.Add(name);
                name.LostKeyboardFocus+=(_,_)=>{if(!string.IsNullOrWhiteSpace(name.Text)){o.Name=name.Text.Trim();save();}};
                if(o.Frames.Count>1)
                {
                    settings.Children.Add(EditorControls.Label($"Кадров: {o.Frames.Count}. Порядок по имени файла (001, 002…)."));
                    var mode=new ComboBox{ItemsSource=new[]{"По кругу","Иногда · случайный интервал"},SelectedIndex=o.Intermittent?1:0};settings.Children.Add(mode);mode.SelectionChanged+=(_,_)=>{o.Intermittent=mode.SelectedIndex==1;Changed();Build();};
                    EditorControls.Slider(settings,"Кадров в секунду",o.FramesPerSecond,1,30,v=>{o.FramesPerSecond=v;Changed();},"0");
                    if(o.Intermittent)
                    {
                        EditorControls.Slider(settings,"Пауза от (сек.)",o.IntervalMin,1,300,v=>{o.IntervalMin=v;o.IntervalMax=Math.Max(o.IntervalMax,v);Changed();},"0");
                        EditorControls.Slider(settings,"Пауза до (сек.)",o.IntervalMax,1,600,v=>{o.IntervalMax=Math.Max(o.IntervalMin,v);Changed();},"0");
                        var hide=new CheckBox{Content="Скрывать между запусками",IsChecked=o.HideBetween};settings.Children.Add(hide);hide.Click+=(_,_)=>{o.HideBetween=hide.IsChecked==true;Changed();};
                    }
                }
                void Toggle(string title,bool value,Action<bool> set)
                {var box=new CheckBox{Content=title,IsChecked=value,Margin=new Thickness(0,5,0,5)};settings.Children.Add(box);box.Click+=(_,_)=>{set(box.IsChecked==true);Changed();};}

                settings.Children.Add(EditorControls.Label("Прикрепить к"));
                var anchor=new ComboBox {ItemsSource=new[]{"Телу / персонажу","Голове"},SelectedIndex=o.Head?1:0};settings.Children.Add(anchor);
                anchor.SelectionChanged+=(_,_)=>{o.Head=anchor.SelectedIndex==1;Changed();};
                Toggle("Позади выбранной части",o.Behind,v=>o.Behind=v);
                Toggle("Плавное покачивание",o.Motion,v=>o.Motion=v);
                Toggle("Пульсирующее свечение",o.PulseGlow,v=>o.PulseGlow=v);
                EditorControls.Slider(settings,"Яркость свечения (%)",o.PulseStrength*100,0,100,v=>{o.PulseStrength=v/100;Changed();},"0");
                EditorControls.Slider(settings,"Период свечения (сек.)",o.PulseSeconds,1,10,v=>{o.PulseSeconds=v;Changed();},"0.0");
                var glowColors=new ComboBox{ItemsSource=new[]{"Бирюзовый","Синий","Фиолетовый","Тёплый"},SelectedIndex=Array.IndexOf(new[]{"#20EFFF","#4080FF","#BC60FF","#FFD080"},o.PulseColor)};settings.Children.Add(glowColors);
                glowColors.SelectionChanged+=(_,_)=>{if(glowColors.SelectedIndex>=0){o.PulseColor=new[]{"#20EFFF","#4080FF","#BC60FF","#FFD080"}[glowColors.SelectedIndex];Changed();}};
                EditorControls.Button(settings,"▶ Проверить свечение · 4 секунды",()=>scene.TestAccessoryGlow(o.Id));
                Toggle("Металлический блик",o.Metallic,v=>o.Metallic=v);
                EditorControls.Slider(settings,"Сила блика (%)",o.Shine*100,0,100,v=>{o.Shine=v/100;Changed();},"0");
                EditorControls.Slider(settings,"Размах (°)",o.Amount,0,25,v=>{o.Amount=v;Changed();},"0.0");
                EditorControls.Slider(settings,"Скорость",o.Speed,.25,2,v=>{o.Speed=v;Changed();},"0.00");
                EditorControls.Slider(settings,"Затухание",o.Damping,.15,1.5,v=>{o.Damping=v;Changed();},"0.00");
                EditorControls.Button(settings,o.Frames.Count>1?"▶ Проиграть сейчас":"▶ Качнуть · тест без голоса",()=>scene.TestAccessory(o.Id));
                var actions=new StackPanel {Visibility=Visibility.Collapsed};body.Children.Add(actions);
                actions.Children.Add(EditorControls.Label("Потяните область, чтобы переместить. Красная точка — крепление, белая — размер. Жёлтая линия отделяет гибкую цепочку сверху от жёсткой подвески снизу."));
                void End(bool apply){scene.EndAccessoryPlacement(apply);foreach(UIElement child in panel.Children)child.IsEnabled=true;settings.IsEnabled=true;actions.Visibility=Visibility.Collapsed;Build();}
                EditorControls.Button(actions,"Применить",()=>End(true));EditorControls.Button(actions,"Отмена",()=>End(false));
                var place=EditorControls.Button(settings,"✥ Переместить · размер · деформация",()=>{
                    ((TextBlock)actions.Children[0]).Text="Потяните область для перемещения. Красная точка — крепление, белая — общий размер, зелёная — растянуть высоту, жёлтая линия — начало жёсткой части. Поворот и деформация — ниже.";
                    if(!scene.BeginAccessoryPlacement(o,save)){MessageBox.Show("Покажите аксессуар и проверьте доступность PNG.");return;}
                    var card=((FrameworkElement)body.Parent).Parent;
                    foreach(UIElement child in panel.Children)child.IsEnabled=ReferenceEquals(child,card);
                    settings.IsEnabled=false;actions.Visibility=Visibility.Visible;actions.BringIntoView();
                });
                settings.Children.Remove(place);move.Click+=(_,_)=>place.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                double rotation=o.Rotation,skew=o.Skew,bend=o.Bend;
                var arrows=new System.Windows.Controls.Primitives.UniformGrid{Columns=4};actions.Children.Add(arrows);
                foreach(var entry in new[]{("←",-1d,0d),("↑",0d,-1d),("↓",0d,1d),("→",1d,0d)})
                {var button=new Button{Content=entry.Item1,Margin=new Thickness(2),Padding=new Thickness(8)};arrows.Children.Add(button);button.Click+=(_,_)=>scene.NudgeAccessory(entry.Item2,entry.Item3);}
                actions.Children.Add(EditorControls.Label("Стрелки на клавиатуре после щелчка по рамке — точный сдвиг; Shift — крупнее."));
                var shape=EditorControls.Section(actions,"Поворот и деформация",true);
                void Shape()=>scene.SetAccessoryShape(rotation,skew,bend);
                EditorControls.Slider(shape,"Поворот (°)",rotation,-180,180,v=>{rotation=v;Shape();},"0");
                EditorControls.Slider(shape,"Наклон формы (°)",skew,-40,40,v=>{skew=v;Shape();},"0");
                EditorControls.Slider(shape,"Изгиб (°)",bend,-30,30,v=>{bend=v;Shape();},"0");
                EditorControls.Button(settings,"Выделить зону металлического блика",()=>{
                    if(!scene.BeginAccessoryPlacement(o,save,true)){MessageBox.Show("Покажите аксессуар и проверьте PNG.");return;}
                    var card=((FrameworkElement)body.Parent).Parent;
                    foreach(UIElement child in panel.Children)child.IsEnabled=ReferenceEquals(child,card);
                    settings.IsEnabled=false;actions.Visibility=Visibility.Visible;actions.BringIntoView();
                    ((TextBlock)actions.Children[0]).Text="Золотая область — зона блика. Потяните её для перемещения, квадратные углы — для размера. Затем примените или отмените.";
                });
                EditorControls.Button(settings,"Блик по всему PNG",()=>{o.ShineRegion=false;Changed();});
                EditorControls.Button(settings,"Удалить аксессуар",()=>{scene.EndAccessoryPlacement(false);character.Accessories.Remove(o);Changed();Build();});
            }

        }
        Build();Unloaded+=(_,_)=>scene.EndAccessoryPlacement(false);
    }
}
