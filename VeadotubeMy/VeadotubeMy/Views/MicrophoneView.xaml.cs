using System;
using System.Windows;
using System.Windows.Controls;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views
{
    public partial class MicrophoneView : UserControl
    {
        private MicrophoneService? _micService;
        private int _lastSavedThreshold = -1;
        private bool _initializing = true;

        public MicrophoneView()
        {
            InitializeComponent();
            _micService = (Application.Current as App)?.MicrophoneService;
            SliderThreshold.ValueChanged += SliderThreshold_ValueChanged;
            SliderCloseDelay.ValueChanged += SliderCloseDelay_ValueChanged;
            SliderSensitivity.ValueChanged += (_, e) => {
                TxtSensitivity.Text = "×" + e.NewValue.ToString("0.0");
                if (_initializing || _micService == null) return;
                _micService.Sensitivity = e.NewValue;
                ConfigurationService.UpdateDefaultProfile(p => p.MicrophoneSensitivity = e.NewValue);
            };
            Loaded += MicrophoneView_Loaded;
            Unloaded += MicrophoneView_Unloaded;
        }

        private void MicrophoneView_Loaded(object sender, RoutedEventArgs e)
        {
            _initializing = true;
            RefreshDevices();
            if (_micService != null)
            {
                _lastSavedThreshold = _micService.Threshold;
                SliderThreshold.Value = _micService.Threshold;
                SliderSensitivity.Value = _micService.Sensitivity;
                TxtSensitivity.Text = "×" + _micService.Sensitivity.ToString("0.0");
                SliderCloseDelay.Value = _micService.ReleaseDelayMs;
                TxtCloseDelay.Text = _micService.ReleaseDelayMs + " мс";
                UpdateMonitoringUi();
                _micService.LevelUpdated += MicService_LevelUpdated;
            }
            _initializing = false;
        }

        private void MicrophoneView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_micService != null)
                _micService.LevelUpdated -= MicService_LevelUpdated;
        }

        private void SliderThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int threshold = (int)SliderThreshold.Value;
            TxtThreshold.Text = threshold + "%";
            if (_micService != null)
            {
                _micService.Threshold = threshold;
                if (!_initializing && threshold != _lastSavedThreshold)
                {
                    _lastSavedThreshold = threshold;
                    ConfigurationService.UpdateDefaultProfile(p => p.TalkThreshold = threshold);
                }
            }
        }

        private void SliderCloseDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int delay = (int)Math.Round(SliderCloseDelay.Value);
            TxtCloseDelay.Text = delay + " мс";
            if (_initializing || _micService == null) return;
            _micService.ReleaseDelayMs = delay;
            if (!ConfigurationService.UpdateDefaultProfile(p => p.MouthCloseDelayMs = delay))
                TxtState.Text = "Не удалось сохранить настройку.";
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshDevices();
        private void CalibrationExpanded(object sender, RoutedEventArgs e) { if (sender is Expander panel) panel.Content=new VoiceCalibrationView(); }
        private void CalibrationCollapsed(object sender, RoutedEventArgs e) { if (sender is Expander panel) panel.Content=null; }

        private void RefreshDevices()
        {
            try
            {
                CbDevices.Items.Clear();
                if (_micService != null)
                {
                    foreach (var d in _micService.GetDevices())
                    {
                        CbDevices.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Id });
                    }

                    if (CbDevices.Items.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(_micService.SelectedDeviceId))
                        {
                            for (int i = 0; i < CbDevices.Items.Count; i++)
                            {
                                var it = (ComboBoxItem)CbDevices.Items[i];
                                if (it.Tag is string id && id == _micService.SelectedDeviceId)
                                {
                                    CbDevices.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            CbDevices.SelectedIndex = 0;
                        }
                    }
                }

                TxtNoDevices.Visibility = CbDevices.Items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                UpdateCurrentDeviceText();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось получить список устройств: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCurrentDeviceText()
        {
            if (CbDevices.SelectedItem is ComboBoxItem it)
                TxtCurrentDevice.Text = "Текущее: " + it.Content;
            else
                TxtCurrentDevice.Text = "";
        }

        private void BtnStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_micService == null) return;
            if (!_micService.IsMonitoring) _micService.Start(); else _micService.Stop();
            ConfigurationService.UpdateDefaultProfile(p => p.IsMicrophoneMonitoring = _micService.IsMonitoring);
            UpdateMonitoringUi();
            if (!_micService.IsMonitoring && _micService.LastException != null)
                TxtState.Text = _micService.Status;
        }

        private void UpdateMonitoringUi()
        {
            bool running = _micService != null && _micService.IsMonitoring;
            BtnStartStop.Content = running ? "Стоп" : "Старт";
            if (!running)
            {
                PbLevel.Value = 0;
                TxtLevelPercent.Text = "0%";
                TxtState.Text = _micService?.Status ?? "Выключен";
            }
        }

        private void MicService_LevelUpdated(object? sender, int percent)
        {
            Dispatcher.Invoke(() =>
            {
                PbLevel.Value = percent;
                TxtLevelPercent.Text = percent + "%";
                bool talking = (Application.Current as App)?.AudioStateService.IsTalking ?? false;
                TxtState.Text = _micService?.IsMonitoring != true || _micService.IsMuted || _micService.DeviceVolumePercent == 0 ? _micService?.Status : talking ? "Говорит" : "Подключён · тишина";
                BtnStartStop.Content = _micService?.IsMonitoring == true ? "Стоп" : "Старт";
            });
        }

        private void CbDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbDevices.SelectedItem is ComboBoxItem it && it.Tag is string id)
            {
                try
                {
                    _micService?.SetDevice(id);
                    if (!_initializing) _micService?.Start();
                    ConfigurationService.UpdateDefaultProfile(p => { p.SelectedDeviceId = id; if (!_initializing) p.IsMicrophoneMonitoring = _micService?.IsMonitoring == true; });
                    UpdateMonitoringUi();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Device selection failed: {ex.Message}");
                }
                UpdateCurrentDeviceText();
            }
        }
    }
}
