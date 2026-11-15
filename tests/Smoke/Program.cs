using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;
using Dekotube.Services;

internal static class Program
{
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--mouth-startup-probe")) { MouthStartupChecks.Run(Assert); return; }
        if (args.Contains("--eye-clothing-probe")) { EyeClothingChecks.Run(Assert); return; }
        if (args.Contains("--hand-lighting-probe")) { HandLightingChecks.Run(Assert); return; }
        if (args.Contains("--arm-motion-probe")) { ArmMotionChecks.Run(Assert); return; }
        if (args.Contains("--workspace-probe")) { WorkspaceChecks.Run(Assert); return; }
        if (args.Contains("--png-reload-probe")) { PngReloadChecks.Run(); return; }
        if (args.Contains("--layered-rig-probe")) { LayeredRigChecks.Run(); return; }
        if (args.Contains("--hands-probe")) { HandsChecks.Run(); return; }
        if (args.Contains("--screen-editor-probe")) { ScreenEditorChecks.Run(Assert); return; }
        if (args.Contains("--mouth-layer-probe")) { MouthLayerChecks.Run(Assert); return; }
        if (args.Contains("--mouth-menu-preview")) { EyeMenuChecks.RenderMouth(); return; }
        if (args.Contains("--layers-menu-preview")) { EyeMenuChecks.RenderLayers(); return; }
        if (args.Contains("--color-probe")) { ColorCorrectionChecks.Run(); return; }
        if (args.Contains("--rounded-vowel-probe")) { RoundedVowelChecks.Run(); return; }
        if (args.Contains("--layer-actions-probe")) { LayerActionsChecks.Run(); return; }
        if (args.Contains("--voice-lights-probe")) { VoiceLightsChecks.Run(); return; }
        if (args.Contains("--scanline-layer-probe")) { ScanlineLayerChecks.Run(); return; }
        if (args.Contains("--robot-screen-probe")) { RobotScreenChecks.Run(); return; }
        if (args.Contains("--effects-menu-probe")) { EffectsMenuChecks.Run(); return; }
        if (args.Contains("--idle-expression-probe")) { IdleExpressionChecks.Run(); return; }
        if (args.Contains("--pose-probe")) { PoseChecks.Run(); return; }
        if (args.Contains("--accessory-probe")) { AccessoryChecks.Run(); return; }
        if (args.Contains("--advanced-visual-probe")) { AdvancedVisualChecks.Run(); return; }
        if (args.Contains("--capture-surface-probe")) { CaptureSurfaceChecks.Run(); return; }
        if (args.Contains("--voice-calibration-probe")) { VoiceCalibrationChecks.Run(Assert); return; }
        if (args.Contains("--digital-blink-probe")) { DigitalBlinkChecks.Run(Assert); return; }
        if (args.Contains("--placement-probe")) { PlacementChecks.Run(Assert); return; }
        if (args.Contains("--eye-menu-probe")) { EyeMenuChecks.Run(Assert); return; }
        if (args.Contains("--light-editor-probe")) { LightEditorChecks.Run(Assert); return; }
        if (args.Contains("--perspective-probe")) { LivingChecks.RenderProbe(System.IO.Path.Combine(AppContext.BaseDirectory,"perspective-probe.png")); return; }
        if (!args.Contains("--render-probe") && !args.Contains("--audio-probe")) { VowelChecks.Run(Assert); VowelChecks.CheckRendering(Assert); SeparateEyeChecks.Run(Assert); HeadChecks.Run(Assert); NaturalMotionChecks.Run(Assert); LivingChecks.Run(Assert); }
        if (args.Contains("--render-probe"))
        {
            var view = new Dekotube.Views.AvatarView();
            var characterForProbe = new AvatarEmotion();
            characterForProbe.Pupils.Enabled = true;
            characterForProbe.Effects.Add(new CharacterEffect { Kind = "wave", When = "always" });
            view.ConfigureCharacter(characterForProbe);
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[] { 255, 255, 255, 255 }, 4);
            bitmap.Freeze();
            ((System.Windows.Controls.Image)view.FindName("LeftPupilOverlay")).Source = bitmap;
            ((System.Windows.Controls.Image)view.FindName("RightPupilOverlay")).Source = bitmap;
            var render = view.GetType().GetMethod("RenderFrame", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var gaze = view.GetType().GetField("_gaze", BindingFlags.NonPublic | BindingFlags.Instance)!;
            view.Measure(new System.Windows.Size(1280, 720));
            view.Arrange(new System.Windows.Rect(0, 0, 1280, 720));
            render.Invoke(view, null); view.UpdateLayout();
            var clip = (System.Windows.Controls.Canvas)view.FindName("LeftEyeClip");
            int replacedMasks = 0, invalidLayouts = 0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                var previousClip = clip.Clip;
                gaze.SetValue(view, new System.Windows.Vector(Math.Sin(i * .1), Math.Cos(i * .1)));
                render.Invoke(view, null);
                if (!ReferenceEquals(previousClip, clip.Clip)) replacedMasks++;
                if (!clip.IsArrangeValid) invalidLayouts++;
                view.UpdateLayout();
            }
            clock.Stop();
            Console.WriteLine($"2000 frame updates: {clock.Elapsed.TotalMilliseconds:0.0} ms, {GC.GetAllocatedBytesForCurrentThread() - allocated} managed bytes; replaced masks: {replacedMasks}; invalidated eye layouts: {invalidLayouts}");
            Assert(replacedMasks == 0 && invalidLayouts == 0, "Animation preserves eye masks and does not invalidate eye layout");
            return;
        }
        if (args.Contains("--audio-probe"))
        {
            using var mic = new MicrophoneService(new AudioStateService());
            var settings = ConfigurationService.LoadOrCreateDefault();
            mic.SetDevice(settings.SelectedDeviceId);
            int max = 0, samples = 0;
            double rawMax = 0;
            mic.RawLevelUpdated += (_, level) => rawMax = Math.Max(rawMax, level);
            mic.LevelUpdated += (_, level) => { max = Math.Max(max, level); samples++; };
            mic.Start();
            if (!mic.IsMonitoring) { Console.WriteLine(mic.LastException); Environment.ExitCode = 1; return; }
            Assert(mic.IsMonitoring, "Selected microphone opens a WASAPI stream: " + mic.Status);
            var probeFrame = new DispatcherFrame();
            var probeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            probeTimer.Tick += (_, _) => { probeTimer.Stop(); probeFrame.Continue = false; };
            probeTimer.Start(); Dispatcher.PushFrame(probeFrame);
            Assert(mic.IsMonitoring && mic.LastException == null, "Capture remains running without device errors");
            Console.WriteLine($"Audio probe: {samples} level events, {mic.CapturedPacketCount} packets, maximum {max}%, raw {rawMax:0.000000}");
            Console.WriteLine($"Endpoint mute: {mic.IsMuted}; volume: {mic.DeviceVolumePercent}%");
            Assert(mic.CapturedPacketCount > 0, "Audio buffers arrive from the selected device");
            mic.Stop();
            return;
        }
        var samplesBytes = new[] { .1f, -.75f, float.NaN, .3f }.SelectMany(BitConverter.GetBytes).ToArray();
        var region = new System.Windows.Rect(.3, .3, .2, .1);
        var moved = EyeRegionGeometry.Move(region, new System.Windows.Vector(1, -1));
        Assert(Math.Abs(moved.Right - 1) < .000001 && moved.Top == 0 && moved.Size == region.Size, "Moving an eye stays on canvas and preserves size");
        Assert(MouthLevelSelection.IsWide(true, 70, 65, false), "Loud speech selects wide mouth");
        Assert(MouthLevelSelection.IsWide(true, 62, 65, true) && !MouthLevelSelection.IsWide(true, 62, 65, false), "Mouth boundary has hysteresis");
        Assert(!MouthLevelSelection.IsWide(false, 100, 65, true), "Silence or simple mode disables wide mouth");
        var extended = new AvatarEmotion { AdvancedMouth = true, MouthWideEyesOpenPath = "wide.png", WideMouthThreshold = 72 };
        extended.Pupils.Left.RotationDegrees = 37;
        var extendedReload = JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(extended))!;
        var extendedCopy = extended.Duplicate();
        extendedCopy.Pupils.Left.RotationDegrees = -20;
        Assert(extendedReload.AdvancedMouth && extendedReload.GetPaths()[4] == "wide.png" && extendedReload.WideMouthThreshold == 72 && extendedReload.Pupils.Left.RotationDegrees == 37 && extended.Pupils.Left.RotationDegrees == 37, "Extended mode and rotation persist and copy independently");
        var rotatedView = new Dekotube.Views.AvatarView();
        rotatedView.ConfigureCharacter(extended); rotatedView.EditEyeRegion(0);
        var rotatedClip = (System.Windows.Controls.Canvas)rotatedView.FindName("LeftEyeClip");
        Assert(rotatedClip.RenderTransform is System.Windows.Media.RotateTransform rt && rt.Angle == 37, "Eye clipping region rotates with its pupil");
        var draftField = rotatedView.GetType().GetField("_regionDraft", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((EyeRegion)draftField.GetValue(rotatedView)!).RotationDegrees = 90;
        rotatedView.EditEyeRegion(null);
        Assert(extended.Pupils.Left.RotationDegrees == 37, "Cancel rotation preserves profile");
        rotatedView.EditEyeRegion(0); ((EyeRegion)draftField.GetValue(rotatedView)!).RotationDegrees = 90;
        rotatedView.ApplyEyeRegion();
        Assert(extended.Pupils.Left.RotationDegrees == 90, "Apply rotation commits draft");
        var previewCharacter = new AvatarEmotion();
        var previewEffect = new CharacterEffect { Kind="darken", Enabled=false, Amount=.5, When="idle" };
        previewCharacter.Effects.Add(previewEffect);
        previewCharacter.Effects.Add(new CharacterEffect { Kind="darken", Enabled=true, Amount=.8, When="always" });
        var previewScene = new Dekotube.Views.AvatarView(); previewScene.ConfigureCharacter(previewCharacter);
        previewScene.PreviewEffect(previewEffect);
        var shade = (System.Windows.Shapes.Rectangle)previewScene.FindName("DarkenOverlay");
        Assert(Math.Abs(shade.Opacity-.4)<.0001 && !previewEffect.Enabled, "Effect preview isolates selected disabled effect without changing settings");
        previewScene.CancelEffectPreview();
        Assert(Math.Abs(shade.Opacity-.64)<.0001, "Cancelling effect preview restores normal effects");
        var emptyEffects = new AvatarEmotion();
        _ = new Dekotube.Views.EffectsView(emptyEffects, previewScene, () => {});
        Assert(emptyEffects.Effects.Count == 0, "Opening effects editor does not create effects");
        var resized = EyeRegionGeometry.Resize(region, new System.Windows.Point(.9, .8), 4);
        Assert(resized.TopLeft == region.TopLeft && (resized.BottomRight - new System.Windows.Point(.9, .8)).Length < .000001, "Corner resize keeps opposite corner fixed");
        var minimum = EyeRegionGeometry.Resize(region, new System.Windows.Point(0, 0), 4);
        Assert(minimum.Width >= .00499 && minimum.Height >= .00499, "Resize cannot invert an eye region");
        var drawn = EyeRegionGeometry.Draw(new System.Windows.Point(.8, .8), new System.Windows.Point(.2, .1));
        Assert(drawn.Left == .2 && drawn.Top == .1 && Math.Abs(drawn.Bottom - .8) < .000001, "An eye can be drawn in reverse direction");
        Assert(MicrophoneService.CalculateFloatPeak(samplesBytes, samplesBytes.Length) == .75, "Audio peak includes negative samples and ignores NaN");
        Assert(MicrophoneService.CalculateFloatPeak(samplesBytes, 4) < .11, "Audio calculation respects recorded byte count");
        Assert(MicrophoneService.CalculateFloatPeak(BitConverter.GetBytes(2f), 4) == 1, "Clipping is limited to 100 percent");
        var offset = GazeMath.Constrain(new System.Windows.Vector(3, 4), 30, 10, 1);
        Assert(Math.Abs(offset.X * offset.X / 900 + offset.Y * offset.Y / 100 - 1) < .000001, "Diagonal gaze stays inside elliptical boundary");
        Assert(GazeMath.Constrain(new System.Windows.Vector(1, 1), 30, 10, 0).Length == 0, "Zero gaze range keeps pupil centred");
        double factor = GazeMath.SmoothingFactor(.016, 100);
        Assert(factor > 0 && factor < 1 && GazeMath.SmoothingFactor(.016, 0) == 1, "Gaze smoothing is bounded and can be disabled");
        var character = new AvatarEmotion();
        character.Pupils.Left.ImagePath = "pupil.png";
        character.Effects.Add(new CharacterEffect { Kind = "wave", Amount = .4 });
        var independent = character.Duplicate();
        independent.Pupils.Left.X = .1; independent.Effects[0].Amount = .9;
        Assert(character.Pupils.Left.X == .45 && character.Effects[0].Amount == .4, "Duplicated pupil and effect settings are independent");
        var restored = JsonSerializer.Deserialize<AvatarEmotion>(JsonSerializer.Serialize(character))!;
        Assert(restored.Pupils.Left.ImagePath == "pupil.png" && restored.Effects[0].Amount == .4, "Pupil regions and effects survive serialization");
        var audio = new AudioStateService { ReleaseDelayMs = 200 };
        int audioChanges = 0;
        audio.IsTalkingChanged += (_, _) => audioChanges++;
        audio.ProcessLevel(19, 20, 0);
        Assert(!audio.IsTalking, "Subthreshold sound does not open mouth");
        audio.ProcessLevel(20, 20, 100);
        audio.ProcessLevel(0, 20, 299);
        Assert(audio.IsTalking && audioChanges == 1, "Short silence holds mouth open without repeated events");
        audio.ProcessLevel(0, 20, 300);
        Assert(!audio.IsTalking && audioChanges == 2, "Mouth closes at release deadline");
        audio.ProcessLevel(80, 20, 400);
        audio.ProcessLevel(0, 20, 500);
        audio.ProcessLevel(50, 20, 550);
        audio.ProcessLevel(0, 20, 700);
        Assert(audio.IsTalking, "Resumed voice renews release delay");
        audio.Reset();
        audio.ProcessLevel(0, 20, 710);
        Assert(!audio.IsTalking, "Stopping clears the held speech state immediately");
        audio.ReleaseDelayMs = 0;
        audio.ProcessLevel(20, 20, 800);
        audio.ProcessLevel(0, 20, 801);
        Assert(!audio.IsTalking, "Zero release closes on the next sample");
        audio.ProcessLevel(0, 0, 900);
        Assert(!audio.IsTalking, "Zero threshold does not treat absolute silence as voice");
        audio.ReleaseDelayMs = 2000;
        audio.ProcessLevel(30, 20, 1000);
        audio.ReleaseDelayMs = 50;
        audio.ProcessLevel(0, 20, 1050);
        Assert(!audio.IsTalking, "Changed release delay takes effect without restart");
        var avatar = AvatarService.Instance;
        int changes = 0;
        avatar.IsTalkingChanged += (_, _) => changes++;
        avatar.IsTalking = true;
        avatar.IsTalking = true;
        avatar.IsTalking = false;
        Assert(changes == 2, "Speech transitions notify exactly once");
        avatar.IsTalking = true;
        new EyeTrackingService(avatar).Stop();
        Assert(avatar.IsTalking, "Stopping pupils preserves speech");
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(file, JsonSerializer.Serialize(new AvatarProfile
            {
                BlinkEnabled = false, TalkThreshold = 0, MouthCloseDelayMs = 350,
                MouthClosedEyesOpenPath = "sample.png"
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            var read = typeof(ConfigurationService).GetMethod("LoadProfileFromPath", BindingFlags.Static | BindingFlags.NonPublic)!;
            var loaded = (AvatarProfile)read.Invoke(null, new object[] { file })!;
            Assert(!loaded.BlinkEnabled && loaded.TalkThreshold == 0 && loaded.MouthClosedEyesOpenPath == "sample.png",
                "Profile round trip preserves disabled blink, zero threshold and PNG path");
            Assert(loaded.MouthCloseDelayMs == 350, "Release delay survives profile round trip");
            Assert(loaded.Emotions.Count == 1 && loaded.Emotions[0].MouthClosedEyesOpenPath == "sample.png",
                "Legacy PNG set migrates to an emotion");
            var original = loaded.Emotions[0];
            var copy = original.Duplicate();
            copy.SetPath("3", "different.png");
            Assert(copy.Id != original.Id && original.MouthClosedEyesOpenPath == "sample.png",
                "Duplicating an emotion preserves independent identity and images");
            loaded.Emotions.Add(copy);
            loaded.SelectedEmotionId = copy.Id;
            File.WriteAllText(file, JsonSerializer.Serialize(loaded,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            loaded = (AvatarProfile)read.Invoke(null, new object[] { file })!;
            Assert(loaded.Emotions.Count == 2 && loaded.SelectedEmotionId == copy.Id &&
                loaded.Emotions[1].MouthClosedEyesOpenPath == "different.png",
                "Multiple emotions and selection survive reload");
            loaded.Emotions[1].Id = loaded.Emotions[0].Id;
            loaded.SelectedEmotionId = "missing";
            AvatarEmotion.Normalize(loaded);
            Assert(loaded.Emotions.Select(x => x.Id).Distinct().Count() == 2 &&
                loaded.SelectedEmotionId == loaded.Emotions[0].Id,
                "Duplicate IDs and missing selection are repaired");
            File.WriteAllText(file, "{}");
            loaded = (AvatarProfile)read.Invoke(null, new object[] { file })!;
            Assert(loaded.MouthCloseDelayMs == 200, "Old profiles get default mouth release delay");
            File.WriteAllText(file, "{\"mouthCloseDelayMs\":-100}");
            var invalidDelay = (AvatarProfile)read.Invoke(null, new object[] { file })!;
            Assert(invalidDelay.MouthCloseDelayMs == 0, "Negative saved delay is clamped");
            Assert(loaded.BlinkEnabled && loaded.TalkThreshold == 20, "Old profiles get defaults for missing fields");
        }
        finally { File.Delete(file); }
        var blink = new BlinkService(avatar) { CloseDurationMs = 20 };
        int opens = 0;
        blink.BlinkStateChanged += (_, closed) => { if (!closed) opens++; };
        blink.BlinkNow();
        blink.BlinkNow();
        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; };
        timeout.Start();
        Dispatcher.PushFrame(frame);
        Assert(opens == 1 && !blink.IsBlinking, "Repeated test blink closes once without enabling automatic blink");
        blink.Dispose();
    }
}
