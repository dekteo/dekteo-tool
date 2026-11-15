using System;
using System.Windows;
using System.Windows.Media;

namespace Dekotube
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public Services.AudioStateService AudioStateService { get; private set; }
        public Services.MicrophoneService MicrophoneService { get; private set; }

        public App()
        {
            AudioStateService = new Services.AudioStateService();
            MicrophoneService = new Services.MicrophoneService(AudioStateService);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                var p = Services.ConfigurationService.LoadOrCreateDefault();

                MicrophoneService.Threshold = (int)p.TalkThreshold;
                MicrophoneService.Sensitivity = p.MicrophoneSensitivity;
                MicrophoneService.ApplyVoiceCalibration(p.VoiceCalibration);
                MicrophoneService.ReleaseDelayMs = p.MouthCloseDelayMs;

                if (!string.IsNullOrEmpty(p.SelectedDeviceId))
                {
                    try { MicrophoneService.SetDevice(p.SelectedDeviceId); }
                    catch { }
                }

                if (Services.ConfigurationService.TryParseColor(p.SceneBackgroundColor, out var color))
                    Resources["SceneBackgroundBrush"] = new SolidColorBrush(color);
                if (p.IsMicrophoneMonitoring) MicrophoneService.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App.OnStartup settings restore error: {ex}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            try
            {
                MicrophoneService?.Dispose();
            }
            catch { }
        }
    }
}
