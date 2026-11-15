using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dekotube.Services;

namespace Dekotube.Views;

public sealed class VoiceCalibrationView : UserControl
{
    private readonly MicrophoneService? _mic = (Application.Current as App)?.MicrophoneService;
    private readonly TextBlock _status=EditorControls.Label("Выберите звук для обучения."), _reading=EditorControls.Label(""), _samples=EditorControls.Label("");
    private readonly StackPanel _controls=new();
    private readonly DispatcherTimer _timer=new() { Interval=TimeSpan.FromMilliseconds(100) };
    private string? _sound;
    private long _started;
    private bool _recording, _diagnosticsBefore;
    private bool _freezeDiagnostic;
    private readonly TextBox _diagnostic=new() { IsReadOnly=true, TextWrapping=TextWrapping.Wrap, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, MinHeight=140, MaxHeight=230, Margin=new Thickness(0,6,0,6) };
    public VoiceCalibrationView()
    {
        var panel=new StackPanel { Margin=new Thickness(6) }; Content=panel;
        panel.Children.Add(EditorControls.Label("Говорите обычным голосом в выбранный микрофон. Нажмите звук, дождитесь подсказки и тяните его 3 секунды без слов. Сохраняются только характеристики голоса, аудиозапись не сохраняется."));
        panel.Children.Add(EditorControls.Label("И и Э имеют отдельные формы процедурного рта. Старые наборы PNG используют общий слот И/Э. После смены микрофона образцы желательно обучить заново."));
        panel.Children.Add(EditorControls.Label("Чтобы персонаж использовал результат, включите формы гласных в настройках рта. Обучение само этот режим не включает."));
        panel.Children.Add(_controls);
        var settings=_mic?.VoiceCalibrationSettings ?? new();
        var enabled=new CheckBox { Content="Использовать мои образцы", IsChecked=settings.Enabled, Margin=new Thickness(0,6,0,6) };
        _controls.Children.Add(enabled);
        enabled.Click += (_,_) => { if (!SaveChange(s=>s.Enabled=enabled.IsChecked==true)) enabled.IsChecked=_mic?.VoiceCalibrationSettings.Enabled ?? true; };
        var buttons=new WrapPanel(); _controls.Children.Add(buttons);
        foreach(var sound in VoiceCalibration.Sounds)
        {
            var button=new Button { Content="Обучить «"+sound+"»", Margin=new Thickness(3), Padding=new Thickness(8,5,8,5) };
            button.Click += (_,_)=>Begin(sound); buttons.Children.Add(button);
        }
        EditorControls.Slider(_controls,"Отсеивать сомнительные звуки (%)",settings.MinimumConfidence*100,0,90,v=>SaveChange(s=>s.MinimumConfidence=v/100),"0");
        _controls.Children.Add(EditorControls.Label("Выше — меньше случайных форм, но больше речи только по громкости. Это оценка уверенности алгоритма, не процент точности."));
        EditorControls.Button(_controls,"Сбросить обучение",()=>{ if (SaveChange(s=>s.Samples.Clear())) _status.Text="Образцы удалены. Используются общие настройки распознавания."; });
        panel.Children.Add(_samples); panel.Children.Add(_status); panel.Children.Add(_reading);
        panel.Children.Add(EditorControls.Label("Почему звук не распознаётся · диагностика О / У"));
        panel.Children.Add(_diagnostic);
        var diagnosticButtons=new WrapPanel(); panel.Children.Add(diagnosticButtons);
        var freeze=new Button { Content="Зафиксировать показания", Margin=new Thickness(3), Padding=new Thickness(6) };
        freeze.Click+=(_,_)=>{ _freezeDiagnostic=!_freezeDiagnostic; freeze.Content=_freezeDiagnostic?"Продолжить обновление":"Зафиксировать показания"; };
        diagnosticButtons.Children.Add(freeze);
        EditorControls.Button(diagnosticButtons,"Скопировать диагностику",()=> { try { Clipboard.SetText(_diagnostic.Text); } catch { _status.Text="Не удалось скопировать. Текст можно выделить вручную."; } });
        panel.Children.Add(EditorControls.Label("Произнесите О или У, затем зафиксируйте показания. F1/F2 — измеренные частотные характеристики. Расстояние до образца: меньше — ближе. Это не запись голоса."));
        EditorControls.Button(panel,"Отменить обучение",Cancel);
        _timer.Tick += (_,_)=>Tick();
        Loaded += (_,_)=>{ if(_mic!=null) { _diagnosticsBefore=_mic.VowelDiagnosticsEnabled; _mic.VowelDiagnosticsEnabled=true; } RefreshSamples(); _timer.Start(); };
        Unloaded += (_,_)=>{ Cancel(); _timer.Stop(); if(_mic!=null) _mic.VowelDiagnosticsEnabled=_diagnosticsBefore; };
    }
    private bool SaveChange(Action<VoiceCalibration> change)
    {
        if(_mic==null)return false;
        var next=_mic.VoiceCalibrationSettings; change(next);
        if(!ConfigurationService.UpdateDefaultProfile(p=>p.VoiceCalibration=next)) { _status.Text="Не удалось сохранить. Прежнее обучение сохранено."; return false; }
        _mic.ApplyVoiceCalibration(next); RefreshSamples(); return true;
    }
    private void RefreshSamples() => _samples.Text="Обучены: "+(_mic?.VoiceCalibrationSettings.Samples is {Count:>0} samples ? string.Join(", ",samples.Select(s=>s.Sound)) : "пока нет");
    private void Begin(string sound)
    {
        if(_mic?.IsMonitoring!=true) { _status.Text="Сначала включите микрофон. Обучение использует выбранное устройство."; return; }
        _sound=sound; _started=Environment.TickCount64; _recording=false; _controls.IsEnabled=false;
        _status.Text="Приготовьтесь произнести «"+sound+"»…";
    }
    private void Tick()
    {
        if(_mic==null)return;
        string shape=_mic.CurrentVowel switch { VowelShape.A=>"А",VowelShape.O=>"О",VowelShape.U=>"У",VowelShape.IE=>"И",VowelShape.E=>"Э",_=>"обычная форма по громкости" };
        _reading.Text="Сейчас: "+shape+" · уверенность "+_mic.CurrentVowelConfidence.ToString("P0");
        if(!_freezeDiagnostic) _diagnostic.Text=FormatDiagnostic(_mic.VowelDiagnosticSnapshot,_mic.VoiceCalibrationSettings);
        if(_sound==null)return;
        long elapsed=Environment.TickCount64-_started;
        if(!_mic.IsMonitoring) { Cancel(); _status.Text="Микрофон остановлен. Обучение отменено."; return; }
        if(!_recording && elapsed>=1000)
        {
            if(!_mic.BeginVowelCalibration(_sound)) { Cancel(); _status.Text="Не удалось начать обучение. Проверьте микрофон."; return; }
            _recording=true; _started=Environment.TickCount64; elapsed=0;
        }
        if(!_recording)return;
        _status.Text="Тяните «"+_sound+"» · осталось "+Math.Max(0,3-elapsed/1000.0).ToString("0.0")+" с · устойчивых кадров: "+_mic.CalibrationFrameCount;
        if(elapsed<3000)return;
        bool success=_mic.FinishVowelCalibration(out var sample,out string message);
        _sound=null; _recording=false; _controls.IsEnabled=true;
        _status.Text=message;
        if(success && sample!=null && SaveChange(s=> { s.Samples.RemoveAll(v=>v.Sound==sample.Sound); s.Samples.Add(sample); }))
            _status.Text="«"+sample.Sound+"» сохранён. Произнесите звук снова и посмотрите результат ниже. Можно переобучить тем же нажатием.";
    }
    private void Cancel()
    {
        if(_sound!=null) { _mic?.CancelVowelCalibration(); _status.Text="Обучение отменено. Прежние образцы сохранены."; }
        _sound=null; _recording=false; _controls.IsEnabled=true;
    }
    internal static string FormatDiagnostic(VowelDiagnostic d,VoiceCalibration settings)
    {
        string Sample(string sound) {
            var s=settings.Samples.FirstOrDefault(s=>s.Sound==sound);
            return s==null?"«"+sound+"»: личного образца нет":"«"+sound+"»: F1 "+s.F1.ToString("0")+", F2 "+s.F2.ToString("0")+" Гц"+(VoiceCalibration.IsSuspect(s)?" · подозрительный образец, используется общий эталон; переобучите звук":"");
        }
        bool fresh=d.ObservedAt!=0;
        string text=d.Explanation+"\n";
        if(fresh) {
            text+=$"Уровень до усиления (RMS): {d.Rms*100:0.000}% · устойчивость: {d.Features.Periodicity:P0}\n";
            text+=$"F1: {d.Features.F1:0} Гц · F2: {d.Features.F2:0} Гц (0 — не найдено)\n";
            if(d.Candidate!=VowelShape.Unknown) text+=$"Ближайший: {VowelDiagnostic.Label(d.Candidate)} ({d.BestDistance:0.00}); второй: {VowelDiagnostic.Label(d.RunnerUp)} ({d.SecondDistance:0.00})\nУверенность: {d.Confidence:P0} · порог: {settings.MinimumConfidence:P0}\n";
        }
        return text+"Личные образцы: "+(settings.Enabled?"включены":"выключены")+"\n"+Sample("О")+"\n"+Sample("У");
    }
}
