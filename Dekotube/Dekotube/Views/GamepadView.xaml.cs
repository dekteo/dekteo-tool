using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dekotube.Views
{
    public partial class GamepadView : UserControl
    {
        [DllImport("XInput.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState(uint dwUserIndex, out XinputState dwState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XinputState
        {
            public uint dwPacketNumber;
            public Gamepad wGamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Gamepad
        {
            public ushort wButtons;
            public ushort wLeftTrigger;
            public ushort wRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        private DispatcherTimer _updateTimer;
        private bool _isInitialized;

        public GamepadView()
        {
            InitializeComponent();
            Loaded += GamepadView_Loaded;
            Unloaded += GamepadView_Unloaded;
        }

        private void GamepadView_Loaded(object sender, RoutedEventArgs e)
        {
            CheckControllerConnection();
            _updateTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(16) };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void GamepadView_Unloaded(object sender, RoutedEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer = null;
        }

        private void CheckControllerConnection()
        {
            for (int i = 0; i <= 3; i++)
            {
                int result = XInputGetState((uint)i, out var state);
                if (result == 0)
                {
                    _isInitialized = true;
                    TxtGamepadName.Text = $"Контроллер {i + 1}";
                    TxtControllerInfo.Text = "Контроллер обнаружен";
                    TxtControllerInfo.Foreground = Brushes.LightGreen;
                    break;
                }
            }
            if (!_isInitialized)
            {
                TxtGamepadName.Text = "Не подключен";
                TxtControllerInfo.Text = "Контроллер не найден";
                TxtControllerInfo.Foreground = Brushes.Orange;
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Just re-check connection status every tick
            CheckControllerConnection();
        }
    }
}