using System.Windows;
using System.Windows.Controls;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public sealed class MouthPlacementView : UserControl
{
    public MouthPlacementView(AvatarEmotion character,AvatarView scene,Action save)
    {
        var m=character.MouthLayer;var snapshot=(m.X,m.Y,m.Width,m.Height,m.Rotation,m.PerspectiveYaw,m.PerspectivePitch);bool finished=false,sync=false;
        var panel=new StackPanel {Margin=new Thickness(12)};Content=panel;
        panel.Children.Add(EditorControls.Label("Перетаскивайте на сцене — рот перемещается. Колесо меняет размер; ширину и высоту можно настроить отдельно. Голубая рамка показывает область рта."));
        void Changed(){if(!sync)scene.RefreshMouthPlacement();}
        var x=EditorControls.Slider(panel,"По горизонтали (%)",m.X*100,0,100,v=>{if(!sync){m.X=v/100;Changed();}},"0.0");
        var y=EditorControls.Slider(panel,"По вертикали (%)",m.Y*100,0,100,v=>{if(!sync){m.Y=v/100;Changed();}},"0.0");
        var w=EditorControls.Slider(panel,"Ширина (%)",m.Width*100,1,100,v=>{if(!sync){m.Width=v/100;Changed();}},"0.0");
        var h=EditorControls.Slider(panel,"Высота (%)",m.Height*100,1,100,v=>{if(!sync){m.Height=v/100;Changed();}},"0.0");
        var roll=EditorControls.Slider(panel,"Наклон рта (°)",m.Rotation,-90,90,v=>{m.Rotation=v;Changed();},"0.0");
        var yaw=EditorControls.Slider(panel,"Поворот в глубину · влево / вправо (°)",m.PerspectiveYaw,-60,60,v=>{m.PerspectiveYaw=v;Changed();},"0.0");
        var pitch=EditorControls.Slider(panel,"Наклон плоскости · вверх / вниз (°)",m.PerspectivePitch,-60,60,v=>{m.PerspectivePitch=v;Changed();},"0.0");
        panel.Children.Add(EditorControls.Label("Настройте рот параллельно краям экрана. Поворот в глубину уменьшает дальнюю сторону. Применяется к PNG и рисованному рту, включая речь."));
        EditorControls.Button(panel,"Пример ракурса CRT",()=>{roll.Value=12;yaw.Value=25;pitch.Value=0;});
        EditorControls.Button(panel,"Убрать наклон и перспективу",()=>{roll.Value=yaw.Value=pitch.Value=0;});
        void Update(object? sender,EventArgs e){sync=true;x.Value=m.X*100;y.Value=m.Y*100;w.Value=m.Width*100;h.Value=m.Height*100;sync=false;}
        void End(bool apply){scene.EndPlacement();if(apply)save();else{(m.X,m.Y,m.Width,m.Height,m.Rotation,m.PerspectiveYaw,m.PerspectivePitch)=snapshot;scene.RefreshMouthPlacement();}finished=true;IsEnabled=false;}
        EditorControls.Button(panel,"Применить",()=>End(true));EditorControls.Button(panel,"Отмена",()=>End(false));
        Loaded+=(_,_)=>{scene.PlacementChanged+=Update;scene.BeginMouthPlacement();};
        Unloaded+=(_,_)=>{scene.PlacementChanged-=Update;if(!finished)End(false);};
    }
}
