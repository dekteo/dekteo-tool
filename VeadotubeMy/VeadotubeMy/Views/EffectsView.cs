using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views;

public sealed class EffectsView : UserControl
{
    private static readonly (string Kind, string Name, string Parameter, string Icon)[] Types =
    [ ("jump", "Прыжок", "Высота прыжка", "M12,22 L12,3 M5,10 L12,3 L19,10"),
      ("wave", "Покачивание", "Размах движения", "M1,12 C5,0 8,0 12,12 C16,24 19,24 23,12"),
      ("rotate", "Наклон", "Угол наклона", "M4,17 A9,9 0 1 1 20,17 M20,10 L20,17 L13,17"),
      ("random", "Случайное движение", "Размах движения", "M12,2 L12,22 M2,12 L22,12 M8,6 L12,2 L16,6 M18,8 L22,12 L18,16"),
      ("darken", "Затемнение", "Степень затемнения", "M12,2 A10,10 0 1 0 12,22 A10,10 0 1 0 12,2 M12,2 L12,22") ];
    private static FrameworkElement Icon(string geometry) => new Path { Data = Geometry.Parse(geometry), Stroke = Brushes.Turquoise, StrokeThickness = 1.8, Width = 22, Height = 22, Stretch = Stretch.Uniform, Margin = new Thickness(0,0,10,0) };

