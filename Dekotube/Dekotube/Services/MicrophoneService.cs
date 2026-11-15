using System;
using System.Collections.Generic;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Dekotube.Services;

/// <summary>One WASAPI recording stream; all UI notifications run on the dispatcher.</summary>
public sealed partial class MicrophoneService : IDisposable
{
    // Capture the native mix format without render-only automatic conversion flags.
    private sealed class NativeCapture(MMDevice device) : WasapiCapture(device, true, 20)
    {
        protected override AudioClientStreamFlags GetAudioClientStreamFlags() => 0;
    }
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly AudioStateService _audio;
    private readonly DispatcherTimer _timer;
    private MMDevice? _device;
    private WasapiCapture? _capture;
    private readonly object _peakLock = new();
    private double _pendingPeak;
    private VowelAnalyzer _vowels = new();
    private VowelShape _detectedVowel;
    private int _endpointReadPending;
    private bool _analyzeVowels;
    public bool AnalyzeVowels
    {
        get { lock (_peakLock) return _analyzeVowels; }
        set { lock (_peakLock) { if (_analyzeVowels != value) { _analyzeVowels = value; _vowels = CreateVowelAnalyzer(); _detectedVowel = CurrentVowel = VowelShape.Unknown; } } }
    }
    public VowelShape CurrentVowel { get; private set; }

    public string SelectedDeviceId { get; private set; } = "";
    public int Threshold { get; set; } = 20;
    public double Sensitivity { get; set; } = 1;
    public static int AmplifiedLevel(double peak, double gain) => (int)Math.Round(Math.Clamp(
        (double.IsFinite(peak) ? peak : 0) * (double.IsFinite(gain) ? Math.Clamp(gain, .5, 8) : 1), 0, 1) * 100);
    public int ReleaseDelayMs { get => _audio.ReleaseDelayMs; set => _audio.ReleaseDelayMs = value; }
    public bool IsMonitoring { get; private set; }
    public double LastRawPeak { get; private set; }
    public Exception? LastException { get; private set; }
    public int LevelEventCount { get; private set; }
    public int CapturedPacketCount { get; private set; }
    public bool IsMuted { get; private set; }
    public int DeviceVolumePercent { get; private set; }
    private long _lastEndpointCheck;
    public string? CurrentDeviceName => _device?.FriendlyName;
    public string? CurrentDeviceState => _device?.State.ToString();
    public string CurrentDataFlow => "Capture";
    public string Status => LastException != null ? "Ошибка: " + LastException.Message : IsMonitoring
        ? IsMuted ? "Микрофон заглушён в Windows" : DeviceVolumePercent == 0 ? "Громкость микрофона в Windows: 0%" : "Микрофон подключён"
        : "Микрофон выключен";
    public event EventHandler<int>? LevelUpdated;
    public event EventHandler<double>? RawLevelUpdated;

