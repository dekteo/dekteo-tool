using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dekotube.Services;
using Dekotube.Views;

namespace Dekotube;

public partial class MainWindow
{
    private AvatarEmotion? _layerCharacter;
    private string? _layerSignature;
    private void BuildLayerImages()
    {
        if (LayerImages == null || LegacyImages == null) return;
        if (_selectedEmotion is not { } character) return;
        var m = character.MouthLayer;
        var signature = System.Text.Json.JsonSerializer.Serialize(new {
            m.Enabled, m.Procedural, m.ProceduralVowels, m.UseStripPng,
            m.BasePath, m.BlinkBasePath, m.ClosedPath, m.OpenPath, m.StripPath, m.VowelPaths,
            Eyes = character.Pupils.Enabled, Head = character.Head.Enabled, character.Head.BodyImagePath,
            character.HiddenParts, LeftHidden=character.Pupils.Left.HiddenLayers, RightHidden=character.Pupils.Right.HiddenLayers,
            EyePaths = new[] { character.Pupils.Left, character.Pupils.Right }.Select(e => new[] {
                e.ImagePath,e.WhiteImagePath,e.ForegroundImagePath,e.HalfClosedImagePath,e.ClosedEyeImagePath,e.MaskImagePath })
        });
        if (ReferenceEquals(_layerCharacter,character) && _layerSignature == signature) return;
        _layerCharacter = character; _layerSignature = signature;
        var expanded = LayerImages.Children.OfType<Border>().Select(b => b.Child).OfType<Expander>()
            .Where(e => e.IsExpanded).Select(e => e.Header?.ToString()).ToHashSet();
        bool first = LayerImages.Children.Count == 0;
        LayerImages.Children.Clear();
        LegacyImages.Visibility = m.Enabled ? Visibility.Collapsed : Visibility.Visible;
        StackPanel Group(string title, bool open = false)
        {
            var body = new StackPanel { Margin = new Thickness(0,8,0,0) };
            LayerImages.Children.Add(new Border {
                Padding = new Thickness(8), Margin = new Thickness(0,0,0,8), CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(79,99,115)), Background = new SolidColorBrush(Color.FromRgb(25,33,43)),
                Child = new Expander { Header = title, Content = body, IsExpanded = expanded.Contains(title) || first && open }
            });
            return body;
        }
        void Settings(Panel panel, string key, string title, Func<UserControl> view)
        {
            var properties=new Expander{Header="Поведение и настройки",Margin=new Thickness(0,10,0,0)};
            properties.Expanded+=(_,_)=>{if(properties.Content==null){var editor=view();InspectorTabs.Organize(editor);properties.Content=editor;}};panel.Children.Add(properties);
        }
        void Changed()
        {
            AvatarView.EndMouthCurveEditing(false);
            AvatarView.RefreshVisualSettings(); AvatarView.RefreshFromService();
            SaveVisualSettings(); BuildLayerImages();
        }
        var cardTools = new Dictionary<Panel, System.Windows.Controls.Primitives.UniformGrid>();
        void Actions(Panel panel, Func<bool> visible, Action<bool> setVisible, Action place, string placeLabel = "Разместить")
        {
            var actions=new Grid { Margin=new Thickness(0,3,0,8) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(32) });actions.ColumnDefinitions.Add(new ColumnDefinition());
            var toggle=new System.Windows.Controls.Primitives.ToggleButton { IsChecked=visible(),Style=(Style)FindResource("LayerVisibilityToggle") };
            toggle.Click+=(_,_)=>{setVisible(toggle.IsChecked==true);Changed();};
            if(cardTools.Remove(panel,out var tools)){
                toggle.IsEnabled=((System.Windows.Controls.Primitives.ToggleButton)tools.Children[0]).IsEnabled;
                toggle.Width=double.NaN;toggle.Margin=new Thickness(0,0,3,0);tools.Children.RemoveAt(0);tools.Children.Insert(0,toggle);
                var move=(Button)tools.Children[1];move.IsEnabled=toggle.IsEnabled&&visible();move.ToolTip=visible()?"Переместить слой":"Сначала покажите слой";
                move.Tag=place;
                return;
            }
            else actions.Children.Add(toggle);
            var edit=new Button { Content=placeLabel,Height=30,Margin=new Thickness(6,0,0,0),Padding=new Thickness(5,0,5,0),IsEnabled=visible(),ToolTip=visible()?"Открыть размещение на сцене":"Сначала покажите слой" };
            edit.Click+=(_,_)=>place();Grid.SetColumn(edit,1);actions.Children.Add(edit);panel.Children.Add(actions);
        }
        void PartActions(Panel panel,string key,bool head=false)
        {
            Actions(panel,()=>character.HiddenParts?.Contains(key)!=true,v=>{character.HiddenParts??=new();character.HiddenParts.Remove(key);if(!v)character.HiddenParts.Add(key);},
                ()=>ShowOverlay("placement:"+key,head?"Переместить голову":"Переместить тело",()=>new PlacementView(character,AvatarView,SaveVisualSettings,head,key=="body")),"Переместить слой");
        }
        void FileRow(Panel panel, string title, Func<string> get, Action<string> set)
        {
            bool hasFile=!string.IsNullOrEmpty(get());
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(112)});row.ColumnDefinitions.Add(new ColumnDefinition());
            var card=new Border{Child=row,Padding=new Thickness(8),Margin=new Thickness(0,3,0,7),Background=new SolidColorBrush(Color.FromRgb(26,30,34)),BorderBrush=new SolidColorBrush(Color.FromRgb(57,67,75)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)};panel.Children.Add(card);
            var preview=new Border{Height=96,Margin=new Thickness(0,0,10,0),Background=new SolidColorBrush(Color.FromRgb(17,20,23)),CornerRadius=new CornerRadius(5),ToolTip=title};row.Children.Add(preview);
            var bitmap=hasFile?LoadBitmap(get()):null;
            preview.Child=bitmap==null?new TextBlock{Text="PNG",Foreground=Brushes.Gray,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}:new Image{Source=bitmap,Stretch=Stretch.Uniform,Margin=new Thickness(4)};
            var right=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(right,1);row.Children.Add(right);
            string prompt=title switch { "Голова без рта"=>"Добавить PNG головы", "Тело без головы"=>"Добавить PNG тела", "Зрачок"=>"Добавить PNG зрачка", "Белок"=>"Добавить PNG белка", _=>"Добавить PNG · "+title };
            var upload=new Button{Content=new TextBlock{Text=hasFile?System.IO.Path.GetFileName(get()):prompt,TextTrimming=TextTrimming.CharacterEllipsis},Height=38,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,4,10,4),ToolTip=hasFile?title+"\n"+get()+"\nНажмите, чтобы заменить PNG":prompt};right.Children.Add(upload);
            upload.Click+=(_,_)=>{var path=SelectPngFile();if(string.IsNullOrEmpty(path)||LoadBitmap(path)==null)return;set(path);AvatarView.ReloadImageFiles();_layerSignature=null;Changed();};
            var tools=new System.Windows.Controls.Primitives.UniformGrid{Columns=4,Margin=new Thickness(0,8,0,0)};right.Children.Add(tools);cardTools[panel]=tools;
            Button Icon(string symbol,string hint,Action action){var button=new Button{Content=symbol,Height=30,Padding=new Thickness(0),Margin=new Thickness(3,0,3,0),FontSize=18,ToolTip=hint,IsEnabled=hasFile};System.Windows.Automation.AutomationProperties.SetName(button,hint);button.Click+=(_,_)=>action();tools.Children.Add(button);return button;}
            string part=title=="Основа · закрытые глаза"?"base":"mouth";
            var visibility=new System.Windows.Controls.Primitives.ToggleButton{Style=(Style)FindResource("LayerVisibilityToggle"),IsChecked=character.HiddenParts?.Contains(part)!=true,IsEnabled=hasFile,Width=double.NaN,Margin=new Thickness(0,0,3,0)};
            visibility.Click+=(_,_)=>{character.HiddenParts??=new();character.HiddenParts.Remove(part);if(visibility.IsChecked!=true)character.HiddenParts.Add(part);Changed();};tools.Children.Add(visibility);
            var move=Icon("✥","Разместить слой",()=>{});
            move.Tag=(Action)(()=>{if(part=="base")ShowOverlay("placement","Разместить персонажа",()=>new PlacementView(character,AvatarView,SaveVisualSettings));else ShowOverlay("mouthPlacement","Разместить рот",()=>new MouthPlacementView(character,AvatarView,SaveVisualSettings));});
            move.Click+=(_,_)=>((Action)move.Tag)();
            Icon("↻","Обновить PNG с диска после редактирования",()=>{AvatarView.ReloadImageFiles();_layerSignature=null;Changed();});
            Icon("×","Убрать PNG из слоя. Файл останется на диске",()=>{set("");Changed();}).Margin=new Thickness(3,0,0,0);
            if(!hasFile){
                preview.Visibility=Visibility.Collapsed;
                row.ColumnDefinitions[0].Width=new GridLength(0);
                tools.Visibility=Visibility.Collapsed;
                card.Padding=new Thickness(0);
                card.BorderThickness=new Thickness(0);
                card.Background=Brushes.Transparent;
                upload.Height=40;
                upload.Content=new TextBlock{Text="+  "+prompt,TextTrimming=TextTrimming.CharacterEllipsis};
            }
        }

        if (m.Enabled)
        {
            var basis = Group("Основа персонажа", true);
            basis.Children.Add(new TextBlock { Text = character.Head.Enabled ? "PNG головы без рта. Тело загружается отдельно." : "PNG персонажа без нарисованного рта.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,6) });
            FileRow(basis,character.Head.Enabled ? "Голова без рта" : "Основа без рта",()=>m.BasePath,v=>m.BasePath=v);
            PartActions(basis,"base",character.Head.Enabled);
            if (character.Head.Enabled)
            {
                FileRow(basis,"Тело без головы",()=>character.Head.BodyImagePath,v=>character.Head.BodyImagePath=v);
                PartActions(basis,"body");

            }
            if (!character.Pupils.Enabled) FileRow(basis,"Основа · закрытые глаза",()=>m.BlinkBasePath,v=>m.BlinkBasePath=v);
            var mouth = Group("Рот", true);
            var mouthModes=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new Thickness(0,0,0,8)};mouth.Children.Add(mouthModes);
            foreach(var choice in new[]{("Рот PNG",false),("Рот полоской",true)}){
                var button=new Button{Content=choice.Item1,Margin=new Thickness(2),IsEnabled=m.Procedural!=choice.Item2};mouthModes.Children.Add(button);
                button.Click+=(_,_)=>{m.Procedural=choice.Item2;Changed();};
            }
            Actions(mouth,()=>character.HiddenParts?.Contains("mouth")!=true,v=>{character.HiddenParts??=new();character.HiddenParts.Remove("mouth");if(!v)character.HiddenParts.Add("mouth");},
                ()=>ShowOverlay("mouthPlacement","Разместить рот",()=>new MouthPlacementView(character,AvatarView,SaveVisualSettings)));
            if (m.Procedural)
            {
                if (m.UseStripPng) FileRow(mouth,"Моя полоска PNG",()=>m.StripPath,v=>m.StripPath=v);
                else mouth.Children.Add(new TextBlock { Text = "Полоска рисуется программой. PNG не нужен.", TextWrapping = TextWrapping.Wrap });
                if (m.ProceduralVowels)
                {
                    mouth.Children.Add(new TextBlock { Text = "Форма рта для звуков", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,12,0,4) });
                    mouth.Children.Add(new TextBlock { Text = "Выберите букву, чтобы настроить, как выглядит рот при произнесении этого звука.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, Margin = new Thickness(0,0,0,6) });
                    var forms = new WrapPanel(); mouth.Children.Add(forms);
                    foreach (var sound in Enum.GetValues<VowelShape>().Where(v => v != VowelShape.Unknown))
                    {
                        var shape = sound;
                        EditorControls.Button(forms,VowelDiagnostic.Label(shape),()=> {
                            if (!AvatarView.BeginMouthCurveEditing(SaveVisualSettings, shape)) ShowToast("Сначала загрузите основу без рта");
                        });
                    }
                }
            }
            else
            {
                FileRow(mouth,"Закрытый рот",()=>m.ClosedPath,v=>m.ClosedPath=v);
                FileRow(mouth,"Открытый рот",()=>m.OpenPath,v=>m.OpenPath=v);
                foreach (var sound in Enum.GetValues<VowelShape>().Where(v => v != VowelShape.Unknown))
                {
                    var shape = sound;
                    if (!m.VowelPaths.ContainsKey(shape)) continue;
                    FileRow(mouth,"Рот " + VowelDiagnostic.Label(shape),()=>m.VowelPaths.GetValueOrDefault(shape,""),v=>{if(v=="")m.VowelPaths.Remove(shape);else m.VowelPaths[shape]=v;});
                }
                EditorControls.Button(mouth,"+ Добавить звук",()=> {
                    var menu = new ContextMenu();
                    foreach(var sound in Enum.GetValues<VowelShape>().Where(v => v != VowelShape.Unknown).Where(s=>!m.VowelPaths.ContainsKey(s)))
                    {
                        var shape=sound; var item=new MenuItem { Header=VowelDiagnostic.Label(shape) };
                        item.Click+=(_,_)=>{var path=SelectPngFile();if(string.IsNullOrEmpty(path)||LoadBitmap(path)==null)return;m.VowelPaths[shape]=path;Changed();};
                        menu.Items.Add(item);
                    }
                    menu.IsOpen=true;
                });
            }
            Settings(mouth,"mouthLayer","Рот · отдельный слой",()=>new MouthLayerView(character,AvatarView,SaveVisualSettings,true));
        }
        if (character.Pupils.Enabled)
        {
            foreach (var pair in new[] { ("Левый глаз",character.Pupils.Left), ("Правый глаз",character.Pupils.Right) })
            {
                var eye=pair.Item2; var group=Group(pair.Item1);
                void EyeActions(string key) => Actions(group,()=>eye.IsLayerVisible(key),v=>eye.SetLayerVisible(key,v),()=>{HideOverlay();AvatarView.EndPlacement();AvatarView.EditEyeRegion(eye==character.Pupils.Left?0:1);},"Область глаза");
                FileRow(group,"Зрачок",()=>eye.ImagePath,v=>{eye.ImagePath=v;eye.SetLayerVisible("pupil",true);});
                EyeActions("pupil");
                FileRow(group,"Белок",()=>eye.WhiteImagePath,v=>{eye.WhiteImagePath=v;eye.SetLayerVisible("white",true);});
                EyeActions("white");
                FileRow(group,"Контур / открытые веки",()=>eye.ForegroundImagePath,v=>{eye.ForegroundImagePath=v;eye.SetLayerVisible("contour",true);});
                EyeActions("contour");
                FileRow(group,"Полуприкрытое веко",()=>eye.HalfClosedImagePath,v=>{eye.HalfClosedImagePath=v;eye.SetLayerVisible("half",true);});
                EyeActions("half");
                FileRow(group,"Закрытое веко",()=>eye.ClosedEyeImagePath,v=>{eye.ClosedEyeImagePath=v;eye.SetLayerVisible("closed",true);});
                EyeActions("closed");
                FileRow(group,"Маска области",()=>eye.MaskImagePath,v=>{eye.MaskImagePath=v;eye.SetLayerVisible("mask",true);});
                EyeActions("mask");

            }
        }
        if (character.Head.Enabled && !m.Enabled)
        {
            var body=Group("Тело");
            FileRow(body,"Тело без головы",()=>character.Head.BodyImagePath,v=>character.Head.BodyImagePath=v);
            PartActions(body,"body");

        }
        BuildLayerNavigator(character);
    }
}

