using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VeadotubeMy.Services
{
    public class AvatarService
    {
        private static AvatarService _instance;

        public static AvatarService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AvatarService();
                }
                return _instance;
            }
        }

        // Singleton pattern - prevent cloning/serialization
        private AvatarService() { }

        // Clone protection
        public object Clone() => this;

        // Core avatar state
        private bool _isTalking;
        public bool IsTalking
        {
            get => _isTalking;
            set
            {
                if (_isTalking == value) return;
                _isTalking = value;
                IsTalkingChanged?.Invoke(this, value);
            }
        }

        // Image layers
        public BitmapImage?[] VowelImages { get; } = new BitmapImage?[8];
        public BitmapImage? MouthWideEyesOpen { get; set; }
        public BitmapImage? MouthWideEyesClosed { get; set; }
        public BitmapImage BaseImage { get; set; } = null;
        public BitmapImage LeftEyeImage { get; set; } = null;
        public BitmapImage RightEyeImage { get; set; } = null;
        public BitmapImage LeftPupilImage { get; set; } = null;
        public BitmapImage RightPupilImage { get; set; } = null;
        public BitmapImage BlinkImage { get; set; } = null;
        public BitmapImage MouthImage { get; set; } = null;

        // Composite state images (new: 4 combined states)
        public BitmapImage MouthOpenEyesOpen { get; set; } = null;
        public BitmapImage MouthOpenEyesClosed { get; set; } = null;
        public BitmapImage MouthClosedEyesOpen { get; set; } = null;
        public BitmapImage MouthClosedEyesClosed { get; set; } = null;

        // Eye tracking settings
        public Point LeftPupilPosition { get; set; } = new Point(0.5, 0.5);
        public Point RightPupilPosition { get; set; } = new Point(0.5, 0.5);
        public double PupilSize { get; set; } = 28;
        public bool HandsActive { get; set; }
        public string LastInputDevice { get; set; } = "Нет ввода";

        // Blink timer
        private DispatcherTimer _blinkTimer;
        private bool _blinkEnabled = true;

        public event EventHandler<bool> IsTalkingChanged;

        // Initialize default images (optional - can be set at startup)
        public void Initialize()
        {
            // Default empty/null images - user will load their own
        }

        // Blink control
        public void StartBlinkTimer(DispatcherTimer timer)
        {
            _blinkTimer?.Stop();
            _blinkTimer = timer;
            _blinkTimer.Tick += BlinkTimer_Tick;
            _blinkTimer.Start();
        }

        public void StopBlinkTimer()
        {
            _blinkTimer?.Stop();
            _blinkTimer = null;
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            // Toggle blink state
            // This will be handled in AvatarView by showing/hiding the BlinkImage
            // The timer just signals that it's time for a blink
            OnBlink?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler OnBlink;

        // Reset all images
        public void Reset()
        {
            BaseImage = null;
            LeftEyeImage = null;
            RightEyeImage = null;
            LeftPupilImage = null;
            RightPupilImage = null;
            BlinkImage = null;
            MouthImage = null;
            IsTalking = false;
            LeftPupilPosition = new Point(0.5, 0.5);
            RightPupilPosition = new Point(0.5, 0.5);

            IsTalkingChanged?.Invoke(this, false);
        }
    }
}
