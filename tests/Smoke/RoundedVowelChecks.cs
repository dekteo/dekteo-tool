using VeadotubeMy.Services;
using NAudio.Wave;
internal static class RoundedVowelChecks
{
    public static void Run()
    {
        int recovered=0;
        foreach(var f2 in new[]{550,650,750}) foreach(var pitch in new[]{100,150,200})
        {
            var signal=VowelChecks.Synthesize(48000,350,f2,pitch,150);
            var bytes=new byte[signal.Length*4];for(int i=0;i<signal.Length;i++)BitConverter.GetBytes((float)signal[i]).CopyTo(bytes,i*4);
            var analyzer=new VowelAnalyzer();analyzer.Push(bytes,bytes.Length,WaveFormat.CreateIeeeFloatWaveFormat(48000,1));
            Console.WriteLine($"350/{f2} pitch {pitch}: {analyzer.Diagnostic.Features} {analyzer.Diagnostic.Reason}");
            if(f2==650)
            {
                var features=analyzer.Diagnostic.Features;
                if(!features.Valid || Math.Abs(features.F1-350)>80 || Math.Abs(features.F2-f2)>100)throw new Exception("Close low formants were not recovered");
                recovered++;
            }
        }
        var bad=new VoiceSample { Sound="У",F1=390,F2=2390,Frames=90 };
        var calibration=new VoiceCalibration { Samples=[bad] };
        if(VoiceCalibration.TryCreateSample("У",Enumerable.Repeat(new VowelFeatures(390,2390,.99),90).ToArray(),out _,out _))throw new Exception("Suspect training accepted");
        if(!VoiceCalibration.TryCreateSample("У",Enumerable.Repeat(new VowelFeatures(350,650,.99),90).ToArray(),out _,out _))throw new Exception("Valid U training rejected");
        if(VowelAnalyzer.Classify(new(300,700,.99),calibration).Shape!=VowelShape.U)throw new Exception("Bad saved sample still overrides fallback");
        if(calibration.Samples[0].F2!=2390)throw new Exception("Original sample was deleted");
        if(VowelAnalyzer.Classify(new(350,0,.99),calibration).Shape!=VowelShape.Unknown)throw new Exception("Missing F2 forced to U");
        Console.WriteLine($"{recovered} close-formant cases and 5 training/fallback checks passed. 550 Hz cases remain difficult.");
    }
}
