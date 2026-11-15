using System;
using System.Windows.Threading;

namespace Dekotube.Services
{
    /// <summary>
    /// Drives natural blinking: eyes stay open for a random delay (Min..Max),
    /// then close for a short CloseDuration, then reopen. Independent of speech.
    /// </summary>
    public class BlinkService
    {
        private readonly AvatarService _avatarService;
        private readonly Random _random = new Random();
        private DispatcherTimer? _blinkTimer;  // open-phase timer (random delay)
        private DispatcherTimer? _closeTimer;  // close-phase timer (close duration)
        private bool _isBlinking = false;

        /// <summary>Minimum delay between blinks (milliseconds).</summary>
        public int MinBlinkIntervalMs { get; set; } = 3000;

        /// <summary>Maximum delay between blinks (milliseconds).</summary>
        public int MaxBlinkIntervalMs { get; set; } = 8000;

        /// <summary>How long the eyes stay closed per blink (milliseconds).</summary>
        public int CloseDurationMs { get; set; } = 150;

        /// <summary>true = eyes closed, false = eyes open.</summary>
        public event EventHandler<bool>? BlinkStateChanged;

        public BlinkService(AvatarService avatarService)
        {
            _avatarService = avatarService ?? throw new ArgumentNullException(nameof(avatarService));
        }

        public void Start()
        {
            if (_isBlinking) return;
            _isBlinking = true;
            BlinkStateChanged?.Invoke(this, false); // start with eyes open
            ScheduleNextBlink();
        }

        public void Stop()
        {
            _isBlinking = false;
            StopTimers();
            BlinkStateChanged?.Invoke(this, false);
        }

        /// <summary>Triggers an immediate blink (close, then reopen after CloseDurationMs).</summary>
        public void BlinkNow()
        {
            CloseEyes();
        }

        public bool IsBlinking => _isBlinking;

        public void Dispose() => Stop();

        private void ScheduleNextBlink()
        {
            if (!_isBlinking) return;
            StopTimers();

            int min = Math.Max(1, MinBlinkIntervalMs);
            int max = Math.Max(min, MaxBlinkIntervalMs);
            int delay = _random.Next(min, max + 1);

            _blinkTimer = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromMilliseconds(delay)
            };
            _blinkTimer.Tick += (_, _) => CloseEyes();
            _blinkTimer.Start();
        }

        private void CloseEyes()
        {
            StopTimers();
            BlinkStateChanged?.Invoke(this, true);

            _closeTimer = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(20, CloseDurationMs))
            };
            _closeTimer.Tick += (_, _) =>
            {
                StopTimers();
                BlinkStateChanged?.Invoke(this, false);
                ScheduleNextBlink();
            };
            _closeTimer.Start();
        }

        private void StopTimers()
        {
            _blinkTimer?.Stop();
            _blinkTimer = null;
            _closeTimer?.Stop();
            _closeTimer = null;
        }
    }
}
