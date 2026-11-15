using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Dekotube.Services;

namespace Dekotube.Views;

public partial class AvatarView
{
    private readonly Canvas _mouthCurveHandles = new() { Visibility = Visibility.Collapsed };
    private readonly StackPanel _mouthCurveTools = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,12,0,0), Background = Brushes.Black, Visibility = Visibility.Collapsed };
    private readonly Thumb[] _mouthCurveThumbs = new Thumb[3];
    private readonly Thumb[] _mouthSizeThumbs = new Thumb[4];
    private double[]? _mouthRestDraft, _mouthOpenDraft;
    private bool _mouthEditOpen, _mouthEditTesting, _mouthPauseBefore;
    private VowelShape _mouthEditVowel;
    private MouthVowelPose[]? _mouthPoseDrafts;
    private ComboBox? _mouthFormChoice;
    private Slider? _mouthFormWidth, _mouthFormHeight;
    private readonly StackPanel _mouthDimensions = new() { Orientation=Orientation.Horizontal };
    private bool _updatingMouthDimensions;
    private Action? _mouthCurveSave;
    private Button? _mouthCurveTest;
    private double _mouthEditorLastFrame;
    public bool IsMouthCurveEditing => _mouthRestDraft != null;

    private void InitializeMouthCurveEditor()
    {
        _mouthSprite.Children.Add(_mouthCurveHandles);
        ((Grid)Content).Children.Add(_mouthCurveTools); Panel.SetZIndex(_mouthCurveTools, 110);
        var actions = new WrapPanel(); _mouthCurveTools.Children.Add(actions);
        var state = _mouthFormChoice = new ComboBox { ItemsSource = new[] { "Форма в покое", "Обычное раскрытие", "А · широко", "О · округло", "У · узко", "И · растянуто", "Э · среднее раскрытие" }, SelectedIndex = 0, MinWidth = 170, Margin = new Thickness(4) };
        state.SelectionChanged += (_,_) => { _mouthEditOpen = state.SelectedIndex > 0; _mouthEditVowel = state.SelectedIndex>=2 ? (VowelShape)(state.SelectedIndex-1) : VowelShape.Unknown; StopMouthCurveTest(); SyncMouthDimensions(); RefreshMouthCurvePreview(); };
        actions.Children.Add(state);
        _mouthCurveTest = EditorControls.Button(actions, "▶ Тест звука", () => { _mouthEditTesting = !_mouthEditTesting; _mouthCurveTest!.Content = _mouthEditTesting ? "■ Стоп" : "▶ Тест звука"; RefreshMouthCurvePreview(); });
        EditorControls.Button(actions, "Выпрямить", StraightenMouthCurve);
        EditorControls.Button(actions, "Сброс формы", () => {
            if(_mouthEditVowel!=VowelShape.Unknown && _mouthPoseDrafts!=null) _mouthPoseDrafts[(int)_mouthEditVowel]=MouthVowelPose.Default(_mouthEditVowel);
            else { var curve = new double[] {0,0,.5,0,1,0}; if (_mouthEditOpen) _mouthOpenDraft = curve; else _mouthRestDraft = curve; }
            StopMouthCurveTest(); SyncMouthDimensions(); RefreshMouthCurvePreview();
        });
        EditorControls.Button(actions, "Отмена", () => EndMouthCurveEditing(false));
        EditorControls.Button(actions, "Применить", () => EndMouthCurveEditing(true));
        _mouthCurveTools.Children.Add(_mouthDimensions);
        var widthPanel=new StackPanel { Width=210, Margin=new Thickness(8) }; var heightPanel=new StackPanel { Width=210, Margin=new Thickness(8) };
        _mouthDimensions.Children.Add(widthPanel); _mouthDimensions.Children.Add(heightPanel);
        _mouthFormWidth=EditorControls.Slider(widthPanel,"Ширина формы (%)",100,20,150,v=>{ if(!_updatingMouthDimensions && _mouthPoseDrafts!=null && _mouthEditVowel!=VowelShape.Unknown) { _mouthPoseDrafts[(int)_mouthEditVowel].Width=v/100; RefreshMouthCurvePreview(); } },"0");
        _mouthFormHeight=EditorControls.Slider(heightPanel,"Высота звука (% области рта)",40,2,100,v=>{ if(!_updatingMouthDimensions && _mouthPoseDrafts!=null && _mouthEditVowel!=VowelShape.Unknown) { _mouthPoseDrafts[(int)_mouthEditVowel].Opening=v/100; RefreshMouthCurvePreview(); } },"0");
        _mouthCurveTools.Children.Add(EditorControls.Label("Голубые ручки — ширина и высота. Жёлтые — изгиб. «Выпрямить» убирает улыбку. Высота задаётся только выбранному звуку."));
        for (int i=0;i<3;i++)
        {
            int index = i;
            var thumb = new Thumb { Width = 18, Height = 18, Background = Brushes.Gold, Cursor = Cursors.SizeAll, ToolTip = i == 1 ? "Середина: потяните для изгиба" : "Уголок: потяните для изменения формы" };
            thumb.DragStarted += (_,_) => StopMouthCurveTest();
            thumb.DragDelta += (_,e) => {
                if (!IsMouthCurveEditing) return;
                var curve = SelectedMouthCurve();
                double w = Math.Max(1,_mouthSprite.Width*_proceduralScale.ScaleX), h = Math.Max(1,_mouthSprite.Height*_proceduralScale.ScaleY);
                // Keep x handles ordered and the quadratic mapping monotonic.
                double min = index == 0 ? -.15 : index == 1 ? .4 : .9;
                double max = index == 0 ? .1 : index == 1 ? .6 : 1.15;
                curve[index*2] = Math.Clamp(curve[index*2]+e.HorizontalChange/w,min,max);
                curve[index*2+1] = Math.Clamp(curve[index*2+1]+e.VerticalChange/h,-1.5,1.5);
                RefreshMouthCurvePreview(); e.Handled = true;
            };
            _mouthCurveThumbs[i] = thumb; _mouthCurveHandles.Children.Add(thumb);
        }
        for(int i=0;i<4;i++)
        {
            int index=i;
            var thumb=new Thumb { Width=16, Height=16, Background=Brushes.Cyan, Cursor=i<2?Cursors.SizeWE:Cursors.SizeNS, ToolTip=i<2?"Растянуть рот по ширине":"Раскрыть рот вверх и вниз" };
            thumb.DragStarted+=(_,_)=>{ StopMouthCurveTest(); RefreshMouthCurvePreview(); };
            thumb.DragDelta+=(_,e)=>{
                if(!IsMouthCurveEditing || _mouthEditVowel==VowelShape.Unknown)return;
                var pose=_mouthPoseDrafts![(int)_mouthEditVowel];
                if(index<2)pose.Width=Math.Clamp(pose.Width+(index==0?-1:1)*2*e.HorizontalChange/Math.Max(1,_mouthSprite.Width),.2,1.5);
                else pose.Opening=Math.Clamp((pose.Opening ?? _character!.MouthLayer.MaxOpening*pose.Height)+(index==2?-1:1)*2*e.VerticalChange/Math.Max(1,_mouthSprite.Height),Math.Clamp(_character!.MouthLayer.RestThickness,.02,.4),1);
                SyncMouthDimensions(); RefreshMouthCurvePreview(); e.Handled=true;
            };
            _mouthSizeThumbs[i]=thumb; _mouthCurveHandles.Children.Add(thumb);
        }
    }
    private void StraightenMouthCurve()
    {
        if(!IsMouthCurveEditing)return;
        var curve=SelectedMouthCurve(); curve[1]=curve[3]=curve[5]=0;
        StopMouthCurveTest(); RefreshMouthCurvePreview();
    }
    private double[] SelectedMouthCurve() => _mouthEditVowel!=VowelShape.Unknown ? _mouthPoseDrafts![(int)_mouthEditVowel].Curve : _mouthEditOpen ? _mouthOpenDraft! : _mouthRestDraft!;
    private void SyncMouthDimensions()
    {
        bool show=_mouthEditVowel!=VowelShape.Unknown && _mouthPoseDrafts!=null;
        _mouthDimensions.Visibility=show?Visibility.Visible:Visibility.Collapsed;
        if(!show || _mouthFormWidth==null || _mouthFormHeight==null)return;
        _updatingMouthDimensions=true;
        _mouthFormWidth.Value=_mouthPoseDrafts![(int)_mouthEditVowel].Width*100;
        var pose=_mouthPoseDrafts[(int)_mouthEditVowel];
        _mouthFormHeight.Value=(pose.Opening ?? _character!.MouthLayer.MaxOpening*pose.Height)*100;
        _updatingMouthDimensions=false;
    }
    public bool BeginMouthCurveEditing(Action save, VowelShape vowel=VowelShape.Unknown)
    {
        if (IsMouthCurveEditing) { _mouthFormChoice!.SelectedIndex=vowel==VowelShape.Unknown?0:(int)vowel+1; return true; }
        if (!LayerMouthActive || _character?.MouthLayer.Procedural != true) return false;
        EndScreenRegionEditing(false); EndPlacement(); EndLightEditing(); EditEyeRegion(null);
        _mouthPauseBefore = _editingPaused;
        _mouthRestDraft = MouthLayerOptions.CurveOrDefault(_character.MouthLayer.RestCurve);
        _mouthOpenDraft = MouthLayerOptions.CurveOrDefault(_character.MouthLayer.OpenCurve);
        _mouthPoseDrafts=_proceduralPoses.Select(p=>p.Copy()).ToArray();
        SyncPerspective(); // Editable thumbs must remain in the interactive 2D tree.
        LayoutMouthLayer();
        _mouthFormChoice!.SelectedIndex=vowel==VowelShape.Unknown?0:(int)vowel+1;
        SyncMouthDimensions();
        _mouthCurveSave = save; MouthLayerPreview = false;
        SetEditingPaused(true);
        _mouthCurveHandles.Visibility = _mouthCurveTools.Visibility = Visibility.Visible;
        _mouthCanvas.IsHitTestVisible = true;
        CompositionTarget.Rendering += MouthCurveEditorFrame;
        _mouthEditorLastFrame = _clock.Elapsed.TotalSeconds;
        RefreshMouthCurvePreview(); return true;
    }
    private void MouthCurveEditorFrame(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        if (_mouthEditTesting) RenderMouthLayer(Math.Clamp(now-_mouthEditorLastFrame,0,.1), now);
        _mouthEditorLastFrame = now;
    }
    private void StopMouthCurveTest()
    {
        _mouthEditTesting = false;
        if (_mouthCurveTest != null) _mouthCurveTest.Content = "▶ Тест звука";
    }
    private void RefreshMouthCurvePreview()
    {
        if (IsMouthCurveEditing) RenderMouthLayer(.1, _clock.Elapsed.TotalSeconds);
    }
    private void UpdateMouthCurveHandles()
    {
        var curve = SelectedMouthCurve();
        for (int i=0;i<3;i++)
        {
            Canvas.SetLeft(_mouthCurveThumbs[i], _mouthSprite.Width*(.5+(curve[i*2]-.5)*_proceduralScale.ScaleX)-9);
            Canvas.SetTop(_mouthCurveThumbs[i], _mouthSprite.Height*(.5+curve[i*2+1]*_proceduralScale.ScaleY)-9);
            _mouthCurveThumbs[i].Visibility = _mouthEditTesting ? Visibility.Collapsed : Visibility.Visible;
        }
        double cx=_mouthSprite.Width*(.5+(curve[2]-.5)*_proceduralScale.ScaleX), cy=_mouthSprite.Height*(.5+curve[3]*_proceduralScale.ScaleY);
        for(int i=0;i<4;i++)
        {
            double x=i<2 ? _mouthSprite.Width*(.5+(curve[i==0?0:4]-.5)*_proceduralScale.ScaleX)+(i==0?-24:24) : cx;
            double y=i<2 ? _mouthSprite.Height*(.5+curve[i==0?1:5]*_proceduralScale.ScaleY) : cy+(i==2?-1:1)*(_mouthSprite.Height*_proceduralScale.ScaleY/2+18);
            Canvas.SetLeft(_mouthSizeThumbs[i],x-8); Canvas.SetTop(_mouthSizeThumbs[i],y-8);
            _mouthSizeThumbs[i].Visibility=_mouthEditTesting || _mouthEditVowel==VowelShape.Unknown?Visibility.Collapsed:Visibility.Visible;
        }
    }
    public void EndMouthCurveEditing(bool apply)
    {
        if (!IsMouthCurveEditing) return;
        if (apply && _character != null) {
            _character.MouthLayer.RestCurve = _mouthRestDraft!; _character.MouthLayer.OpenCurve = _mouthOpenDraft!;
            _character.MouthLayer.VowelPoses=Enumerable.Range(1,5).ToDictionary(i=>(VowelShape)i,i=>_mouthPoseDrafts![i].Copy());
        }
        var save = _mouthCurveSave;
        _mouthRestDraft = _mouthOpenDraft = null; _mouthCurveSave = null;
        _mouthPoseDrafts=null;
        StopMouthCurveTest(); CompositionTarget.Rendering -= MouthCurveEditorFrame;
        foreach (var thumb in _mouthCurveThumbs) if (thumb.IsMouseCaptured) thumb.ReleaseMouseCapture();
        foreach (var thumb in _mouthSizeThumbs) if (thumb.IsMouseCaptured) thumb.ReleaseMouseCapture();
        _mouthCurveHandles.Visibility = _mouthCurveTools.Visibility = Visibility.Collapsed;
        _mouthCanvas.IsHitTestVisible = false;
        RefreshMouthLayer(); SyncPerspective(); SetEditingPaused(_mouthPauseBefore); UpdateScene();
        if (apply) save?.Invoke();
    }
}
