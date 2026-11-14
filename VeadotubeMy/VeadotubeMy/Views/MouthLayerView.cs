using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views;

public sealed class MouthLayerView : UserControl
{
    public MouthLayerView(AvatarEmotion character, AvatarView scene, Action save, bool imagesInSidebar = false)
    {
        var m = character.MouthLayer;
        var panel = new StackPanel { Margin = new Thickness(12) }; Content = panel;
        void Changed() { scene.EndMouthCurveEditing(false); scene.RefreshVisualSettings(); scene.RefreshFromService(); save(); }
        StackPanel Section(Panel owner,string title,bool expanded=false)
        {
            return EditorControls.Section(owner,title,expanded);
        }
        panel.Children.Add(EditorControls.Label("Рот поверх персонажа · настройте вид, затем движение."));
        var enabled = new CheckBox { Content = "Использовать отдельный рот", IsChecked = m.Enabled, Margin = new Thickness(0,8,0,8) };
        enabled.Checked += (_,_) => { m.Enabled = true; Changed(); };
        enabled.Unchecked += (_,_) => { m.Enabled = false; Changed(); };
        panel.Children.Add(enabled);
        var calibration=new Expander { Header="Обучить гласные моему голосу", Margin=new Thickness(0,6,0,6) };
        calibration.Expanded += (_,_)=>calibration.Content=new VoiceCalibrationView();
        calibration.Collapsed += (_,_)=>calibration.Content=null;
        panel.Children.Add(EditorControls.Label("Вид рта"));
        var mode = new ComboBox { ItemsSource = new[] { "Из PNG", "Процедурный · светящаяся полоска" }, SelectedIndex = m.Procedural ? 1 : 0, Margin = new Thickness(0,4,0,8) };
        panel.Children.Add(mode);
        var basis=Section(panel,"Основа персонажа",string.IsNullOrEmpty(m.BasePath));
        basis.Children.Add(EditorControls.Label("Загрузите персонажа или голову без нарисованного рта."));
        var pngPanel = new StackPanel();
        var proceduralPanel = new StackPanel();
        void File(string title, Func<string> get, Action<string> set, Panel? parent = null)
        {
            if (imagesInSidebar) return;
            parent ??= basis;
            var row=new Grid { Margin=new Thickness(0,3,0,3) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var label=new TextBlock { Text=title, TextTrimming=TextTrimming.CharacterEllipsis, VerticalAlignment=VerticalAlignment.Center, ToolTip=get() };
            row.Children.Add(label);
            var actions=new StackPanel { Orientation=Orientation.Horizontal }; Grid.SetColumn(actions,1); row.Children.Add(actions); parent.Children.Add(row);
            var upload=EditorControls.Button(actions, string.IsNullOrEmpty(get()) ? "Загрузить" : "Заменить", () =>
            {
                var dialog = new OpenFileDialog { Filter = "PNG|*.png" };
                if (dialog.ShowDialog() != true) return;
                try {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions=System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                    bitmap.UriSource = new Uri(dialog.FileName); bitmap.EndInit();
                    scene.ReloadImageFiles();
                    set(dialog.FileName); label.ToolTip=get(); Changed();
                } catch { MessageBox.Show("Не удалось прочитать PNG."); }
            });
            upload.Padding=new Thickness(7,3,7,3); upload.MinHeight=28; upload.Margin=new Thickness(5,0,3,0);
            var remove=EditorControls.Button(actions,"×",()=>{ set(""); label.ToolTip="Не загружен"; upload.Content="Загрузить"; Changed(); });
            remove.ToolTip="Убрать: "+title; remove.Width=28; remove.MinHeight=28; remove.Margin=new Thickness(0);
            upload.Click+=(_,_)=>{upload.Content=string.IsNullOrEmpty(get())?"Загрузить":"Заменить";remove.IsEnabled=!string.IsNullOrEmpty(get());};
            remove.Click+=(_,_)=>remove.IsEnabled=!string.IsNullOrEmpty(get()); remove.IsEnabled=!string.IsNullOrEmpty(get());
        }
        File("Основа без рта", () => m.BasePath, v => m.BasePath = v);
        File("Закрытые глаза · необязательно", () => m.BlinkBasePath, v => m.BlinkBasePath = v);
        panel.Children.Add(pngPanel);
        panel.Children.Add(proceduralPanel);
        var pngFiles=Section(pngPanel,"Изображения рта",true);
        File("Закрытый рот", () => m.ClosedPath, v => m.ClosedPath = v, pngFiles);
        File("Открытый рот", () => m.OpenPath, v => m.OpenPath = v, pngFiles);
        var vowels = Section(pngPanel,"PNG для звуков · необязательно");
        vowels.Children.Add(EditorControls.Label("Добавляйте только нужные звуки. Во время речи выбранная форма плавно смешивается с обычным открытым ртом. Если PNG звука нет, используется обычный рот."));
        var vowelNames = new Dictionary<VowelShape,string> { [VowelShape.A]="А", [VowelShape.O]="О", [VowelShape.U]="У", [VowelShape.IE]="И / Е" };
        foreach (var pair in vowelNames)
        {
            var shape = pair.Key;
            string Path() => m.VowelPaths.TryGetValue(shape, out var path) ? path : "";
            File(pair.Value,Path,v=>{ if(string.IsNullOrEmpty(v))m.VowelPaths.Remove(shape);else m.VowelPaths[shape]=v; },vowels);
        }
        vowels.Children.Add(EditorControls.Label("Одинаковый прозрачный холст и положение рта для всех PNG."));
        if (imagesInSidebar)
        {
            ((FrameworkElement)((FrameworkElement)basis.Parent).Parent).Visibility = Visibility.Collapsed;
            ((FrameworkElement)((FrameworkElement)pngFiles.Parent).Parent).Visibility = Visibility.Collapsed;
            ((FrameworkElement)((FrameworkElement)vowels.Parent).Parent).Visibility = Visibility.Collapsed;
            panel.Children.Add(EditorControls.Label("PNG загружаются в панели «Изображения и слои»."));
        }
        var appearance=Section(proceduralPanel,"Внешний вид");
        var motion=Section(proceduralPanel,"Движение и раскрытие");
        var forms=Section(proceduralPanel,"Формы звуков",true);
        void Toggle(Panel owner,string title, bool value, Action<bool> set)
        {
            var box = new CheckBox { Content = title, IsChecked = value, Margin = new Thickness(0,6,0,6) };
            box.Checked += (_,_) => { set(true); Changed(); };
            box.Unchecked += (_,_) => { set(false); Changed(); };
            owner.Children.Add(box);
        }
        var stripFiles=new StackPanel(); var drawnAppearance=new StackPanel();
        Toggle(appearance,"Мой PNG полоски", m.UseStripPng, v => { m.UseStripPng=v; stripFiles.Visibility=v?Visibility.Visible:Visibility.Collapsed; drawnAppearance.Visibility=v?Visibility.Collapsed:Visibility.Visible; });
        appearance.Children.Add(stripFiles); appearance.Children.Add(drawnAppearance);
        stripFiles.Visibility=m.UseStripPng?Visibility.Visible:Visibility.Collapsed; drawnAppearance.Visibility=m.UseStripPng?Visibility.Collapsed:Visibility.Visible;
        File("Полоска PNG", () => m.StripPath, v => m.StripPath=v, stripFiles);
        stripFiles.Children.Add(EditorControls.Label("Обрежьте прозрачные поля. Без PNG используется рисуемая полоска."));
        Toggle(drawnAppearance,"Светлая середина", m.HighlightEnabled, v => m.HighlightEnabled=v);
        var glowSettings=new StackPanel();
        Toggle(appearance,"Дополнительное свечение", m.GlowEnabled, v => { m.GlowEnabled=v; glowSettings.Visibility=v?Visibility.Visible:Visibility.Collapsed; });
        appearance.Children.Add(glowSettings); glowSettings.Visibility=m.GlowEnabled?Visibility.Visible:Visibility.Collapsed;
        Toggle(motion,"Изгиб", m.BendEnabled, v => m.BendEnabled=v);
        Toggle(motion,"Растяжение на слогах", m.StretchEnabled, v => m.StretchEnabled=v);
        EditorControls.Button(motion, "Редактировать форму на персонаже", () => {
            if (!scene.BeginMouthCurveEditing(save)) MessageBox.Show("Включите отдельный процедурный рот и загрузите основу без рта.");
        });
        motion.Children.Add(EditorControls.Label("Голубые ручки — размер, жёлтые — изгиб. Сохраните форму кнопкой «Применить»."));
        var sounds = new CheckBox { Content = "Менять форму под гласные А / О / У / И / Э", IsChecked = m.ProceduralVowels, Margin = new Thickness(0,8,0,8) };
        forms.Children.Add(sounds);
        sounds.ToolTip="Выключите для реакции только на громкость. Распознавание может ошибаться.";
        sounds.Checked += (_,_) => { m.ProceduralVowels = true; Changed(); };
        sounds.Unchecked += (_,_) => { m.ProceduralVowels = false; Changed(); };
        forms.Children.Add(EditorControls.Label("Выберите звук для редактирования и теста:"));
        var formButtons=new WrapPanel(); forms.Children.Add(formButtons);
        foreach(var sound in VoiceCalibration.Sounds)
        {
            var shape=VoiceCalibration.ShapeFor(sound);
            var button=new Button { Content=sound, MinWidth=38, Margin=new Thickness(3), Padding=new Thickness(8,5,8,5) };
            button.Click += (_,_)=>{ if(!scene.BeginMouthCurveEditing(save,shape)) MessageBox.Show("Включите отдельный процедурный рот и загрузите основу без рта."); };
            formButtons.Children.Add(button);
        }
        drawnAppearance.Children.Add(EditorControls.Label("Цвет полоски"));
        var colors = new ComboBox { ItemsSource = new[] { "Бирюзовый", "Белый", "Голубой", "Розовый", "Зелёный", "Свой цвет" } };
        var codes = new[] { "#58F4FF", "#FFFFFF", "#64A9FF", "#FF7CD6", "#8AFF98" };
        int colorIndex = Array.FindIndex(codes, c => string.Equals(c, m.Color, StringComparison.OrdinalIgnoreCase));
        colors.SelectedIndex = colorIndex < 0 ? 5 : colorIndex;
        drawnAppearance.Children.Add(colors);
        var custom = new TextBox { Text = m.Color, Margin = new Thickness(0,4,0,4), ToolTip = "Цвет в формате #RRGGBB" }; drawnAppearance.Children.Add(custom);
        EditorControls.Button(drawnAppearance, "Применить цвет", () => {
            try { _ = System.Windows.Media.ColorConverter.ConvertFromString(custom.Text); m.Color = custom.Text; Changed(); }
            catch { MessageBox.Show("Укажите цвет в формате #RRGGBB, например #58F4FF."); }
        });
        colors.SelectionChanged += (_,_) => { if (colors.SelectedIndex < 0 || colors.SelectedIndex >= codes.Length) return; m.Color = codes[colors.SelectedIndex]; custom.Text = m.Color; Changed(); };
        EditorControls.Slider(motion, "Толщина в тишине (%)", m.RestThickness*100, 2, 40, v => { m.RestThickness=v/100; Changed(); }, "0");
        EditorControls.Slider(motion, "Базовое раскрытие (%)", m.MaxOpening*100, 2, 100, v => { m.MaxOpening=v/100; Changed(); }, "0").ToolTip="Ручная высота отдельного звука имеет приоритет.";
        EditorControls.Slider(drawnAppearance, "Скругление концов (%)", m.Roundness*200, 0, 100, v => { m.Roundness=v/200; Changed(); }, "0");
        EditorControls.Slider(motion, "Живость движения (%)", m.Liveliness*100, 0, 100, v => { m.Liveliness=v/100; Changed(); }, "0");
        var influenceSlider=EditorControls.Slider(forms, "Влияние гласных (%)", m.VowelInfluence*100, 0, 100, v => { m.VowelInfluence=v/100; Changed(); }, "0");
        EditorControls.Button(forms,"Полная выразительность · 100%",()=>{ influenceSlider.Value=100; });
        EditorControls.Slider(glowSettings, "Сила свечения", m.Glow, 0, 40, v => { m.Glow=v; Changed(); }, "0");
        void ShowMode() { pngPanel.Visibility = m.Procedural ? Visibility.Collapsed : Visibility.Visible; proceduralPanel.Visibility = m.Procedural ? Visibility.Visible : Visibility.Collapsed; }
        ShowMode();
        mode.SelectionChanged += (_,_) => { m.Procedural = mode.SelectedIndex == 1; ShowMode(); Changed(); };
        var placement = Section(panel,"Положение и размер");
        EditorControls.Slider(placement, "По горизонтали (%)", m.X*100, 0, 100, v => { m.X=v/100; Changed(); }, "0.0");
        EditorControls.Slider(placement, "По вертикали (%)", m.Y*100, 0, 100, v => { m.Y=v/100; Changed(); }, "0.0");
        EditorControls.Slider(placement, "Ширина (%)", m.Width*100, 1, 100, v => { m.Width=v/100; Changed(); }, "0.0");
        EditorControls.Slider(placement, "Высота (%)", m.Height*100, 1, 100, v => { m.Height=v/100; Changed(); }, "0.0");
        var voice=Section(panel,"Реакция на голос и обучение");
        EditorControls.Slider(voice, "Полное раскрытие при громкости (%)", m.FullVolume, 1, 100, v => { m.FullVolume=v; Changed(); }, "0");
        voice.Children.Add(EditorControls.Label("Значение должно быть выше порога микрофона.")); voice.Children.Add(calibration);
        var preview = new Button { Content = "▶ Тест без голоса", MinHeight = 32, Margin = new Thickness(0,4,0,6) }; panel.Children.Insert(4,preview);
        preview.Click += (_,_) => {
            if (!m.Enabled || string.IsNullOrWhiteSpace(m.BasePath)) { MessageBox.Show("Включите отдельный рот и загрузите основу без рта."); return; }
            scene.MouthLayerPreview = !scene.MouthLayerPreview;
            preview.Content = scene.MouthLayerPreview ? "■ Остановить тест" : "▶ Тест без голоса";
            scene.UpdateRenderingSubscription();
        };
        Unloaded += (_,_) => { scene.EndMouthCurveEditing(false); scene.MouthLayerPreview=false; scene.UpdateRenderingSubscription(); };
    }
}
