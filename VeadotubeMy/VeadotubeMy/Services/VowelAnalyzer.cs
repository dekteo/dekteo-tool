using NAudio.Wave;

namespace VeadotubeMy.Services;

// IE keeps its persisted numeric value for legacy PNG slots; now denotes И.
public enum VowelShape { Unknown, A, O, U, IE, E }

/// <summary>Experimental local LPC vowel-shape heuristic, not speech transcription.
/// 51.2 ms windows at 10 kHz, 25.6 ms hop. No retained recordings or network.</summary>
public sealed class VowelAnalyzer
{
    // Order 12 merged nearby low resonances in rounded-vowel probes and selected a higher peak as F2.
    private const int Rate = 10000, Size = 512, Order = 20;
    private readonly double[] _ring = new double[Size];
    private int _position, _count, _hop, _sourceRate;
    private double _phase, _filter1, _filter2, _filter3, _filter4;
    private VowelShape _candidate;
    private int _votes;
    public VowelShape Shape { get; private set; }
    public double Confidence { get; private set; }
    public VowelDiagnostic Diagnostic { get; private set; }
    private readonly VoiceCalibration _calibration;
    public event Action<VowelFeatures>? FeaturesAnalyzed;
    public VowelAnalyzer(VoiceCalibration? calibration = null) { _calibration = (calibration ?? new()).Copy(); }

    public void Reset()
    {
        Array.Clear(_ring); _position = _count = _hop = _sourceRate = _votes = 0;
        _phase = _filter1 = _filter2 = _filter3 = _filter4 = Confidence = 0;
        Shape = _candidate = VowelShape.Unknown;
        Diagnostic = default;
    }

    public void Push(byte[] buffer, int count, WaveFormat format)
    {
        if (_sourceRate != format.SampleRate) { Reset(); _sourceRate = format.SampleRate; }
        if (_sourceRate < Rate || format.Channels < 1) { Unsupported(); return; }
        bool floating = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71");
        bool pcm = format.Encoding == WaveFormatEncoding.Pcm ||
            format is WaveFormatExtensible pe && pe.SubFormat == new Guid("00000001-0000-0010-8000-00aa00389b71");
        int bytes = format.BitsPerSample / 8;
        if ((!floating && !pcm) || bytes < 1 || bytes > 4 || floating && bytes != 4) { Unsupported(); return; }
        int stride = format.BlockAlign;
        if (stride < bytes * format.Channels) { Unsupported(); return; }
        // Four low-pass stages reduce aliasing before the streaming rate conversion.
        double alpha = 1 - Math.Exp(-2 * Math.PI * 4000 / _sourceRate);
        Span<double> frame = stackalloc double[Size];
        for (int i = 0; i + stride <= Math.Min(count, buffer.Length); i += stride)
        {
            double mono = 0;
            for (int c = 0; c < format.Channels; c++)
            {
                int j = i + c * bytes;
                double sample = floating ? BitConverter.ToSingle(buffer, j) : bytes switch
                {
                    1 => (buffer[j] - 128) / 128.0,
                    2 => BitConverter.ToInt16(buffer, j) / 32768.0,
                    3 => ((buffer[j] << 8 | buffer[j + 1] << 16 | buffer[j + 2] << 24) >> 8) / 8388608.0,
                    _ => BitConverter.ToInt32(buffer, j) / 2147483648.0
                };
                if (double.IsFinite(sample)) mono += Math.Clamp(sample, -1, 1);
            }
            mono /= format.Channels;
            _filter1 += alpha * (mono - _filter1); _filter2 += alpha * (_filter1 - _filter2);
            _filter3 += alpha * (_filter2 - _filter3); _filter4 += alpha * (_filter3 - _filter4);
            _phase += Rate;
            if (_phase < _sourceRate) continue;
            _phase -= _sourceRate;
            _ring[_position] = _filter4; _position = (_position + 1) % Size;
            _count = Math.Min(Size, _count + 1);
            if (++_hop < Size / 2 || _count < Size) continue;
            _hop = 0;
            for (int n = 0; n < Size; n++) frame[n] = _ring[(_position + n) % Size];
            var features = ExtractFeatures(frame, out var extraction);
            FeaturesAnalyzed?.Invoke(features);
            var result = features.Valid ? ClassifyDetailed(features, _calibration) : extraction;
            Confidence = result.Confidence;
            if (result.Shape == _candidate) _votes = Math.Min(2, _votes + 1); else { _candidate = result.Shape; _votes = 1; }
            // Require two agreeing windows; rejected audio immediately uses the ordinary mouth.
            if (_candidate == VowelShape.Unknown || _votes >= 2 || Shape == VowelShape.Unknown && result.Confidence >= .65) Shape = _candidate;
            Diagnostic = result with { Rms=extraction.Rms, ObservedAt=Environment.TickCount64, Reason=result.Shape!=VowelShape.Unknown && Shape!=result.Shape ? VowelReason.Confirming : result.Reason };
        }
    }
    private void Unsupported() { Shape=VowelShape.Unknown; Confidence=0; Diagnostic=new() { Reason=VowelReason.UnsupportedFormat, ObservedAt=Environment.TickCount64 }; }

