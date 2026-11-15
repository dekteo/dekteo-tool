using NAudio.Wave;
using Dekotube.Services;
using System.Text.Json;

internal static class VowelChecks
{
    public static void Run(Action<bool, string> assert)
    {
        assert(VowelAnalyzer.Analyze(new double[512]).Shape == VowelShape.Unknown, "Vowels: silence rejected");
        var random = new Random(42);
        assert(VowelAnalyzer.Analyze(Enumerable.Range(0,512).Select(_=>random.NextDouble()-.5).ToArray()).Shape == VowelShape.Unknown, "Vowels: noise rejected");
        assert(VowelAnalyzer.Analyze(Enumerable.Repeat(double.NaN,512).ToArray()).Shape == VowelShape.Unknown, "Vowels: invalid samples rejected");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var test in new[] { (VowelShape.A, 800.0, 1300.0), (VowelShape.O, 500.0, 900.0), (VowelShape.U, 300.0, 700.0), (VowelShape.IE, 350.0, 2300.0), (VowelShape.E, 550.0, 1850.0) })
        foreach (int rate in new[] { 44100, 48000 })
        {
            var samples = Synthesize(rate, test.Item2, test.Item3);
            var bytes = new byte[samples.Length * 4];
            for(int i=0;i<samples.Length;i++) BitConverter.GetBytes((float)samples[i]).CopyTo(bytes,i*4);
            var analyzer = new VowelAnalyzer();
            var format = WaveFormat.CreateIeeeFloatWaveFormat(rate,1);
            // Small native packets must accumulate into a complete analysis window.
            for(int pos=0;pos<bytes.Length;pos+=400) analyzer.Push(bytes[pos..Math.Min(bytes.Length,pos+400)],Math.Min(400,bytes.Length-pos),format);
            Console.WriteLine($"Synthetic {test.Item1} at {rate}: {analyzer.Shape}, confidence {analyzer.Confidence:0.00}");
            assert(analyzer.Shape == test.Item1, $"Vowels: synthetic {test.Item1} at {rate} Hz");
            analyzer.Push(new byte[rate / 5 * 4],rate / 5 * 4,format);
            assert(analyzer.Shape == VowelShape.Unknown, "Vowels: silence clears previous shape");
            analyzer.Reset(); assert(analyzer.Shape == VowelShape.Unknown, "Vowels: reset clears state");
        }
        Console.WriteLine($"Synthetic vowel checks: {watch.ElapsedMilliseconds} ms (10 clips of 350 ms plus silence; not real voice accuracy)");
        var pcmSamples = Synthesize(48000,800,1300);
        foreach (int bits in new[] { 8,16,24,32 })
        {
            int width = bits / 8;
            var data = new byte[pcmSamples.Length * width * 2];
            for (int i=0;i<pcmSamples.Length;i++) for(int c=0;c<2;c++)
            {
                long value = bits == 8 ? (long)(128 + pcmSamples[i] * 127) : (long)(pcmSamples[i] * ((1L << (bits-1))-1));
                for(int b=0;b<width;b++) data[(i*2+c)*width+b]=(byte)(value >> (b*8));
            }
            var analyzer = new VowelAnalyzer(); analyzer.Push(data,data.Length,new WaveFormat(48000,bits,2));
            assert(analyzer.Shape == VowelShape.A,$"Vowels: stereo PCM{bits}");
        }
        var character = new AvatarEmotion { AdvancedMouth = true, UseVowels = true };
        character.SetPath("7","a.png"); character.SetPath("14","ie-blink.png");
        var restored = JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        var copy = character.Duplicate(); copy.SetPath("7","copy.png");
        assert(restored.UseVowels && restored.GetPaths()[13] == "ie-blink.png" && character.VowelPaths[0] == "a.png", "Vowels: slots persist and copies are independent");
        var selective = new AvatarEmotion();
        assert(!selective.HasVowel(VowelShape.A), "Selective vowels: empty character starts without optional sounds");
        selective.SetVowelEnabled(VowelShape.O,true); selective.SetPath("9","o.png");
        assert(selective.HasVowel(VowelShape.O) && !selective.HasVowel(VowelShape.A), "Selective vowels: adding one does not enable others");
        selective.SetVowelEnabled(VowelShape.O,false);
        var roundTrip=JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(selective))!;
        var selectiveProfile=new AvatarProfile { Emotions=[roundTrip] }; AvatarEmotion.Normalize(selectiveProfile);
        assert(!roundTrip.HasVowel(VowelShape.O) && roundTrip.VowelPaths[2]=="o.png", "Selective vowels: removal survives reload without losing PNG");
        selective.SetVowelEnabled(VowelShape.O,true); var independent=selective.Duplicate(); independent.SetVowelEnabled(VowelShape.A,true);
        assert(!selective.HasVowel(VowelShape.A) && selective.HasVowel(VowelShape.O), "Selective vowels: copied selection is independent");
        var attackSamples=Synthesize(48000,800,1300); var attack=new VowelAnalyzer(); int detectedAt=0;
        for(int pos=0;pos<attackSamples.Length;pos+=480)
        {
            var packet=new byte[480*4];
            for(int i=0;i<480 && pos+i<attackSamples.Length;i++) BitConverter.GetBytes((float)attackSamples[pos+i]).CopyTo(packet,i*4);
            attack.Push(packet,packet.Length,WaveFormat.CreateIeeeFloatWaveFormat(48000,1));
            if(attack.Shape==VowelShape.A) { detectedAt=(pos+480)/48; break; }
        }
        assert(detectedAt>0 && detectedAt<=60, $"Vowels: confident synthetic onset detected by {detectedAt} ms (algorithm only)");
        var profile = new AvatarProfile { Emotions = [new AvatarEmotion { VowelPaths = ["old.png"] }] };
        AvatarEmotion.Normalize(profile);
        assert(profile.Emotions[0].VowelPaths.Length == 8 && profile.Emotions[0].VowelPaths[0] == "old.png" && profile.Emotions[0].HasVowel(VowelShape.A) && !profile.Emotions[0].HasVowel(VowelShape.O), "Vowels: short saved slot arrays repaired");
    }
    public static void CheckRendering(Action<bool,string> assert)
    {
        var service = AvatarService.Instance;
        var image = System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[]{255,255,255,255},4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var memory = new System.IO.MemoryStream(); encoder.Save(memory);
        System.Windows.Media.Imaging.BitmapImage Make()
        {
            memory.Position=0; var bmp=new System.Windows.Media.Imaging.BitmapImage(); bmp.BeginInit();
            bmp.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bmp.StreamSource=memory; bmp.EndInit(); bmp.Freeze(); return bmp;
        }
        var ordinary=Make(); var vowelOpen=Make(); var vowelClosed=Make();
        service.MouthOpenEyesOpen=service.MouthOpenEyesClosed=ordinary;
        service.MouthClosedEyesOpen=service.MouthClosedEyesClosed=ordinary;
        service.VowelImages[2]=vowelOpen; service.VowelImages[3]=vowelClosed; service.IsTalking=true;
        var view=new Dekotube.Views.AvatarView();
        var character=new AvatarEmotion { AdvancedMouth=true,UseVowels=true, EnabledVowels=[VowelShape.O] }; view.ConfigureCharacter(character);
        var type=view.GetType(); var update=type.GetMethod("UpdateScene",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        type.GetProperty("CurrentVowel")!.SetValue(view,VowelShape.O);
        update.Invoke(view,null);
        var sceneImage=(System.Windows.Controls.Image)view.FindName("StateImage");
        assert(ReferenceEquals(sceneImage.Source,vowelOpen),"Vowels: detected form selects its PNG");
        type.GetField("_eyesClosed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(view,true);
        update.Invoke(view,null); assert(ReferenceEquals(sceneImage.Source,vowelClosed),"Vowels: blinking selects closed-eye vowel PNG");
        service.VowelImages[3]=null; update.Invoke(view,null);
        assert(ReferenceEquals(sceneImage.Source,ordinary),"Vowels: missing form falls back to ordinary blink PNG");
        character.SetVowelEnabled(VowelShape.O,false); service.VowelImages[3]=vowelClosed; update.Invoke(view,null);
        assert(ReferenceEquals(sceneImage.Source,ordinary),"Selective vowels: removed sound renders ordinary mouth");
        character.UseVowels=false; update.Invoke(view,null);
        assert(ReferenceEquals(sceneImage.Source,ordinary),"Vowels: volume mode ignores detected vowels");
        service.IsTalking=false; Array.Clear(service.VowelImages);
        service.MouthOpenEyesOpen=service.MouthOpenEyesClosed=service.MouthClosedEyesOpen=service.MouthClosedEyesClosed=null!;
    }
    internal static double[] Synthesize(int rate, double f1, double f2, double pitch = 125, double bandwidth = 90)
    {
        var signal = new double[(int)(rate * .35)];
        for (int i=0;i<signal.Length;i++) signal[i] = i % (int)(rate/pitch) == 0 ? 1 : 0;
        foreach (double frequency in new[] { f1, f2, 3000.0 })
        {
            double r=Math.Exp(-Math.PI * bandwidth / rate), c=2*r*Math.Cos(2*Math.PI*frequency/rate), y1=0,y2=0;
            for (int i=0;i<signal.Length;i++) { double y=signal[i]+c*y1-r*r*y2; y2=y1;y1=y; signal[i]=y; }
        }
        double max=signal.Max(Math.Abs);
        for(int i=0;i<signal.Length;i++) signal[i] *= .5/max;
        return signal;
    }
}
