using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dekotube.Services;
using Dekotube.Views;
namespace Dekotube;
public partial class MainWindow
{
    private void CharacterMenu_Click(object sender,RoutedEventArgs e){
        var menu=new ContextMenu();
        void Add(string title,RoutedEventHandler handler){var item=new MenuItem{Header=title};item.Click+=handler;menu.Items.Add(item);}
        Add("Копировать",DuplicateEmotion_Click);Add("Переименовать",EditEmotion_Click);Add("Удалить",DeleteEmotion_Click);
        menu.PlacementTarget=(UIElement)sender;menu.IsOpen=true;
    }
    private int _inspectorEye;
    private int _inspectorMouth;
    private void BuildLayerNavigator(AvatarEmotion character)
    {
        var groups=LayerImages.Children.OfType<Border>().Select(b=>b.Child).OfType<Expander>().ToArray();
        LayerImages.Children.Clear();LegacyImages.Visibility=Visibility.Collapsed;
        var tree=new TreeView{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Brushes.White};LayerImages.Children.Add(tree);
        var layers=new TreeViewItem{Header="ПЕРСОНАЖ",Tag="NavigationGroup",IsExpanded=true};tree.Items.Add(layers);
        void Item(ItemsControl parent,string title,string key,Func<UserControl> create){
            var node=new TreeViewItem{Header=new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap,FontSize=13,Margin=new Thickness(8,9,0,9)},ToolTip="Выбрать слой и открыть свойства справа"};parent.Items.Add(node);
            node.Selected+=(_,e)=>{if(ReferenceEquals(e.OriginalSource,node)){ShowOverlay(key,title,create);e.Handled=true;}};
            node.PreviewMouseLeftButtonUp+=(_,_)=>{if(node.IsSelected)ShowOverlay(key,title,create);};
        }
        UserControl Mouth(FrameworkElement body){
            Detach(body);Detach(LegacyImages);LegacyImages.Visibility=Visibility.Visible;
            return new UserControl{Content=InspectorTabs.Create(new (string,FrameworkElement)[]{("Отдельный рот",body),("PNG-состояния",LegacyImages)},_inspectorMouth,i=>_inspectorMouth=i)};
        }
        var eyeGroups=groups.Where(g=>g.Header?.ToString() is "Левый глаз" or "Правый глаз").ToArray();
        foreach(var group in groups.Where(g=>!eyeGroups.Contains(g))){
            string title=group.Header?.ToString()??"Слой";var body=(StackPanel)group.Content!;group.Content=null;
            if(title=="Рот"){
                Item(layers,title,"layer:Рот",()=>Mouth(body));
                if(_currentOverlayKey=="layer:Рот")OverlayContent.Content=Mouth(body);
                continue;
            }
            Item(layers,title,"layer:"+title,()=>{Detach(body);return new UserControl{Content=body};});
            if(_currentOverlayKey=="layer:"+title){Detach(body);OverlayContent.Content=new UserControl{Content=body};}
        }
        if(eyeGroups.Length==2){
            var bodies=eyeGroups.Select(g=>{var body=(StackPanel)g.Content!;g.Content=null;return body;}).ToArray();
            UserControl Eyes(){
                var panel=new StackPanel();var buttons=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new Thickness(0,0,0,8)};panel.Children.Add(buttons);
                var content=new ContentControl();panel.Children.Add(content);
                void Select(int index){_inspectorEye=index;content.Content=null;Detach(bodies[index]);content.Content=InspectorTabs.Create(new (string,FrameworkElement)[]{("PNG",bodies[index]),("Размеры",new PupilsView(character,AvatarView,SaveVisualSettings,true,index,"Размеры")),("Перспектива",new PupilsView(character,AvatarView,SaveVisualSettings,true,index,"Перспектива")),("Движение",new PupilsView(character,AvatarView,SaveVisualSettings,true,index,"Движение"))});for(int i=0;i<2;i++)((Button)buttons.Children[i]).IsEnabled=i!=index;}
                for(int i=0;i<2;i++){int index=i;var button=new Button{Content=i==0?"Левый глаз":"Правый глаз",Margin=new Thickness(2)};buttons.Children.Add(button);button.Click+=(_,_)=>Select(index);}
                Select(_inspectorEye);return new UserControl{Content=panel};
            }
            Item(layers,"Глаза","layer:Глаза",Eyes);
            if(_currentOverlayKey=="layer:Глаза")OverlayContent.Content=Eyes();
        }


        if(!character.Pupils.Enabled)Item(layers,"Глаза","pupils",()=>new PupilsView(character,AvatarView,SaveVisualSettings));
        if(!character.MouthLayer.Enabled){
            Item(layers,"Рот","layer:Рот",()=>Mouth(new MouthLayerView(character,AvatarView,SaveVisualSettings)));
            if(_currentOverlayKey=="layer:Рот")OverlayContent.Content=Mouth(new MouthLayerView(character,AvatarView,SaveVisualSettings));
        }
        Item(layers,"Руки и устройства","handsRig",()=>new HandsRigView(character,AvatarView,SaveVisualSettings,CreateHandsSample));
        Item(layers,"Одежда","clothing",()=>new ClothingView(character,AvatarView,SaveVisualSettings));
        Item(layers,"Аксессуары","accessories",()=>new AccessoriesView(character,AvatarView,SaveVisualSettings));
        tree.Items.Add(new TreeViewItem { IsEnabled=false, Focusable=false, Header=new Border { Height=1, Width=230, Background=new SolidColorBrush(Color.FromRgb(65,82,85)), Margin=new Thickness(0,12,0,12) } });
        var scene=new TreeViewItem{Header="СЦЕНА",Tag="NavigationGroup",IsExpanded=true};tree.Items.Add(scene);

        Item(scene,"Моргание","blink",()=>new BlinkView(character,AvatarView,SaveVisualSettings));
        Item(scene,"Коррекция цвета","color",()=>new ColorCorrectionView(character,AvatarView,SaveVisualSettings));
        Item(scene,"Свет от игры","screenLight",()=>new ScreenLightView(character,AvatarView,SaveVisualSettings));
        Item(scene,"Эффекты","effects",()=>new EffectsView(character,AvatarView,SaveVisualSettings,true));
        Item(scene,"Фон и OBS","background",()=>new ObsView());

    }
    private static void Detach(FrameworkElement child){
        if(child.Parent is Panel panel)panel.Children.Remove(child);
        else if(child.Parent is ContentControl content)content.Content=null;
        else if(child.Parent is Decorator decorator)decorator.Child=null;
    }
}