    public MicrophoneService(AudioStateService audioStateService)
    {
        _audio = audioStateService;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(20) };
        _timer.Tick += (_, _) => Publish();
    }

    public IEnumerable<(string Id, string Name)> GetDevices()
    {
        var result = new List<(string, string)>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            using (device) result.Add((device.ID, device.FriendlyName));
        return result;
    }

    public void SetDevice(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == SelectedDeviceId) return;
        bool restart = IsMonitoring;
        Stop();
        SelectedDeviceId = id;
        if (restart) Start();
    }

    public void Start()
    {
        if (IsMonitoring) return;
        LastException = null;
        CapturedPacketCount = 0;
        try
        {
            _device = string.IsNullOrEmpty(SelectedDeviceId)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
                : _enumerator.GetDevice(SelectedDeviceId);
            SelectedDeviceId = _device.ID;
            var capture = new NativeCapture(_device);
            _capture = capture;
            capture.DataAvailable += (_, e) =>
            {
                double peak = CalculatePeak(e.Buffer, e.BytesRecorded, capture.WaveFormat);
                VowelAnalyzer? analyzer;
                lock (_peakLock)
                {
                    if (!ReferenceEquals(_capture, capture)) return;
                    CapturedPacketCount++; _pendingPeak = Math.Max(_pendingPeak, peak);
                    analyzer = _analyzeVowels || _vowelDiagnostics || _trainingSound != null ? _vowels : null;
                }
                // Publish raw peaks independently: the UI never waits for LPC analysis.
                if (analyzer != null)
                {
                    analyzer.Push(e.Buffer, e.BytesRecorded, capture.WaveFormat);
                    lock (_peakLock)
                        if (ReferenceEquals(_capture, capture) && ReferenceEquals(_vowels, analyzer)) { _detectedVowel = analyzer.Shape; _detectedConfidence = analyzer.Confidence; _latestVowelDiagnostic=analyzer.Diagnostic; }
                }
            };
            capture.RecordingStopped += (_, e) => _timer.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!ReferenceEquals(_capture, capture)) return;
                Stop();
                LastException = e.Exception ?? new InvalidOperationException("Запись остановлена. Подключите устройство и включите микрофон.");
                Publish();
            }));
            capture.StartRecording();
            IsMonitoring = true;
            _timer.Start();
            Publish();
        }
        catch (Exception ex)
        {
            Stop();
            LastException = ex;
            Publish();
        }
    }

    public static double CalculateFloatPeak(byte[] buffer, int count)
    {
        double peak = 0;
        for (int i = 0; i + 4 <= Math.Min(count, buffer.Length); i += 4)
        {
            float sample = BitConverter.ToSingle(buffer, i);
            if (float.IsFinite(sample)) peak = Math.Max(peak, Math.Abs(sample));
        }
        return Math.Clamp(peak, 0, 1);
    }

    public static double CalculatePeak(byte[] buffer, int count, WaveFormat format)
    {
        bool floating = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        if (floating && format.BitsPerSample == 32) return CalculateFloatPeak(buffer, count);
        int bytes = format.BitsPerSample / 8;
        if (bytes < 1 || bytes > 4) return 0;
        double peak = 0;
        for (int i = 0; i + bytes <= Math.Min(count, buffer.Length); i += bytes)
        {
            double sample = bytes switch
            {
                1 => (buffer[i] - 128) / 128.0,
                2 => BitConverter.ToInt16(buffer, i) / 32768.0,
                3 => ((buffer[i] << 8 | buffer[i + 1] << 16 | buffer[i + 2] << 24) >> 8) / 8388608.0,
                _ => BitConverter.ToInt32(buffer, i) / 2147483648.0
            };
            peak = Math.Max(peak, Math.Abs(sample));
        }
        return Math.Clamp(peak, 0, 1);
    }

    private void Publish()
    {
        if (_device != null && Environment.TickCount64 - _lastEndpointCheck > 500)
        {
            _lastEndpointCheck = Environment.TickCount64;
            var device = _device;
            if (System.Threading.Interlocked.CompareExchange(ref _endpointReadPending, 1, 0) == 0)
                _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        bool muted = device.AudioEndpointVolume.Mute;
                        int volume = (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                        _timer.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (ReferenceEquals(_device, device)) { IsMuted = muted; DeviceVolumePercent = volume; }
                        }), DispatcherPriority.Background);
                    }
                    catch { } // Device may be unplugged/disposed while querying its status.
                    finally { System.Threading.Interlocked.Exchange(ref _endpointReadPending, 0); }
                });
        }
        lock (_peakLock) { LastRawPeak = IsMonitoring ? _pendingPeak : 0; _pendingPeak = 0; CurrentVowel = IsMonitoring && (_analyzeVowels || _vowelDiagnostics) && LastRawPeak > 0 ? _detectedVowel : VowelShape.Unknown; CurrentVowelConfidence = IsMonitoring && LastRawPeak > 0 ? _detectedConfidence : 0; }
        int percent = AmplifiedLevel(LastRawPeak, Sensitivity);
        _audio.ProcessLevel(percent, Threshold, Environment.TickCount64);
        LevelEventCount++;
        RawLevelUpdated?.Invoke(this, LastRawPeak);
        LevelUpdated?.Invoke(this, percent);
    }

    public void Stop()
    {
        _timer.Stop();
        IsMonitoring = false;
        var capture = _capture;
        _capture = null;
        try { capture?.StopRecording(); } catch { }
        capture?.Dispose();
        _device?.Dispose();
        _device = null;
        LastException = null;
        lock (_peakLock) { _trainingSound = null; _trainingFrames.Clear(); _vowels = CreateVowelAnalyzer(); _pendingPeak = 0; _detectedConfidence = CurrentVowelConfidence = 0; _detectedVowel = CurrentVowel = VowelShape.Unknown; }
        _audio.Reset();
        Publish();
    }

    public void Dispose() { Stop(); _enumerator.Dispose(); }
}
