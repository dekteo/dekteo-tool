using System;
using System.Windows.Media.Imaging;

namespace VeadotubeMy.Services
{
    public class AudioStateService
    {
        private bool _isTalking;
        private long? _lastVoiceTimeMs;
        private int _releaseDelayMs = 200;

        public int ReleaseDelayMs
        {
            get => _releaseDelayMs;
            set => _releaseDelayMs = Math.Clamp(value, 0, 2000);
        }

        /// <summary>Use the microphone's existing sampling loop and a monotonic clock.</summary>
        public void ProcessLevel(int percent, int threshold, long nowMs)
        {
            if (percent > 0 && percent >= Math.Clamp(threshold, 0, 100))
            {
                _lastVoiceTimeMs = nowMs;
                IsTalking = true;
            }
            else if (!_lastVoiceTimeMs.HasValue || nowMs - _lastVoiceTimeMs.Value >= ReleaseDelayMs)
            {
                IsTalking = false;
            }
        }

        public void Reset()
        {
            _lastVoiceTimeMs = null;
            IsTalking = false;
        }

        public bool IsTalking
        {
            get => _isTalking;
            private set
            {
                if (_isTalking == value) return;
                _isTalking = value;
                IsTalkingChanged?.Invoke(this, _isTalking);
            }
        }

        public BitmapImage? IdleImage { get; set; }
        public BitmapImage? TalkingImage { get; set; }

        public event EventHandler<bool>? IsTalkingChanged;
    }
}
