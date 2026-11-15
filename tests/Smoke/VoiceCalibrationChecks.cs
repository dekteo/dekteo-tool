using NAudio.Wave;
using System.Text.Json;
using Dekotube.Services;

internal static class VoiceCalibrationChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(!VoiceCalibration.TryCreateSample("А",Array.Empty<VowelFeatures>(),out _,out _),"Calibration rejects silence without replacing an old sample");
        var frames=Enumerable.Range(0,90).Select(i=>new VowelFeatures(650+(i%5-2)*5,1400+(i%7-3)*5,.9)).ToArray();
        check(VoiceCalibration.TryCreateSample("О",frames,out var learned,out _) && learned!.Frames>=80,
            "Stable voice features produce a robust calibration sample");
        var unstable=Enumerable.Range(0,90).Select(i=>new VowelFeatures(i%2==0?300:900,i%2==0?700:2600,.9)).ToArray();
        check(!VoiceCalibration.TryCreateSample("А",unstable,out _,out _),"Calibration rejects mixed or unstable vowels");
        var settings=new VoiceCalibration { Samples = [new(){Sound="А",F1=1000,F2=2000},learned!,new(){Sound="У",F1=350,F2=800},new(){Sound="И",F1=300,F2=2600},new(){Sound="Э",F1=550,F2=2300}] };
        var personalO=new VowelFeatures(650,1400,.95);
        check(VowelAnalyzer.Classify(personalO).Shape!=VowelShape.O && VowelAnalyzer.Classify(personalO,settings).Shape==VowelShape.O,
            "Personal calibration changes recognition for a shifted speaker example");
        check(VowelAnalyzer.Classify(new(300,2600,.95),settings).Shape==VowelShape.IE && VowelAnalyzer.Classify(new(550,2300,.95),settings).Shape==VowelShape.E,
            "Personal I and E samples produce distinct mouth shapes");
        var ambiguous=settings.Copy(); ambiguous.Samples.First(s=>s.Sound=="А").F1=650; ambiguous.Samples.First(s=>s.Sound=="А").F2=1400;
        check(VowelAnalyzer.Classify(personalO,ambiguous).Shape==VowelShape.Unknown,"Ambiguous personalized sounds fall back to ordinary mouth");
        var strict=new VoiceCalibration { MinimumConfidence=.9 };
        check(VowelAnalyzer.Classify(new(300,700,.6),strict).Shape==VowelShape.Unknown,
            "Confidence threshold rejects a weak classification");
        var off=settings.Copy();off.Enabled=false;
        check(VowelAnalyzer.Classify(personalO,off)==VowelAnalyzer.Classify(personalO),"Disabling calibration restores generic recognition");
        var copy=settings.Copy();copy.Samples[0].F1=800;
        var loaded=JsonSerializer.Deserialize<VoiceCalibration>(JsonSerializer.Serialize(settings))!;
        check(settings.Samples[0].F1==1000 && loaded.Samples.Count==5 && loaded.MinimumConfidence==.2,
            "Calibration numbers serialize and copy independently without recorded audio");
        var legacy=JsonSerializer.Deserialize<AvatarProfile>("{}");
        check(legacy!.VoiceCalibration.Samples.Count==0,"Old profiles default to no personal samples");
        var analyzer=new VowelAnalyzer(); var observed=new List<VowelFeatures>(); analyzer.FeaturesAnalyzed+=f=>observed.Add(f);
        var format=WaveFormat.CreateIeeeFloatWaveFormat(48000,1);
        var samples=VowelChecks.Synthesize(48000,800,1300); var data=new byte[samples.Length*4];
        for(int i=0;i<samples.Length;i++)BitConverter.GetBytes((float)samples[i]).CopyTo(data,i*4);
        for(int i=0;i<8;i++)analyzer.Push(data,data.Length,format);
        check(VoiceCalibration.TryCreateSample("А",observed,out var fromAudio,out _) && fromAudio!.F1>600,
            "Streaming synthetic microphone packets supply usable calibration features");
        analyzer.Push(new byte[48000/5*4],48000/5*4,format);
        check(analyzer.Shape==VowelShape.Unknown,"Silence clears classification after calibration feature collection");
        using var mic=new MicrophoneService(new AudioStateService());
        mic.ApplyVoiceCalibration(settings);
        check(!mic.BeginVowelCalibration("А"),"Calibration cannot start with stopped microphone");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(MicrophoneService).GetField("<IsMonitoring>k__BackingField",flags)!.SetValue(mic,true);
        check(mic.BeginVowelCalibration("А"),"Calibration starts on the existing microphone service");
        var captureAnalyzer=(VowelAnalyzer)typeof(MicrophoneService).GetField("_vowels",flags)!.GetValue(mic)!;
        for(int i=0;i<8;i++)captureAnalyzer.Push(data,data.Length,format);
        check(mic.CalibrationFrameCount>=32 && mic.FinishVowelCalibration(out var captured,out _) && captured!.Sound=="А" && mic.VoiceCalibrationSettings.Samples[0].F1==1000,
            "Service collects features without overwriting saved calibration before explicit save");
        mic.BeginVowelCalibration("О");mic.CancelVowelCalibration();
        check(!mic.IsCalibrating && mic.CalibrationFrameCount==0 && mic.VoiceCalibrationSettings.Samples.Count==5,
            "Cancelling training clears temporary features and retains all saved samples");
        mic.AnalyzeVowels=true;mic.AnalyzeVowels=false;mic.AnalyzeVowels=true;
        var restarted=(VowelAnalyzer)typeof(MicrophoneService).GetField("_vowels",flags)!.GetValue(mic)!;
        var retained=(VoiceCalibration)typeof(VowelAnalyzer).GetField("_calibration",flags)!.GetValue(restarted)!;
        check(retained.Samples.Count==5,"Switching speech analysis keeps personal calibration");
        VowelAnalyzer.ExtractFeatures(new double[512],out var quiet);
        check(quiet.Reason==VowelReason.Quiet && quiet.Rms==0,"Diagnostic identifies silence before vowel matching");
        VowelAnalyzer.ExtractFeatures(Enumerable.Repeat(double.NaN,512).ToArray(),out var invalid);
        check(invalid.Reason==VowelReason.InvalidSignal,"Diagnostic distinguishes invalid samples from silence");
        var ambiguousReport=VowelAnalyzer.ClassifyDetailed(personalO,ambiguous);
        check(ambiguousReport.Reason==VowelReason.Ambiguous && ambiguousReport.Candidate!=VowelShape.Unknown && ambiguousReport.RunnerUp!=VowelShape.Unknown,
            "Diagnostic retains both competing vowels when classification is rejected");
        var low=VowelAnalyzer.ClassifyDetailed(new(300,700,.6),strict);
        check(low.Reason==VowelReason.LowConfidence && low.Confidence<low.Threshold,"Diagnostic distinguishes confidence threshold rejection");
        restarted.Push(data,data.Length,format);
        check(restarted.Diagnostic.ObservedAt>0 && restarted.Diagnostic.Rms>0 && restarted.Diagnostic.Features.Valid,
            "Streaming diagnostic exposes timestamp RMS and measured formants");
        typeof(MicrophoneService).GetField("_latestVowelDiagnostic",flags)!.SetValue(mic,restarted.Diagnostic with { ObservedAt=Environment.TickCount64-1000 });
        check(mic.VowelDiagnosticSnapshot.Reason==VowelReason.Waiting,"Stale diagnostic is not displayed as fresh voice data");
        mic.Stop();
        check(mic.VowelDiagnosticSnapshot.Reason==VowelReason.MicrophoneOff,"Stopping microphone clears live diagnostic status");
    }
}
