using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace VeadotubeMy.Views
{
    public partial class MouseView : UserControl
    {
        private DispatcherTimer _updateTimer;
        private Point _lastPosition;
        private bool _isMouseTracking = false;

        public MouseView()
        {
            InitializeComponent();
            Loaded += MouseView_Loaded;
            Unloaded += MouseView_Unloaded;
        }

        private void MouseView_Loaded(object sender, RoutedEventArgs e)
        {
            // Mouse tracking starts disabled by default;
            // it can be enabled via toggle in the Mouse section
            UpdateCursorPosition();
        }

        private void MouseView_Unloaded(object sender, RoutedEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer = null;
        }

        // Call this to enable/disable mouse tracking externally
        public void SetMouseTrackingEnabled(bool enabled)
        {
            _isMouseTracking = enabled;
            if (enabled)
            {
                _updateTimer = new DispatcherTimer(DispatcherPriority.Send)
                {
                    Interval = TimeSpan.FromMilliseconds(50) // 20 updates per second
                };
                _updateTimer.Tick += UpdateTimer_Tick;
                _updateTimer.Start();
            }
            else
            {
                _updateTimer?.Stop();
                _updateTimer = null;
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_isMouseTracking)
            {
                UpdateCursorPosition();
            }
        }

        private void UpdateCursorPosition()
        {
            try
            {
                // Получаем позицию курсора относительно рабочей области
                var cursorPos = System.Windows.Input.Mouse.GetPosition(null);

                // Обновляем текстовые поля координат
                if (!_isMouseTracking)
                {
                    // Отслеживание выключено - показываем сообщение
                    TxtCursorX?.Dispatcher.Invoke(() => TxtCursorX.Text = "отслеживание выключено");
                    TxtCursorY?.Dispatcher.Invoke(() => TxtCursorY.Text = "отслеживание выключено");
                    TxtMovement?.Dispatcher.Invoke(() => TxtMovement.Text = "");
                    return;
                }

                // Обновляем текстовые поля координат
                TxtCursorX?.Dispatcher.Invoke(() => TxtCursorX.Text = ((int)cursorPos.X).ToString());
                TxtCursorY?.Dispatcher.Invoke(() => TxtCursorY.Text = ((int)cursorPos.Y).ToString());

                // Вычисляем движение за последний тик
                double deltaX = Math.Abs(cursorPos.X - _lastPosition.X);
                double deltaY = Math.Abs(cursorPos.Y - _lastPosition.Y);
                _lastPosition = cursorPos;

                // Обновляем текст движения (сумма перемещений по осям)
                TxtMovement?.Dispatcher.Invoke(() => TxtMovement.Text = ((int)(deltaX + deltaY)).ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MouseView update error: {ex}");
            }
        }
    }
}