    public static (VowelShape Shape, double Confidence) Analyze(ReadOnlySpan<double> input)
        => Classify(ExtractFeatures(input));

    public static VowelFeatures ExtractFeatures(ReadOnlySpan<double> input)
        => ExtractFeatures(input,out _);
    public static VowelFeatures ExtractFeatures(ReadOnlySpan<double> input, out VowelDiagnostic diagnostic)
    {
        diagnostic=new() { Reason=VowelReason.InvalidSignal };
        if (input.Length != Size) return default;
        Span<double> x = stackalloc double[Size];
        double mean = 0, power = 0;
        for (int i = 0; i < Size; i++) mean += input[i];
        mean /= Size;
        for (int i = 0; i < Size; i++) { x[i] = input[i] - mean; power += x[i] * x[i]; }
        if (!double.IsFinite(power)) return default;
        diagnostic=diagnostic with { Rms=Math.Sqrt(power/Size), Reason=VowelReason.Quiet };
        if (power / Size < 1e-7) return default;
        double periodicity = 0;
        for (int lag = 20; lag <= 140; lag++)
        {
            double cross = 0, p1 = 0, p2 = 0;
            for (int i = lag; i < Size; i++) { cross += x[i] * x[i - lag]; p1 += x[i] * x[i]; p2 += x[i - lag] * x[i - lag]; }
            periodicity = Math.Max(periodicity, cross / Math.Sqrt(Math.Max(1e-30, p1 * p2)));
        }
        diagnostic=diagnostic with { Features=new(0,0,periodicity), Reason=VowelReason.Unstable };
        if (periodicity < .55) return default;
        diagnostic=diagnostic with { Reason=VowelReason.UnstableSpectrum };
        for (int i = Size - 1; i >= 0; i--)
            x[i] = (x[i] - .97 * (i > 0 ? x[i - 1] : 0)) * (.54 - .46 * Math.Cos(2 * Math.PI * i / (Size - 1)));
        Span<double> r = stackalloc double[Order + 1];
        Span<double> a = stackalloc double[Order + 1];
        Span<double> old = stackalloc double[Order + 1];
        r.Clear(); a.Clear(); a[0] = 1;
        for (int k = 0; k <= Order; k++) for (int i = k; i < Size; i++) r[k] += x[i] * x[i - k];
        double error = r[0];
        for (int k = 1; k <= Order; k++)
        {
            double sum = r[k];
            for (int j = 1; j < k; j++) sum += a[j] * r[k - j];
            double reflection = -sum / Math.Max(error, 1e-20);
            if (!double.IsFinite(reflection) || Math.Abs(reflection) >= 1) return default;
            a.CopyTo(old); a[k] = reflection;
            for (int j = 1; j < k; j++) a[j] = old[j] + reflection * old[k - j];
            error *= 1 - reflection * reflection;
        }
        Span<double> envelope = stackalloc double[321];
        for (int bin = 0; bin < envelope.Length; bin++)
        {
            double re = 1, im = 0;
            for (int k = 1; k <= Order; k++) { re += a[k] * Cosines[bin, k]; im -= a[k] * Sines[bin, k]; }
            envelope[bin] = 1 / Math.Max(1e-20, re * re + im * im);
        }
        double f1 = 0, f2 = 0;
        for (int bin = 18; bin < 320; bin++)
            if (envelope[bin] > envelope[bin - 1] && envelope[bin] > envelope[bin + 1] &&
                envelope[bin] > 1.15 * Math.Max(envelope[Math.Max(0, bin - 10)], envelope[Math.Min(320, bin + 10)]))
            {
                double frequency = bin * 10;
                if (f1 == 0 && frequency <= 1100) f1 = frequency;
                else if (f1 > 0 && frequency > f1 + 180) { f2 = frequency; break; }
            }
        diagnostic=diagnostic with { Features=new(f1,f2,periodicity), Reason=VowelReason.MissingFormants };
        if (f1 == 0 || f2 == 0) return default;
        return new VowelFeatures(f1, f2, periodicity);
    }
    public static (VowelShape Shape, double Confidence) Classify(VowelFeatures features, VoiceCalibration? calibration = null)
    {
        var result=ClassifyDetailed(features,calibration);
        return (result.Shape,result.Confidence);
    }
    public static VowelDiagnostic ClassifyDetailed(VowelFeatures features, VoiceCalibration? calibration = null)
    {
        if (!features.Valid) return new() { Reason=VowelReason.MissingFormants, Features=features };
        double f1 = features.F1, f2 = features.F2;
        // Compare different output shapes, not I/E exemplars against one another.
        Span<double> distances = stackalloc double[6]; distances.Fill(double.MaxValue);
        foreach (var p in Prototypes)
        {
            bool personalized = false;
            if (calibration?.Enabled == true)
                foreach (var sample in calibration.Samples)
                    if (VoiceCalibration.ShapeFor(sample.Sound) == p.Shape && !VoiceCalibration.IsSuspect(sample))
                    {
                        personalized = true;
                        distances[(int)p.Shape] = Math.Min(distances[(int)p.Shape], Distance(f1,f2,sample.F1,sample.F2));
                    }
            if (!personalized) distances[(int)p.Shape] = Distance(f1,f2,p.F1,p.F2);
        }
        double best = double.MaxValue, second = double.MaxValue; VowelShape shape = VowelShape.Unknown, runner=VowelShape.Unknown;
        for (int i=1;i<distances.Length;i++)
        {
            double d = distances[i];
            if (d < best) { second = best; runner=shape; best = d; shape = (VowelShape)i; } else if(d<second) { second=d; runner=(VowelShape)i; }
        }
        double confidence = best > 2.8 || second-best<.35 ? 0 : Math.Clamp((second - best) / 3, 0, 1) * features.Periodicity;
        double threshold=calibration?.MinimumConfidence ?? .2;
        var reason=best>2.8?VowelReason.FarFromSamples:second-best<.35?VowelReason.Ambiguous:confidence<threshold?VowelReason.LowConfidence:VowelReason.Accepted;
        return new() { Reason=reason, Shape=reason==VowelReason.Accepted?shape:VowelShape.Unknown, Candidate=shape, RunnerUp=runner, Features=features, BestDistance=best, SecondDistance=second, Confidence=confidence, Threshold=threshold };
    }
    private static double Distance(double f1, double f2, double a, double b) => Math.Pow(Math.Log(f1/a)/.35,2)+Math.Pow(Math.Log(f2/b)/.3,2);
    private static readonly (VowelShape Shape, double F1, double F2)[] Prototypes =
        [(VowelShape.A, 750, 1250), (VowelShape.O, 500, 900), (VowelShape.U, 300, 700), (VowelShape.IE, 350, 2300), (VowelShape.E, 550, 1850)];
    private static readonly double[,] Cosines = MakeTable(false), Sines = MakeTable(true);
    private static double[,] MakeTable(bool sine)
    {
        var table = new double[321, Order + 1];
        for (int b = 0; b < 321; b++) for (int k = 1; k <= Order; k++)
            table[b, k] = sine ? Math.Sin(2 * Math.PI * b * 10 * k / Rate) : Math.Cos(2 * Math.PI * b * 10 * k / Rate);
        return table;
    }
}
