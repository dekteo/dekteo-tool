using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Dekotube.Views;
internal static class InspectorTabs
{
    public static void Organize(UserControl view)
    {
        if(view is AccessoriesView || view is MouthLayerView || view is HandsRigView)return;
        void Visit(DependencyObject element){
            if(element is Panel panel){
                foreach(var child in panel.Children.Cast<UIElement>().ToArray())Visit(child);
                var sections=panel.Children.OfType<Border>().Where(b=>b.Child is Expander).ToArray();
                if(sections.Length<2)return;
                var pages=sections.Select(border=>{var expander=(Expander)border.Child;string title=expander.Header is TextBlock text?text.Text:expander.Header?.ToString()??"Настройки";var body=(FrameworkElement)expander.Content;expander.Content=null;panel.Children.Remove(border);return(title,body);}).ToArray();
                panel.Children.Add(Create(pages));
            }else if(element is UserControl control && control.Content is DependencyObject content)Visit(content);
        }
        Visit(view);
    }
    public static FrameworkElement Create((string Title, FrameworkElement Body)[] pages, int selected=0, Action<int>? changed=null)
    {
        var root=new DockPanel{LastChildFill=true};
        var bar=new UniformGrid{Columns=Math.Min(4,pages.Length),Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=520};root.Children.Add(scroll);
        void Resize(){var window=Window.GetWindow(root);if(window!=null)scroll.MaxHeight=Math.Max(120,window.ActualHeight-240);}
        root.Loaded+=(_,_)=>Resize();root.SizeChanged+=(_,_)=>Resize();
        void Select(int index){scroll.Content=null;scroll.Content=pages[index].Body;scroll.ScrollToTop();for(int i=0;i<bar.Children.Count;i++)((Button)bar.Children[i]).IsEnabled=i!=index;changed?.Invoke(index);}
        for(int i=0;i<pages.Length;i++){int index=i;var button=new Button{Content=new TextBlock{Text=pages[i].Title,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center},MinHeight=30,Margin=new Thickness(2),Padding=new Thickness(5,3,5,3)};bar.Children.Add(button);button.Click+=(_,_)=>Select(index);}
        Select(Math.Clamp(selected,0,pages.Length-1));return root;
    }
}
