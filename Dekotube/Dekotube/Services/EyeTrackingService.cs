using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dekotube.Services
{
    public class EyeTrackingSettings
    {
        // Left eye settings
        public Point LeftEyeCenter { get; set; } = new Point(0.5, 0.5);
        public double LeftEyeWidth { get; set; } = 0.8;
        public double LeftEyeHeight { get; set; } = 0.8;
        public double LeftMaxDisplacement { get; set; } = 0.3;
        public double LeftScale { get; set; } = 1.0;

        // Right eye settings
        public Point RightEyeCenter { get; set; } = new Point(0.5, 0.5);
        public double RightEyeWidth { get; set; } = 0.8;
        public double RightEyeHeight { get; set; } = 0.8;
        public double RightMaxDisplacement { get; set; } = 0.3;
        public double RightScale { get; set; } = 1.0;

        // Blink enable/disable
        public bool TrackingEnabled { get; set; } = true;
    }

    public class EyeTrackingService
    {
        private readonly AvatarService _avatarService;
        private EyeTrackingSettings _settings;
        private DispatcherTimer _timer;
        private bool _isTracking = false;

        public bool IsTracking => _isTracking;
        public EyeTrackingSettings Settings => _settings;

        public event EventHandler<Point> LeftPupilMoved;
        public event EventHandler<Point> RightPupilMoved;

        public EyeTrackingService(AvatarService avatarService)
        {
            _avatarService = avatarService ?? throw new ArgumentNullException(nameof(avatarService));
            _settings = new EyeTrackingSettings();

            // Initialize pupil positions from service settings
            _avatarService.LeftPupilPosition = _settings.LeftEyeCenter;
            _avatarService.RightPupilPosition = _settings.RightEyeCenter;
        }

        public void Start()
        {
            if (_isTracking) return;
            if (!_settings.TrackingEnabled) return;

            _isTracking = true;
            _timer = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public void SetTrackingEnabled(bool enabled)
        {
            _settings.TrackingEnabled = enabled;
            if (!enabled)
            {
                Stop();
            }
        }

        public void Stop()
        {
            _isTracking = false;
            _timer?.Stop();
            _timer = null;

            // Reset pupils to center when stopped
            _avatarService.LeftPupilPosition = new Point(0.5, 0.5);
            _avatarService.RightPupilPosition = new Point(0.5, 0.5);

        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isTracking || _avatarService == null) return;

            try
            {
                // Get cursor position relative to screen
                var cursorPos = System.Windows.Input.Mouse.GetPosition(null);

                // Convert to relative position (-0.5 to 0.5 range)
                var relativePos = GetRelativeCursorPosition(cursorPos);

                // Calculate gaze direction for each eye with constraints
                var leftPupil = CalculatePupilPosition(
                    relativePos,
                    _settings.LeftEyeCenter,
                    _settings.LeftEyeWidth,
                    _settings.LeftEyeHeight,
                    _settings.LeftMaxDisplacement,
                    _settings.LeftScale);

                var rightPupil = CalculatePupilPosition(
                    relativePos,
                    _settings.RightEyeCenter,
                    _settings.RightEyeWidth,
                    _settings.RightEyeHeight,
                    _settings.RightMaxDisplacement,
                    _settings.RightScale);

                // Update avatar service
                _avatarService.LeftPupilPosition = leftPupil;
                _avatarService.RightPupilPosition = rightPupil;

                // Raise events
                LeftPupilMoved?.Invoke(this, leftPupil);
                RightPupilMoved?.Invoke(this, rightPupil);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EyeTrackingService error: {ex}");
            }
        }

        private Point GetRelativeCursorPosition(Point cursorPos)
        {
            // Get screen bounds using SystemParameters.WorkArea (Rect structure)
            var workArea = SystemParameters.WorkArea;
            var screenWidth = workArea.Width;
            var screenHeight = workArea.Height;

            // Normalize to center (0,0) with -0.5 to 0.5 range
            var relX = (cursorPos.X / screenWidth - 0.5) * 2.0;
            var relY = (cursorPos.Y / screenHeight - 0.5) * 2.0;

            // Invert Y axis (WPF coordinate system vs screen)
            return new Point(relX, -relY);
        }

        private Point CalculatePupilPosition(
            Point relativeCursor,
            Point eyeCenter,
            double eyeWidth,
            double eyeHeight,
            double maxDisplacement,
            double scale)
        {
            // Calculate raw displacement (how far cursor is from eye center)
            var deltaX = relativeCursor.X - eyeCenter.X;
            var deltaY = relativeCursor.Y - eyeCenter.Y;

            // Scale the displacement
            var scaledDeltaX = deltaX * maxDisplacement * scale;
            var scaledDeltaY = deltaY * maxDisplacement * scale;

            // Apply width/height constraints (ellipse constraint - wider than tall typically)
            var constrainedX = scaledDeltaX * (eyeWidth / 2.0);
            var constrainedY = scaledDeltaY * (eyeHeight / 2.0);

            // Return new position: center + constrained displacement
            return new Point(
                Math.Clamp(eyeCenter.X + constrainedX, eyeCenter.X - 0.5, eyeCenter.X + 0.5),
                Math.Clamp(eyeCenter.Y + constrainedY, eyeCenter.Y - 0.5, eyeCenter.Y + 0.5)
            );
        }

        // Method to update settings at runtime
        public void UpdateSettings(EyeTrackingSettings newSettings)
        {
            _settings = newSettings;
            _avatarService.LeftPupilPosition = _settings.LeftEyeCenter;
            _avatarService.RightPupilPosition = _settings.RightEyeCenter;
        }

        // Reset to default positions
        public void Reset()
        {
            _settings = new EyeTrackingSettings();
            _avatarService.LeftPupilPosition = _settings.LeftEyeCenter;
            _avatarService.RightPupilPosition = _settings.RightEyeCenter;

        }
    }
}
