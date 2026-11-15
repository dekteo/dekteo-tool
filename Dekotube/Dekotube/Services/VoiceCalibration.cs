namespace Dekotube.Services;

public readonly record struct VowelFeatures(double F1, double F2, double Periodicity)
{
    public bool Valid => double.IsFinite(F1) && double.IsFinite(F2) && double.IsFinite(Periodicity) && F1 >= 180 && F1 <= 1100 && F2 > F1 + 180 && F2 <= 3200 && Periodicity >= .55;
}

public sealed class VoiceSample
{
    public string Sound { get; set; } = "А";
    public double F1 { get; set; }
    public double F2 { get; set; }
    public int Frames { get; set; }
    public VoiceSample Copy() => (VoiceSample)MemberwiseClone();
}

public sealed class VoiceCalibration
{
    public bool Enabled { get; set; } = true;
    public double MinimumConfidence { get; set; } = .2;
    public List<VoiceSample> Samples { get; set; } = new();
    public static readonly string[] Sounds = ["А", "О", "У", "И", "Э"];
    public static VowelShape ShapeFor(string sound) => sound switch { "А" => VowelShape.A, "О" => VowelShape.O, "У" => VowelShape.U, "И" => VowelShape.IE, "Э" => VowelShape.E, _ => VowelShape.Unknown };
    // Conservative guard for a likely missed low F2, not a universal vowel boundary.
    public static bool IsSuspect(VoiceSample sample) => sample.Sound == "У" && sample.F2 > 1800;
    public VoiceCalibration Copy() => new()
    {
        Enabled = Enabled,
        MinimumConfidence = double.IsFinite(MinimumConfidence) ? Math.Clamp(MinimumConfidence, 0, .95) : .2,
        Samples = (Samples ?? new()).Where(s => s != null && ShapeFor(s.Sound) != VowelShape.Unknown && new VowelFeatures(s.F1,s.F2,1).Valid)
            .GroupBy(s => s.Sound).Select(g => g.Last().Copy()).Take(5).ToList()
    };

    public static bool TryCreateSample(string sound, IReadOnlyList<VowelFeatures> frames, out VoiceSample? sample, out string message)
    {
        sample = null;
        var usable = frames.Where(f => f.Valid && f.Periodicity >= .65).ToArray();
        if (ShapeFor(sound) == VowelShape.Unknown || usable.Length < 32)
        { message = "Мало устойчивого звука. Повторите гласную ровно, без шёпота, ближе к микрофону."; return false; }
        double Median(IEnumerable<double> values) { var sorted=values.OrderBy(x=>x).ToArray(); return sorted[sorted.Length/2]; }
        double f1=Median(usable.Select(f=>f.F1)), f2=Median(usable.Select(f=>f.F2));
        var stable = usable.Where(f=>Math.Abs(Math.Log(f.F1/f1))<.20 && Math.Abs(Math.Log(f.F2/f2))<.16).ToArray();
        if (stable.Length < 32 || stable.Length < usable.Length*.7)
        { message = "Звук слишком менялся. Повторите одну гласную без слов и изменения высоты голоса."; return false; }
        if (sound == "У" && Median(stable.Select(f=>f.F2)) > 1800)
        { message = "Не удалось надёжно измерить «У»: найден слишком высокий второй пик. Возможно, низкий пик пропущен. Образец не заменён. Повторите спокойное «ууу»; если ошибка повторяется, сохраните диагностику."; return false; }
        sample = new VoiceSample { Sound=sound, F1=Median(stable.Select(f=>f.F1)), F2=Median(stable.Select(f=>f.F2)), Frames=stable.Length };
        message = "Образец «"+sound+"» готов."; return true;
    }
}
