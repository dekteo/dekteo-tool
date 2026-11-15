namespace Dekotube.Services;

public sealed partial class MicrophoneService
{
    private VoiceCalibration _voiceCalibration = new();
    private readonly List<VowelFeatures> _trainingFrames = new();
    private string? _trainingSound;
    private long _trainingDeadline;
    private bool _vowelDiagnostics;
    private double _detectedConfidence;
    private VowelDiagnostic _latestVowelDiagnostic;
    public VowelDiagnostic VowelDiagnosticSnapshot
    {
        get { lock(_peakLock) return !IsMonitoring ? new() { Reason=VowelReason.MicrophoneOff } : Environment.TickCount64-_latestVowelDiagnostic.ObservedAt>500 ? new() { Reason=VowelReason.Waiting } : _latestVowelDiagnostic; }
    }
    public double CurrentVowelConfidence { get; private set; }
    public bool VowelDiagnosticsEnabled { get { lock (_peakLock) return _vowelDiagnostics; } set { lock (_peakLock) _vowelDiagnostics=value; } }
    public VoiceCalibration VoiceCalibrationSettings { get { lock (_peakLock) return _voiceCalibration.Copy(); } }
    public int CalibrationFrameCount { get { lock (_peakLock) return _trainingFrames.Count; } }
    public bool IsCalibrating { get { lock (_peakLock) return _trainingSound != null; } }
    public void ApplyVoiceCalibration(VoiceCalibration? settings)
    {
        lock (_peakLock) { _voiceCalibration=(settings ?? new()).Copy(); _vowels=CreateVowelAnalyzer(); _detectedConfidence=CurrentVowelConfidence=0; _detectedVowel=CurrentVowel=VowelShape.Unknown; }
    }
    private VowelAnalyzer CreateVowelAnalyzer()
    {
        _latestVowelDiagnostic=default;
        var analyzer=new VowelAnalyzer(_voiceCalibration);
        analyzer.FeaturesAnalyzed += features => {
            lock (_peakLock)
                if (ReferenceEquals(_vowels,analyzer) && _trainingSound != null && Environment.TickCount64<=_trainingDeadline && features.Valid && features.Periodicity>=.65 && _trainingFrames.Count<160)
                    _trainingFrames.Add(features);
        };
        return analyzer;
    }
    public bool BeginVowelCalibration(string sound)
    {
        lock (_peakLock)
        {
            if (!IsMonitoring || VoiceCalibration.ShapeFor(sound)==VowelShape.Unknown || _trainingSound!=null) return false;
            _trainingSound=sound; _trainingFrames.Clear(); _trainingDeadline=Environment.TickCount64+3200;
            _vowels=CreateVowelAnalyzer(); return true;
        }
    }
    public bool FinishVowelCalibration(out VoiceSample? sample, out string message)
    {
        string? sound; VowelFeatures[] frames;
        lock (_peakLock) { sound=_trainingSound; frames=_trainingFrames.ToArray(); _trainingSound=null; _trainingFrames.Clear(); }
        if (sound==null) { sample=null; message="Обучение прервано. Проверьте микрофон и повторите."; return false; }
        return VoiceCalibration.TryCreateSample(sound,frames,out sample,out message);
    }
    public void CancelVowelCalibration() { lock (_peakLock) { _trainingSound=null; _trainingFrames.Clear(); } }
}
