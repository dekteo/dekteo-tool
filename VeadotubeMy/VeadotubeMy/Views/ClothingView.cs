using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;

public sealed class ClothingView : UserControl
{
    public ClothingView(AvatarEmotion character,AvatarView scene,Action save)
    {
        character.Clothing??=new();var o=character.Clothing;
        var panel=new StackPanel{Margin=new Thickness(12)};Content=panel;
        void Apply(){scene.RefreshClothing();save();}
        void Build(){
            panel.Children.Clear();var look=o.Current;
            panel.Children.Add(EditorControls.Label("Оформление существующей одежды: общий цвет для тела и обеих поз рук. Маска отмечается один раз на каждом PNG; принт движется вместе с тканью. Исходные файлы сохраняются."));
            var enabled=new CheckBox{Content="Применять одежду",IsChecked=o.Enabled,Foreground=Brushes.White};panel.Children.Add(enabled);
            enabled.Click+=(_,_)=>{o.Enabled=enabled.IsChecked==true;Apply();};
            var style=EditorControls.Section(panel,"Цвет ткани",true);
            var color=new TextBox{Text=look.Color};style.Children.Add(EditorControls.Label("Цвет HEX (#RRGGBB)"));style.Children.Add(color);
            var swatches=new WrapPanel();style.Children.Add(swatches);
            foreach(var swatch in new[]{"#8C394B","#244F80","#43664A","#E5DFD2","#38383E","#765299"}){
                var button=new Button{Width=32,Height=28,Margin=new Thickness(3),Background=(Brush)new BrushConverter().ConvertFromString(swatch)!,ToolTip=swatch};
                swatches.Children.Add(button);button.Click+=(_,_)=>color.Text=swatch;
            }
            EditorControls.Slider(style,"Светлота ткани",look.Brightness,.2,5,v=>look.Brightness=v,"0.00");
            var status=EditorControls.Label("");style.Children.Add(status);
            EditorControls.Button(style,"Применить цвет и светлоту",()=>{
                try{var parsed=(Color)ColorConverter.ConvertFromString(color.Text);look.Color=$"#{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}";o.Enabled=true;Apply();Build();}
                catch{status.Text="Введите цвет в формате #RRGGBB.";}
            });
            var sources=new List<(string Path,string Label)>();
            void Source(string path,string label){if(!string.IsNullOrWhiteSpace(path)&&sources.All(s=>s.Path!=path))sources.Add((path,label));}
            Source(character.Head.BodyImagePath,"Тело");
            for(int i=0;i<character.Hands.Gamepad.Layers.Count;i++)Source(character.Hands.Gamepad.Layers[i],"Геймпад · слой "+(i+1));
            for(int i=0;i<character.Hands.Desk.Layers.Count;i++)Source(character.Hands.Desk.Layers[i],"Опущенные руки · слой "+(i+1));
            var layers=EditorControls.Section(panel,"Маски ткани и принты",true);
            if(sources.Count==0)layers.Children.Add(EditorControls.Label("Нужны отдельный PNG тела или слои рук. Добавьте их в настройках персонажа."));
            else{
                var selector=new ComboBox{ItemsSource=sources.Select(s=>s.Label+" · "+Path.GetFileName(s.Path)).ToArray(),SelectedIndex=0};layers.Children.Add(selector);
                var controls=new StackPanel();layers.Children.Add(controls);
                void Layer(){
                    controls.Children.Clear();var entry=sources[selector.SelectedIndex];
                    if(!look.Layers.TryGetValue(entry.Path,out var layer))look.Layers[entry.Path]=layer=new();
                    controls.Children.Add(EditorControls.Label(layer.WholeLayer||layer.Mask.Count>0?"Маска задана":"Маски пока нет — этот слой не перекрашивается."));
                    EditorControls.Button(controls,"✎ Выделить ткань кистью",()=>{
                        try{var window=new ClothMaskWindow(PngFile.Read(entry.Path),layer){Owner=Window.GetWindow(this)};
                            if(window.ShowDialog()==true){look.Layers[entry.Path]=window.Result;o.Enabled=true;Apply();Layer();}
                        }catch(Exception ex){MessageBox.Show("Не удалось открыть слой: "+ex.Message);}
                    });
                    controls.Children.Add(EditorControls.Label("Принт: "+(string.IsNullOrWhiteSpace(layer.PrintPath)?"нет":Path.GetFileName(layer.PrintPath))));
                    EditorControls.Button(controls,"+ PNG принта",()=>{
                        var dialog=new OpenFileDialog{Filter="PNG (*.png)|*.png"};if(dialog.ShowDialog()!=true)return;
                        try{PngFile.Read(dialog.FileName);layer.PrintPath=dialog.FileName;Apply();Layer();}catch{MessageBox.Show("Не удалось прочитать PNG.");}
                    });
                    EditorControls.Slider(controls,"Принт · X (%)",layer.PrintX*100,0,100,v=>layer.PrintX=v/100,"0.0");
                    EditorControls.Slider(controls,"Принт · Y (%)",layer.PrintY*100,0,100,v=>layer.PrintY=v/100,"0.0");
                    EditorControls.Slider(controls,"Принт · ширина (%)",layer.PrintWidth*100,.5,100,v=>layer.PrintWidth=v/100,"0.0");
                    EditorControls.Slider(controls,"Принт · поворот (°)",layer.PrintAngle,-180,180,v=>layer.PrintAngle=v,"0");
                    EditorControls.Button(controls,"Применить положение принта",Apply);
                    EditorControls.Button(controls,"Убрать принт",()=>{layer.PrintPath="";Apply();Layer();});
                }
                selector.SelectionChanged+=(_,_)=>Layer();Layer();
            }
            var presets=EditorControls.Section(panel,"Сохранённые комплекты",true);
            var name=new TextBox{Text=look.Name};presets.Children.Add(name);
            EditorControls.Button(presets,"Сохранить новым комплектом",()=>{look.Name=string.IsNullOrWhiteSpace(name.Text)?"Комплект":name.Text.Trim();o.Looks.Add(look.Copy());save();Build();});
            foreach(var preset in o.Looks.ToArray()){
                EditorControls.Button(presets,"Надеть · "+preset.Name,()=>{o.Current=preset.Copy();o.Enabled=true;Apply();Build();});
                EditorControls.Button(presets,"Удалить комплект · "+preset.Name,()=>{o.Looks.Remove(preset);save();Build();});
            }
        }
        Build();
    }
}
