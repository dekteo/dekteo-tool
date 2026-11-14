namespace VeadotubeMy.Services;

public enum VowelReason { Waiting, Accepted, Quiet, Unstable, MissingFormants, InvalidSignal, UnstableSpectrum, UnsupportedFormat, FarFromSamples, Ambiguous, LowConfidence, Confirming, MicrophoneOff }
public readonly record struct VowelDiagnostic
{
    public VowelReason Reason { get; init; }
    public VowelShape Shape { get; init; }
    public VowelShape Candidate { get; init; }
    public VowelShape RunnerUp { get; init; }
    public VowelFeatures Features { get; init; }
    public double Rms { get; init; }
    public double BestDistance { get; init; }
    public double SecondDistance { get; init; }
    public double Confidence { get; init; }
    public double Threshold { get; init; }
    public long ObservedAt { get; init; }
    public string Explanation => Reason switch {
        VowelReason.Accepted => "Звук принят",
        VowelReason.Quiet => "Сигнал слишком тихий для анализа гласных",
        VowelReason.Unstable => "Нет устойчивой голосовой периодичности",
        VowelReason.MissingFormants => "Не найдены две частотные характеристики голоса",
        VowelReason.InvalidSignal => "Некорректные данные сигнала",
        VowelReason.UnstableSpectrum => "Не удалось устойчиво оценить спектр голоса",
        VowelReason.UnsupportedFormat => "Формат микрофона не поддержан анализатором",
        VowelReason.FarFromSamples => "Звук слишком далеко от всех образцов",
        VowelReason.Ambiguous => "Два варианта слишком похожи — выбор отклонён",
        VowelReason.LowConfidence => "Уверенность ниже выбранного порога",
        VowelReason.Confirming => "Ожидается подтверждение звука следующим кадром",
        VowelReason.MicrophoneOff => "Микрофон выключен",
        _ => "Ожидание свежих данных микрофона"
    };
    public static string Label(VowelShape shape) => shape switch { VowelShape.A=>"А",VowelShape.O=>"О",VowelShape.U=>"У",VowelShape.IE=>"И",VowelShape.E=>"Э",_=>"—" };
}