    public EffectsView(AvatarEmotion character, AvatarView scene, Action save, bool layerWorkspace=false)
    {
        var panel = new StackPanel { Margin = new Thickness(18) }; Content = panel;
        character.AddedEffectCards ??= new();
        character.VoiceLights??=new();if(character.VoiceLights.Enabled&&!character.AddedEffectCards.Contains("voiceLights"))character.AddedEffectCards.Add("voiceLights");
        character.IdleExpression ??= new();character.RobotScreen??=new();
        foreach(var entry in new[]{("processing",character.RobotScreen.Processing),("glitch",character.RobotScreen.Glitch),("bars",character.RobotScreen.VoiceBars)})if(entry.Item2&&!character.AddedEffectCards.Contains(entry.Item1))character.AddedEffectCards.Add(entry.Item1);
        if(character.IdleExpression.Enabled && !character.AddedEffectCards.Contains("mimic"))character.AddedEffectCards.Add("mimic");
        if(character.Head.BreathingEnabled && !character.AddedEffectCards.Contains("breath"))character.AddedEffectCards.Add("breath");
            if(character.Head.Enabled && !character.AddedEffectCards.Contains("head"))character.AddedEffectCards.Add("head");
            if(character.MouthTransitionMs>0 && !character.AddedEffectCards.Contains("mouth"))character.AddedEffectCards.Add("mouth");
        if(character.Head.PoseEnabled&&!character.AddedEffectCards.Contains("pose"))character.AddedEffectCards.Add("pose");
        if(character.ScreenLight.Enabled&&!character.AddedEffectCards.Contains("screenLight"))character.AddedEffectCards.Add("screenLight");
        if(character.ColorCorrections.Values.Any(o=>o.Enabled)&&!character.AddedEffectCards.Contains("color"))character.AddedEffectCards.Add("color");
        var expandedCards=new HashSet<string>();
        string? newlyAdded=null;
        int selectedCategory=0;
        TextBlock? lightingLabel=null;
        void LightingUpdated(object? sender,EventArgs args){if(lightingLabel!=null)lightingLabel.Text=scene.LightingStatus;}
        Loaded+=(_,_)=>scene.LightingStatusChanged+=LightingUpdated;
        void Changed() { scene.RefreshScreenEditor(); scene.UpdateRenderingSubscription(); if (!scene.IsScreenEditing) save(); }
        void Build(CharacterEffect? expand = null)
        {
            panel.Children.Clear();lightingLabel=null;
            var groups=new Dictionary<string,StackPanel>();
            foreach(var name in new[]{"Лицо","Движение","Экран","Освещение"})
            {
                var group=new StackPanel{Margin=new Thickness(0,8,0,12)};
                group.Children.Add(new Border{BorderBrush=Brushes.Turquoise,BorderThickness=new Thickness(0,0,0,2),Padding=new Thickness(0,6,0,8),Child=new TextBlock{Text=name.ToUpperInvariant(),Foreground=Brushes.Turquoise,FontWeight=FontWeights.Bold,FontSize=13}});
                groups[name]=group;panel.Children.Add(group);
            }
            StackPanel Card(string group,string title)
            {
                var body=EditorControls.Section(groups[group],title,expandedCards.Contains(title)||newlyAdded==title);
                var expander=(Expander)((Border)groups[group].Children[groups[group].Children.Count-1]).Child;
                expander.Expanded+=(_,_)=>expandedCards.Add(title);expander.Collapsed+=(_,_)=>expandedCards.Remove(title);
                return body;
            }
            void Embedded(string key,string title,Func<UserControl> create,Action remove)
            {
                if(layerWorkspace || !character.AddedEffectCards.Contains(key))return;
                var body=Card(key=="blink"?"Лицо":"Освещение",title);
                body.Children.Add(create());
                EditorControls.Button(body,"Удалить эффект",()=>{remove();character.AddedEffectCards.Remove(key);save();Build();});
            }
            Embedded("blink","Моргание",()=>new BlinkView(character,scene,save),()=>{if(character.Pupils.Enabled){character.Pupils.BlinkEnabled=false;scene.RefreshVisualSettings();}else{scene.BlinkService.Stop();ConfigurationService.UpdateDefaultProfile(p=>p.BlinkEnabled=false);}});
            Embedded("screenLight","Свет от игры",()=>new ScreenLightView(character,scene,save),()=>{character.ScreenLight.Enabled=false;scene.RefreshScreenLight();});
            Embedded("color","Коррекция цвета",()=>new ColorCorrectionView(character,scene,save),()=>{foreach(var o in character.ColorCorrections.Values)o.Enabled=false;scene.RefreshColorCorrection();});
            if(character.AddedEffectCards.Contains("voiceLights"))
            {
                var lights=Card("Экран","Точки-индикаторы голоса");lights.Children.Add(new VoiceLightsView(character,scene,save));
                EditorControls.Button(lights,"Удалить эффект",()=>{scene.EndVoiceLightsPlacement(false);character.VoiceLights.Enabled=false;scene.StopVoiceLightsTest();character.AddedEffectCards.Remove("voiceLights");save();Build();});
            }
            foreach(var entry in new[]{("processing","Обработка…"),("glitch","Цифровой сбой"),("bars","Индикатор голоса")})
            {
                if(!character.AddedEffectCards.Contains(entry.Item1))continue;
                var o=character.RobotScreen;string key=entry.Item1;
                var body=Card("Экран",entry.Item2);
                bool Enabled()=>key=="processing"?o.Processing:key=="glitch"?o.Glitch:o.VoiceBars;
                void Set(bool value){if(key=="processing")o.Processing=value;else if(key=="glitch")o.Glitch=value;else o.VoiceBars=value;Changed();}
                var on=new CheckBox{Content="Включить автоматически",IsChecked=Enabled(),Margin=new Thickness(0,5,0,8)};body.Children.Add(on);on.Click+=(_,_)=>Set(on.IsChecked==true);
                body.Children.Add(EditorControls.Label(key=="processing"?"В долгой паузе появляются три бегущие точки. При речи они плавно исчезают.":key=="glitch"?"Редкий короткий сдвиг полос основы с цветными помехами внутри экрана.":"Небольшие полоски справа реагируют на громкость голоса. Это декоративный индикатор уровня, не спектр частот."));
                if(key=="processing")EditorControls.Slider(body,"Пауза до появления (сек.)",o.QuietSeconds,5,120,v=>{o.QuietSeconds=v;Changed();},"0");
                if(key=="glitch"){
                    EditorControls.Slider(body,"Средний интервал (сек.)",o.GlitchInterval,10,180,v=>{o.GlitchInterval=v;Changed();},"0");
                    EditorControls.Slider(body,"Сила (%)",o.Strength*100,0,100,v=>{o.Strength=v/100;Changed();},"0");
                }
                if(key=="bars")EditorControls.Slider(body,"Чувствительность",o.BarsGain,.5,8,v=>{o.BarsGain=v;Changed();},"0.0");
                EditorControls.Button(body,"▶ Проверить без голоса",()=>scene.TestRobotScreen(key));
                EditorControls.Button(body,"Остановить тест",scene.StopRobotScreenTest);
                var placementActions=new StackPanel{Visibility=Visibility.Collapsed};body.Children.Add(placementActions);
                placementActions.Children.Add(EditorControls.Label("Потяните голубую рамку для перемещения, белый угол — для размера. Персонаж при редактировании неподвижен."));
                void EndRobot(bool apply){scene.EndRobotPlacement(apply);Build();}
                EditorControls.Button(placementActions,"Применить размещение",()=>EndRobot(true));EditorControls.Button(placementActions,"Отмена",()=>EndRobot(false));
                EditorControls.Button(body,"✥ Переместить и изменить размер",()=>{scene.BeginRobotPlacement(key,save);foreach(UIElement element in body.Children)element.IsEnabled=ReferenceEquals(element,placementActions);placementActions.Visibility=Visibility.Visible;placementActions.BringIntoView();});
                EditorControls.Button(body,"Выбрать общую область экрана",()=>scene.BeginScreenRegionEditing(save));
                EditorControls.Button(body,"Удалить эффект",()=>{Set(false);scene.StopRobotScreenTest();character.AddedEffectCards.Remove(key);save();Build();});
            }
            if(character.AddedEffectCards.Contains("mimic"))
            {
                var expression=character.IdleExpression;
                var card=Card("Лицо","Живая мимика");
                card.Children.Add(EditorControls.Label("Редкие мягкие выражения в паузах: лёгкий прищур и изгиб полоски рта. Нужны отдельные PNG-зрачки и процедурный рот. При речи выражение плавно уходит."));
                void ToggleExpression(string label,bool value,Action<bool> set){var box=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,4,0,4)};card.Children.Add(box);box.Click+=(_,_)=>{set(box.IsChecked==true);Changed();};}
                ToggleExpression("Включить в паузах",expression.Enabled,v=>expression.Enabled=v);
                ToggleExpression("Глаза",expression.Eyes,v=>expression.Eyes=v);
                ToggleExpression("    Прищур",expression.Squint,v=>expression.Squint=v);
                ToggleExpression("    Лёгкое раскрытие",expression.Widen,v=>expression.Widen=v);
                ToggleExpression("Рот",expression.Mouth,v=>expression.Mouth=v);
                ToggleExpression("    Изгиб",expression.Bend,v=>expression.Bend=v);
                ToggleExpression("    Поднятие уголка",expression.Corner,v=>expression.Corner=v);
                ToggleExpression("    Сужение",expression.Narrow,v=>expression.Narrow=v);
                EditorControls.Slider(card,"Сила (%)",expression.Strength*100,0,100,v=>{expression.Strength=v/100;Changed();},"0");
                EditorControls.Slider(card,"Средняя пауза (сек.)",expression.Interval,6,90,v=>{expression.Interval=v;Changed();},"0");
                var status=EditorControls.Label(scene.IdleExpressionStatus);card.Children.Add(status);
                void StatusChanged(object? sender,EventArgs e)=>status.Text=scene.IdleExpressionStatus;
                status.Loaded+=(_,_)=>scene.IdleExpressionChanged+=StatusChanged;
                status.Unloaded+=(_,_)=>scene.IdleExpressionChanged-=StatusChanged;
                EditorControls.Button(card,"▶ Проверить выбранные по очереди",scene.PreviewIdleExpression);
                EditorControls.Button(card,"Остановить тест",scene.StopIdleExpressionPreview);
                EditorControls.Button(card,"Удалить эффект",()=>{expression.Enabled=false;scene.StopIdleExpressionPreview();character.AddedEffectCards.Remove("mimic");Changed();Build();});
            }
            if(character.AddedEffectCards.Contains("pose"))
            {
                var pose=Card("Движение","Спокойная смена позы");
                pose.Children.Add(EditorControls.Label("Персонаж слегка подаётся вперёд, задерживается и возвращается. Между движениями — паузы 12–28 секунд. Отдельная голова следует с небольшим запаздыванием; цельный PNG движется целиком."));
                var enabled=new CheckBox {Content="Включить смену позы",IsChecked=character.Head.PoseEnabled,Margin=new Thickness(0,6,0,6)};
                enabled.Click+=(_,_)=>{character.Head.PoseEnabled=enabled.IsChecked==true;Changed();};pose.Children.Add(enabled);
                EditorControls.Slider(pose,"Сила движения (%)",character.Head.PoseAmount*100,0,200,v=>{character.Head.PoseAmount=v/100;Changed();},"0");
                EditorControls.Button(pose,"▶ Тест без голоса · 10 секунд",scene.PreviewPose);
                EditorControls.Button(pose,"Остановить тест",scene.StopPosePreview);
                EditorControls.Button(pose,"Удалить эффект",()=>{character.Head.PoseEnabled=false;scene.StopPosePreview();scene.StopScreenDepthTest();character.AddedEffectCards.Remove("pose");Changed();Build();});
            }
            if(character.AddedEffectCards.Contains("breath"))
            {
            var breath = new StackPanel { Margin = new Thickness(8) };
            var breathing = new CheckBox { Content="Лёгкое дыхание", IsChecked=character.Head.BreathingEnabled, Margin=new Thickness(0,4,0,8) };
            breathing.Checked += (_,_)=>{character.Head.BreathingEnabled=true;Changed();};
            breathing.Unchecked += (_,_)=>{character.Head.BreathingEnabled=false;Changed();};
            breath.Children.Add(breathing);
            breath.Children.Add(EditorControls.Label("Персонаж слегка расширяется и приподнимается на вдохе, оставаясь на месте снизу. Работает и с цельным PNG; новые рисунки не нужны."));
            EditorControls.Slider(breath,"Сила дыхания (%)",character.Head.BreathingAmount*100,0,300,v=>{character.Head.BreathingAmount=v/100;Changed();},"0");
            EditorControls.Slider(breath,"Вдохов в минуту",character.Head.BreathingRate,4,24,v=>{character.Head.BreathingRate=v;Changed();},"0");
            Card("Движение","Дыхание").Children.Add(breath);
            EditorControls.Button(breath,"Удалить эффект",()=>{character.Head.BreathingEnabled=false;character.AddedEffectCards.Remove("breath");Changed();Build();});
            }
            if (character.ScreenEffect.Added || character.ScreenEffect.Enabled || character.ScreenEffect.DepthEnabled || character.ScreenEffect.Scanlines || character.ScreenEffect.Flicker)
            {
                character.ScreenEffect.Added = true;
                var screen = new StackPanel { Margin = new Thickness(8) };
                StackPanel Section(string title, bool expanded = false)
                {
                    var body = new StackPanel { Margin = new Thickness(8) };
                    screen.Children.Add(new Border {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(55,68,82)), BorderThickness = new Thickness(1),
                        Background = new SolidColorBrush(Color.FromRgb(25,31,39)), CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(8), Margin = new Thickness(0,0,0,12),
                        Child = new Expander { Header = title, Content = body, IsExpanded = expanded, HorizontalContentAlignment = HorizontalAlignment.Stretch }
                    });
                    return body;
                }
                var area = Section("Область экрана");
                var depth=Section("Глубина под стеклом");
                EditorControls.Button(depth,"▶ Проверить глубину · 6 секунд",scene.TestScreenDepth);
                depth.Children.Add(EditorControls.Label("Тест смещает отдельные глаза и рот влево и вправо. На цельном PNG движения внутри экрана не будет."));
                var depthEnabled=new CheckBox{Content="Глаза и рот под стеклом",IsChecked=character.ScreenEffect.DepthEnabled};depth.Children.Add(depthEnabled);
                depthEnabled.Click+=(_,_)=>{character.ScreenEffect.DepthEnabled=depthEnabled.IsChecked==true;Changed();};
                depth.Children.Add(EditorControls.Label("Небольшой параллакс при наклоне или повороте головы. Нужны отдельные глаза/рот и движение головы; нарисованные на основе элементы не отделяются. Отражение сдвигается в противоположную сторону."));
                EditorControls.Slider(depth,"Глубина (пиксели холста)",character.ScreenEffect.DepthAmount,0,16,v=>{character.ScreenEffect.DepthAmount=v;Changed();},"0.0");
                var reflection = Section("Блик");
                var scanPanel = Section("Полосы · Scanline");
                var flickerPanel = Section("Мерцание");
                var enabled = new CheckBox { Content = "Включить движущийся блик", IsChecked = character.ScreenEffect.Enabled };
                enabled.Checked += (_,_) => { character.ScreenEffect.Enabled=true; Changed(); };
                enabled.Unchecked += (_,_) => { character.ScreenEffect.Enabled=false; Changed(); };
                reflection.Children.Add(enabled);
                area.Children.Add(EditorControls.Label("Общая зона для всех эффектов экрана. Выберите её на персонаже; голубая заливка покажет размер, скругление и деформацию."));
                EditorControls.Button(area,"Выбрать область на персонаже",()=>scene.BeginScreenRegionEditing(save));
                EditorControls.Button(area,"Готово · применить область",()=>scene.EndScreenRegionEditing());
EditorControls.Slider(area,"Левый край экрана (%)",character.ScreenEffect.X*100,0,95,v=>{character.ScreenEffect.X=v/100;Changed();scene.RefreshScreenEditor();},"0");
                EditorControls.Slider(area,"Верхний край экрана (%)",character.ScreenEffect.Y*100,0,95,v=>{character.ScreenEffect.Y=v/100;Changed();scene.RefreshScreenEditor();},"0");
                EditorControls.Slider(area,"Ширина экрана (%)",character.ScreenEffect.Width*100,1,100,v=>{character.ScreenEffect.Width=v/100;Changed();scene.RefreshScreenEditor();},"0");
                EditorControls.Slider(area,"Высота экрана (%)",character.ScreenEffect.Height*100,1,100,v=>{character.ScreenEffect.Height=v/100;Changed();scene.RefreshScreenEditor();},"0");
                EditorControls.Slider(reflection,"Прозрачность блика (%)",character.ScreenEffect.Opacity*100,0,60,v=>{character.ScreenEffect.Opacity=v/100;Changed();},"0");
                EditorControls.Slider(reflection,"Скорость блика",character.ScreenEffect.Speed,.01,.5,v=>{character.ScreenEffect.Speed=v;Changed();},"0.00");
                var scan = new CheckBox { Content="Включить scanline-полосы", IsChecked=character.ScreenEffect.Scanlines, Margin=new Thickness(0,6,0,2) }; scan.Checked+=(_,_)=>{character.ScreenEffect.Scanlines=true;Changed();}; scan.Unchecked+=(_,_)=>{character.ScreenEffect.Scanlines=false;Changed();};
                scanPanel.Children.Add(scan);
                EditorControls.Slider(scanPanel,"Прозрачность scanline (%)",character.ScreenEffect.ScanlineOpacity*100,0,50,v=>{character.ScreenEffect.ScanlineOpacity=v/100;Changed();},"0");
                EditorControls.Slider(scanPanel,"Скорость scanline",character.ScreenEffect.ScanlineSpeed,1,60,v=>{character.ScreenEffect.ScanlineSpeed=v;Changed();},"0");
                var flicker = new CheckBox { Content="Лёгкое мерцание", IsChecked=character.ScreenEffect.Flicker, Margin=new Thickness(0,6,0,2) }; flicker.Checked+=(_,_)=>{character.ScreenEffect.Flicker=true;Changed();}; flicker.Unchecked+=(_,_)=>{character.ScreenEffect.Flicker=false;Changed();}; flickerPanel.Children.Add(flicker);
                EditorControls.Slider(flickerPanel,"Сила мерцания (%)",character.ScreenEffect.FlickerAmount*100,0,50,v=>{character.ScreenEffect.FlickerAmount=v/100;Changed();},"0");
                EditorControls.Slider(area,"Скругление углов экрана",character.ScreenEffect.CornerRadius,0,100,v=>{character.ScreenEffect.CornerRadius=v;Changed();scene.RefreshScreenEditor();},"0");
                area.Children.Add(EditorControls.Label("Изгиб сторон: минус — внутрь, плюс — наружу. Откройте выбор области, чтобы видеть изменения."));
                EditorControls.Slider(area,"Верхняя сторона (%)",character.ScreenEffect.CurveTop*100,-25,25,v=>{character.ScreenEffect.CurveTop=v/100;Changed();},"0.0");
                EditorControls.Slider(area,"Нижняя сторона (%)",character.ScreenEffect.CurveBottom*100,-25,25,v=>{character.ScreenEffect.CurveBottom=v/100;Changed();},"0.0");
                EditorControls.Slider(area,"Левая сторона (%)",character.ScreenEffect.CurveLeft*100,-25,25,v=>{character.ScreenEffect.CurveLeft=v/100;Changed();},"0.0");
                EditorControls.Slider(area,"Правая сторона (%)",character.ScreenEffect.CurveRight*100,-25,25,v=>{character.ScreenEffect.CurveRight=v/100;Changed();},"0.0");
                EditorControls.Button(area,"Сбросить изгиб сторон",()=>{character.ScreenEffect.CurveTop=character.ScreenEffect.CurveBottom=character.ScreenEffect.CurveLeft=character.ScreenEffect.CurveRight=0;Changed();Build();});
                EditorControls.Slider(area,"Перспектива по горизонтали",character.ScreenEffect.SkewX,-12,12,v=>{character.ScreenEffect.SkewX=v;Changed();scene.RefreshScreenEditor();},"0.0");
                EditorControls.Slider(area,"Перспектива по вертикали",character.ScreenEffect.SkewY,-12,12,v=>{character.ScreenEffect.SkewY=v;Changed();scene.RefreshScreenEditor();},"0.0");
                EditorControls.Button(area,"Сбросить область экрана",()=>{character.ScreenEffect.X=.25;character.ScreenEffect.Y=.16;character.ScreenEffect.Width=.5;character.ScreenEffect.Height=.42;Changed();Build();});
                EditorControls.Button(screen,"Удалить эффект",()=>{character.ScreenEffect.Added=false;character.ScreenEffect.Enabled=false;character.ScreenEffect.DepthEnabled=false;character.ScreenEffect.Scanlines=false;character.ScreenEffect.Flicker=false;character.ScreenEffect.Glow=false;Changed();Build();});
                Card("Экран","Эффекты экрана").Children.Add(screen);
            }
            if (character.Lighting.Added || character.Lighting.Enabled)
            {
                character.Lighting.Added=true;
                var maps = new StackPanel { Margin=new Thickness(8) };
                var enabled=new CheckBox { Content="Включить освещение",IsChecked=character.Lighting.Enabled };
                enabled.Checked+=(_,_)=>{character.Lighting.Enabled=true;scene.RefreshLighting();save();};
                enabled.Unchecked+=(_,_)=>{scene.EndLightEditing();character.Lighting.Enabled=false;scene.RefreshLighting();save();};
                maps.Children.Add(enabled);
                maps.Children.Add(EditorControls.Label("Добавьте карту нормалей, затем нажмите «Настроить свет» и двигайте лампу по персонажу."));
                void Map(string title,Func<string> get,Action<string> set,string? diffuse=null)
                {
                    var label=EditorControls.Label(string.IsNullOrEmpty(get())?"Карта не загружена":System.IO.Path.GetFileName(get()));
                    EditorControls.Button(maps,title,()=>{
                        var dialog=new OpenFileDialog {Filter="PNG (*.png)|*.png"};
                        if(dialog.ShowDialog()!=true)return;
                        try{
                            var normal=PngFile.Read(dialog.FileName);
                            if(!string.IsNullOrWhiteSpace(diffuse)){
                                var image=PngFile.Read(diffuse);
                                if(normal.PixelWidth!=image.PixelWidth||normal.PixelHeight!=image.PixelHeight){MessageBox.Show("Карта должна совпадать с PNG слоя по размеру: "+image.PixelWidth+" × "+image.PixelHeight);return;}
                            }
                        }catch{MessageBox.Show("Не удалось прочитать PNG карты или слоя.");return;}
                        set(dialog.FileName);label.Text=System.IO.Path.GetFileName(dialog.FileName);
                        scene.RefreshLighting(true);save();
                    });
                    maps.Children.Add(label);
                    EditorControls.Button(maps,"Убрать · "+title,()=>{set("");label.Text="Карта не загружена";scene.RefreshLighting();save();});
                }
                Map("Карта тела",()=>character.Lighting.BodyNormalPath,v=>character.Lighting.BodyNormalPath=v);
                Map("Карта головы / цельного персонажа",()=>character.Lighting.HeadNormalPath,v=>character.Lighting.HeadNormalPath=v);
                maps.Children.Add(EditorControls.Label("У рук каждой позы своя карта. Нужен тот же холст и совпадение рукавов, кистей и устройства. Карта тела автоматически на руки не переносится."));
                foreach(var item in new[]{(Rig:character.Hands.Gamepad,Name:"Руки с геймпадом"),(Rig:character.Hands.Desk,Name:"Руки без геймпада")}){
                    for(int i=0;i<item.Rig.Layers.Count;i++){
                        int index=i;var rig=item.Rig;
                        if(string.IsNullOrWhiteSpace(rig.Layers[i]))continue;
                        Map(item.Name+(rig.Layers.Count==1?"":" · слой "+(i+1)),()=>rig.NormalPath(index),v=>{rig.NormalPaths??=new();while(rig.NormalPaths.Count<=index)rig.NormalPaths.Add("");rig.NormalPaths[index]=v;},rig.Layers[i]);
                    }
                }
                EditorControls.Button(maps,"Настроить свет · двигать лампу",()=>scene.BeginLightEditing(save));
                EditorControls.Button(maps,"Готово · вернуть анимацию",()=>scene.EndLightEditing());
                var status=EditorControls.Label(scene.LightingStatus);maps.Children.Add(status);
                lightingLabel=status;
                var extra=new StackPanel {Margin=new Thickness(8)};
                EditorControls.Section(maps,"Дополнительно").Children.Add(extra);
                var colors=new StackPanel {Orientation=Orientation.Horizontal};
                foreach(var choice in new[]{("Белый","#FFFFFF"),("Тёплый","#FFF4D6"),("Холодный","#D6E8FF")})
                    EditorControls.Button(colors,choice.Item1,()=>{character.Lighting.Color=choice.Item2;scene.RefreshLighting();save();});
                extra.Children.Add(colors);
                EditorControls.Slider(extra,"Высота лампы",character.Lighting.LightHeight,.05,2,v=>{character.Lighting.LightHeight=v;scene.RefreshLighting();save();});
                EditorControls.Slider(extra,"Сила света",character.Lighting.Intensity,0,3,v=>{character.Lighting.Intensity=v;scene.RefreshLighting();save();});
                EditorControls.Slider(extra,"Радиус света",character.Lighting.Radius,.05,2,v=>{character.Lighting.Radius=v;scene.RefreshLighting();save();});
                EditorControls.Button(maps,"Удалить эффект",()=>{scene.EndLightEditing();character.Lighting.Added=false;character.Lighting.Enabled=false;scene.RefreshLighting();save();Build();});
                Card("Освещение","Карта нормалей · лампа").Children.Add(maps);
            }
            if(character.AddedEffectCards.Contains("mouth"))
            {
            var mouth = new StackPanel { Margin = new Thickness(8) };
            mouth.Children.Add(EditorControls.Label("Короткое смешивание изображений при смене рта. Начните с 40–60 мс. Если контуры двоятся, уменьшите время; 0 — мгновенная смена. PNG должны совпадать по положению и размеру. Моргание на цельных PNG остаётся мгновенным."));
            EditorControls.Slider(mouth, "Переход рта (мс)", character.MouthTransitionMs, 0, 120, v=>{character.MouthTransitionMs=v;scene.RefreshVisualSettings();save();}, "0");
            Card("Лицо","Плавность смены рта").Children.Add(mouth);
            EditorControls.Button(mouth,"Удалить эффект",()=>{character.MouthTransitionMs=0;character.AddedEffectCards.Remove("mouth");scene.RefreshVisualSettings();save();Build();});
            }
            if(character.AddedEffectCards.Contains("head"))
            {
                var head=new StackPanel();
                head.Children.Add(new HeadView(character,scene,save,true));
                EditorControls.Button(head,"Удалить эффект",()=>{character.Head.Enabled=false;character.AddedEffectCards.Remove("head");scene.RefreshVisualSettings();save();Build();});
                Card("Движение","Голова и тело").Children.Add(head);
            }
            foreach (var effect in character.Effects.ToArray())
            {
                var type = Types.FirstOrDefault(t => t.Kind == effect.Kind);
                if (type.Kind == null) continue;
                if(ReferenceEquals(effect,expand))newlyAdded=type.Name;
                var body=Card(effect.Kind=="darken"?"Освещение":"Движение",type.Name);
                var enabled=new CheckBox{Content="Включить эффект",IsChecked=effect.Enabled,Margin=new Thickness(0,4,0,8)};body.Children.Add(enabled);
                var summary=EditorControls.Label(WhenLabel(effect.When));body.Children.Add(summary);
                enabled.Checked += (_,_) => { effect.Enabled=true; Changed(); };
                enabled.Unchecked += (_,_) => { effect.Enabled=false; Changed(); };
                body.Children.Add(EditorControls.Label("Когда работает"));
                var when = new ComboBox();
                string[] keys = ["talk","idle","always","start"];
                foreach (var key in keys) when.Items.Add(new ComboBoxItem { Content = WhenLabel(key), Tag=key });
                when.SelectedIndex = Math.Max(0,Array.IndexOf(keys,effect.When));
                when.SelectionChanged += (_,_) => { effect.When=(string)((ComboBoxItem)when.SelectedItem).Tag; summary.Text=WhenLabel(effect.When); Changed(); };
                body.Children.Add(when);
                double factor = effect.Kind == "rotate" ? 15 : effect.Kind == "darken" ? 80 : 100;
                EditorControls.Slider(body, type.Parameter + (effect.Kind == "rotate" ? " (°)" : " (%)"), effect.Amount*factor,0,factor,v=>{effect.Amount=v/factor;Changed();},"0");
                if(effect.Kind!="darken") EditorControls.Slider(body,"Скорость",effect.Speed,.1,5,v=>{effect.Speed=v;Changed();},"0.0");
                var buttons=new StackPanel { Orientation=Orientation.Horizontal };
                EditorControls.Button(buttons,"Проверить",()=>scene.PreviewEffect(effect)).ToolTip="Показать только этот эффект на две секунды без микрофона";
                var remove=EditorControls.Button(buttons,"Убрать",()=>{character.Effects.Remove(effect);scene.CancelEffectPreview();Changed();Build();}); remove.Margin=new Thickness(8,4,0,4);
                body.Children.Add(buttons);
            }
            foreach(var group in groups.Values)if(group.Children.Count==1)panel.Children.Remove(group);
            if(panel.Children.Count==0)panel.Children.Add(EditorControls.Label("Эффектов пока нет. Выберите нужный через «+ Добавить эффект»."));
            newlyAdded=null;
            var add=EditorControls.Button(panel,"+ Добавить эффект",()=>{});
            var picker=new Border{Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,8)};panel.Children.Add(picker);
            add.Click += (_,_) =>
            {
                if(picker.Visibility==Visibility.Visible){picker.Visibility=Visibility.Collapsed;add.Content="+ Добавить эффект";return;}
                var choices=new StackPanel();picker.Child=choices;
                var categories=new Dictionary<string,StackPanel>();
                foreach(var name in new[]{"Лицо","Движение","Экран","Освещение"})categories[name]=EditorControls.Section(choices,name,true);
                void Add(string group,string title,Action action){EditorControls.Button(categories[group],title,()=>{selectedCategory=Array.IndexOf(new[]{"Лицо","Движение","Экран","Освещение"},group);newlyAdded=title;action();});}
                foreach(var entry in new[]{("voiceLights","Точки-индикаторы голоса","Экран"),("processing","Обработка…","Экран"),("glitch","Цифровой сбой","Экран"),("bars","Индикатор голоса","Экран"),("mimic","Живая мимика","Лицо"),("blink","Моргание","Лицо"),("mouth","Плавность смены рта","Лицо"),("pose","Спокойная смена позы","Движение"),("breath","Дыхание","Движение"),("head","Голова и тело","Движение"),("screenLight","Свет от игры","Освещение"),("color","Коррекция цвета","Освещение")})
                {
                    if(character.AddedEffectCards.Contains(entry.Item1) || layerWorkspace && entry.Item1 is "blink" or "screenLight" or "color")continue;
                    Add(entry.Item3,entry.Item2,()=>{character.AddedEffectCards.Add(entry.Item1);save();Build();});
                }
                if(!character.ScreenEffect.Added)Add("Экран","Эффекты экрана",()=>{character.ScreenEffect.Added=true;save();Build();});
                if(!character.Lighting.Added)Add("Освещение","Карта нормалей · лампа",()=>{character.Lighting.Added=true;save();Build();});
                foreach(var type in Types.Where(t=>!character.Effects.Any(e=>e.Kind==t.Kind)))
                    Add(type.Kind=="darken"?"Освещение":"Движение",type.Name,()=>{var effect=new CharacterEffect{Kind=type.Kind,When=type.Kind=="darken"?"idle":type.Kind=="jump"?"start":"talk"};character.Effects.Add(effect);Changed();Build(effect);});
                foreach(var category in categories.Values)if(category.Children.Count==0)category.Children.Add(EditorControls.Label("Всё уже добавлено"));
                picker.Visibility=Visibility.Visible;add.Content="Закрыть выбор эффектов";picker.BringIntoView();
            };
            var footer=panel.Children.Cast<UIElement>().Where(e=>!groups.Values.Contains(e)).ToArray();
            panel.Children.Clear();
            foreach(var group in groups.Values){if(group.Children.Count>0)group.Children.RemoveAt(0);if(group.Children.Count==0)group.Children.Add(EditorControls.Label("В этом разделе пока нет эффектов."));}
            panel.Children.Add(InspectorTabs.Create(groups.Select(g=>(g.Key,(FrameworkElement)g.Value)).ToArray(),selectedCategory,i=>selectedCategory=i));
            foreach(var item in footer)panel.Children.Add(item);
        }
        void ScreenEnded(object? sender, EventArgs e) => Build();
        Loaded += (_,_) => scene.ScreenEditingEnded += ScreenEnded;
        Build(); Unloaded += (_,_)=>{scene.EndRobotPlacement(false);scene.ScreenEditingEnded-=ScreenEnded;scene.EndScreenRegionEditing(false);scene.LightingStatusChanged-=LightingUpdated;scene.EndLightEditing();scene.CancelEffectPreview();scene.StopPosePreview();scene.StopScreenDepthTest();scene.StopIdleExpressionPreview();scene.StopRobotScreenTest();};
    }
    private static string WhenLabel(string key) => key switch { "idle"=>"В тишине", "start"=>"В начале речи", "always"=>"Постоянно", _=>"Во время речи" };
}
