using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views;

public partial class AvatarView
{
    private readonly Dictionary<string,BitmapSource?> _lightFiles = new();
    private readonly Dictionary<(BitmapSource,string),BitmapSource> _litCache = new();
    private int _lightVersion;
    private bool _lightBusy;
    private bool _lightPending;
    private bool _lightEditing;
    private bool _pauseBeforeLight;
    private Action? _saveLight;
    public string LightingStatus { get; private set; } = "";
    public event EventHandler? LightingStatusChanged;

    private BitmapSource? LightFile(string path)
    {
        if (!_lightFiles.TryGetValue(path,out var image)) _lightFiles[path]=image=LoadImage(path);
        return image;
    }
    private BitmapSource? LitImage(BitmapSource? image,bool body)
    {
        if(image==null || _character==null) return image;
        var o=_character.Lighting;
        string path=body?o.BodyNormalPath:o.HeadNormalPath;
        if(!o.Enabled || path.Length==0 || o.LightTarget==(body?"head":"body")) return image;
        if(_litCache.TryGetValue((image,path),out var result)) return result;
        _lightPending=true;
        if(!_lightBusy) Dispatcher.BeginInvoke(new Action(ProcessLighting));
        return image;
    }
    public void RefreshLighting(bool reloadFiles=false)
    {
        _lightVersion++;_litCache.Clear();
        if(reloadFiles) _lightFiles.Clear();
        _lightPending=true;
        PositionLamp();
        ProcessLighting();
    }
    private async void ProcessLighting()
    {
        if(_lightBusy || !_lightPending || _character==null) return;
        _lightBusy=true;
        try
        {
            while(_lightPending && _character!=null)
            {
                _lightPending=false;
                int version=_lightVersion;
                var character=_character;
                var options=character.Lighting.Copy();
                var body=Clothed(character.Head.BodyImagePath,LightFile(character.Head.BodyImagePath));
                var head=_lastHeadDiffuse;
                var jobs=new List<(BitmapSource Source,string Path)>();
                string status=options.Enabled?"Добавьте карту нормалей для нужного слоя.":"Освещение выключено.";
                var problems=new List<string>();
                void Add(BitmapSource? source,string path)
                {
                    if(source==null || path.Length==0)return;
                    var map=LightFile(path);
                    if(map==null){problems.Add("Не удалось открыть карту: "+System.IO.Path.GetFileName(path));return;}
                    if(source.PixelWidth!=map.PixelWidth || source.PixelHeight!=map.PixelHeight){problems.Add("Размер карты не совпадает с PNG слоя: "+System.IO.Path.GetFileName(path));return;}
                    jobs.Add((source,path));
                }
                if(options.Enabled)
                {
                    if(options.LightTarget!="head")Add(body,options.BodyNormalPath);
                    if(options.LightTarget!="body")Add(head,options.HeadNormalPath);
                    if(options.LightTarget!="head")
                        foreach(var rig in new[]{character.Hands.Gamepad,character.Hands.Desk})
                            for(int i=0;i<rig.Layers.Count;i++)Add(_handsCache.GetValueOrDefault(rig.Layers[i]) as BitmapSource,rig.NormalPath(i));
                }
                foreach(var job in jobs)
                {
                    if(_litCache.ContainsKey(job))continue;
                    var map=LightFile(job.Path)!;
                    var result=await Task.Run(()=>NormalMapLighting.Apply(job.Source,map,options));
                    if(version!=_lightVersion || character!=_character)break;
                    if(result!=null){_litCache[job]=result;status="Освещение обновлено.";}
                }
                if(version!=_lightVersion || character!=_character)continue;
                LightingStatus=problems.Count>0?string.Join("\n",problems.Distinct()):status;LightingStatusChanged?.Invoke(this,EventArgs.Empty);
                BitmapSource? Output(BitmapSource? source,string path,bool allowed)
                    => source!=null && options.Enabled && allowed && _litCache.TryGetValue((source,path),out var result)?result:source;
                BodyImage.Source=Output(body,options.BodyNormalPath,options.LightTarget!="head");
                StateImage.Source=Output(_lastHeadDiffuse,options.HeadNormalPath,options.LightTarget!="body");
                UpdateHandLighting();
            }
        }
        catch(Exception ex){LightingStatus="Ошибка освещения: "+ex.Message;LightingStatusChanged?.Invoke(this,EventArgs.Empty);}
        finally{_lightBusy=false;}
    }
    public void BeginLightEditing(Action save)
    {
        EndMouthCurveEditing(false);
        if(_character==null)return;
        if(_lightEditing)return;
        EditEyeRegion(null);
        _pauseBeforeLight=_editingPaused;
        _lightEditing=true;_saveLight=save;
        SetEditingPaused(true);
        PreviousStateImage.Source=null;PreviousStateLayer.Opacity=0;CurrentStateLayer.Opacity=1;
        LampEditor.Visibility=Visibility.Visible;
        PositionLamp();RefreshLighting();
    }
    public void EndLightEditing()
    {
        if(!_lightEditing)return;
        _lightEditing=false;LampHandle.ReleaseMouseCapture();LampRadiusRing.ReleaseMouseCapture();LampEditor.Visibility=Visibility.Collapsed;
        _saveLight?.Invoke();_saveLight=null;
        SetEditingPaused(_pauseBeforeLight);
    }
    private void PositionLamp()
    {
        if(!_lightEditing || _character==null || !IsLoaded)return;
        var p=Artwork.TranslatePoint(new Point(_character.Lighting.LightX*Artwork.ActualWidth,_character.Lighting.LightY*Artwork.ActualHeight),LampEditor);
        Canvas.SetLeft(LampHandle,p.X-20);Canvas.SetTop(LampHandle,p.Y-20);
        double radius=_character.Lighting.Radius*Math.Max(Artwork.ActualWidth,Artwork.ActualHeight);
        var edge=Artwork.TranslatePoint(new Point(_character.Lighting.LightX*Artwork.ActualWidth+radius,_character.Lighting.LightY*Artwork.ActualHeight),LampEditor);
        double screenRadius=(edge-p).Length;
        LampRadiusRing.Width=LampRadiusRing.Height=screenRadius*2;
        Canvas.SetLeft(LampRadiusRing,p.X-screenRadius);Canvas.SetTop(LampRadiusRing,p.Y-screenRadius);
        LampRadiusLabel.Text=$"Радиус: {_character.Lighting.Radius*100:0}% · колесо мыши";
        Canvas.SetLeft(LampRadiusLabel,p.X+26);Canvas.SetTop(LampRadiusLabel,p.Y+22);
    }
    private void RadiusDown(object sender,MouseButtonEventArgs e){LampRadiusRing.CaptureMouse();e.Handled=true;}
    private void RadiusMove(object sender,MouseEventArgs e)
    {
        if(!LampRadiusRing.IsMouseCaptured || _character==null)return;
        var point=e.GetPosition(Artwork);
        var center=new Point(_character.Lighting.LightX*Artwork.ActualWidth,_character.Lighting.LightY*Artwork.ActualHeight);
        _character.Lighting.Radius=Math.Clamp((point-center).Length/Math.Max(1,Math.Max(Artwork.ActualWidth,Artwork.ActualHeight)),.05,2);
        RefreshLighting();e.Handled=true;
    }
    private void RadiusUp(object sender,MouseButtonEventArgs e){LampRadiusRing.ReleaseMouseCapture();_saveLight?.Invoke();e.Handled=true;}
    private void RadiusWheel(object sender,MouseWheelEventArgs e)
    {
        if(!_lightEditing || _character==null)return;
        _character.Lighting.Radius=Math.Clamp(_character.Lighting.Radius+Math.Sign(e.Delta)*.05,.05,2);
        RefreshLighting();_saveLight?.Invoke();e.Handled=true;
    }
    private void LampDown(object sender,MouseButtonEventArgs e){LampHandle.CaptureMouse();e.Handled=true;}
    private void LampMove(object sender,MouseEventArgs e)
    {
        if(!LampHandle.IsMouseCaptured || _character==null)return;
        var p=e.GetPosition(Artwork);
        _character.Lighting.LightX=Math.Clamp(p.X/Math.Max(1,Artwork.ActualWidth),0,1);
        _character.Lighting.LightY=Math.Clamp(p.Y/Math.Max(1,Artwork.ActualHeight),0,1);
        RefreshLighting();e.Handled=true;
    }
    private void LampUp(object sender,MouseButtonEventArgs e){LampHandle.ReleaseMouseCapture();_saveLight?.Invoke();e.Handled=true;}
    private void LampDone(object sender,RoutedEventArgs e)=>EndLightEditing();
}
