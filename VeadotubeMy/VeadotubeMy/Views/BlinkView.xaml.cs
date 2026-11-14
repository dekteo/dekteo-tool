using System;
using System.Windows;
using System.Windows.Controls;
using VeadotubeMy.Services;

namespace VeadotubeMy.Views
{
    public partial class BlinkView : UserControl
    {
        private readonly BlinkService? _blink;
        private bool _loaded;
        private PupilOptions? _pupils;
        private Action? _save;

        public BlinkView(AvatarEmotion character, AvatarView scene, Action save)
            : this(character.Pupils.Enabled ? scene.PupilBlinkService : scene.BlinkService)
        {
            if (character.Pupils.Enabled) _pupils = character.Pupils;
            _save = () => { scene.RefreshVisualSettings(); save(); };
            if (_pupils != null && Content is StackPanel panel)
            {
                var smooth=new CheckBox { Content="Промежуточное веко при моргании", IsChecked=_pupils.SmoothBlink, Margin=new Thickness(0,12,0,4),
                    ToolTip="Нужны PNG полуприкрытого и закрытого века. Загрузите их в разделе «Глаза»." };
                smooth.Checked+=(_,_)=>{_pupils.SmoothBlink=true;_save();};
                smooth.Unchecked+=(_,_)=>{_pupils.SmoothBlink=false;_save();};
                panel.Children.Add(smooth);
                var style = new ComboBox { Margin = new Thickness(0, 8, 0, 4), ToolTip = "Для экранного персонажа PNG век не нужны" };
                style.Items.Add(new ComboBoxItem { Content = "Обычное моргание · PNG век", Tag = "images" });
                style.Items.Add(new ComboBoxItem { Content = "Экранное моргание · маски и свечение", Tag = "digital" });
                style.SelectedIndex = _pupils.BlinkStyle == "digital" ? 1 : 0;
                style.SelectionChanged += (_,_) => { if (style.SelectedItem is ComboBoxItem item && item.Tag is string value) { _pupils.BlinkStyle=value; _save(); } };
                panel.Children.Add(new TextBlock { Text = "Стиль закрытия глаз", Foreground = System.Windows.Media.Brushes.LightGray, Margin = new Thickness(0, 8, 0, 4) });
                panel.Children.Add(style);
                panel.Children.Add(new TextBlock { Text = "Экранный режим работает только с самим зрачком: он сжимается к центру без PNG век, чёрных масок и полос.", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 4, 0, 8) });
                EditorControls.Slider(panel, "Скорость закрытия (мс)", _pupils.DigitalBlinkSpeedMs, 120, 1000, v => { _pupils.DigitalBlinkSpeedMs=v; _save(); }, "0");
                EditorControls.Slider(panel, "Сжатие зрачка (%)", _pupils.DigitalBlinkCompression*100, 10, 95, v => { _pupils.DigitalBlinkCompression=v/100; _save(); }, "0");
            }
        }

        public BlinkView(BlinkService? blinkService)
        {
            InitializeComponent();
            _blink = blinkService;
            Loaded += BlinkView_Loaded;
        }

        private void BlinkView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_blink == null) return;

            TglBlink.IsChecked = _pupils?.BlinkEnabled ?? _blink.IsBlinking;
            SldMin.Value = _blink.MinBlinkIntervalMs / 1000.0;
            SldMax.Value = _blink.MaxBlinkIntervalMs / 1000.0;
            SldClose.Value = _blink.CloseDurationMs;

            _loaded = true;
            UpdateLabels();
        }

        private void TglBlink_Checked(object sender, RoutedEventArgs e) => SetBlinkEnabled(true);
        private void TglBlink_Unchecked(object sender, RoutedEventArgs e) => SetBlinkEnabled(false);

        private void SetBlinkEnabled(bool enabled)
        {
            if (_blink == null) return;
            if (!_loaded) return;
            if (_pupils != null) { _pupils.BlinkEnabled=enabled; _save?.Invoke(); return; }
            if (enabled) _blink.Start(); else _blink.Stop();
            if (_loaded)
                ConfigurationService.UpdateDefaultProfile(p => p.BlinkEnabled = enabled);
        }

        private void SldMin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loaded || _blink == null) return;

            int min = (int)Math.Round(SldMin.Value);
            _blink.MinBlinkIntervalMs = min * 1000;
            if (_blink.MaxBlinkIntervalMs < _blink.MinBlinkIntervalMs)
            {
                _blink.MaxBlinkIntervalMs = _blink.MinBlinkIntervalMs;
                SldMax.Value = min;
            }

            UpdateLabels();
            PersistIntervals();
        }

        private void SldMax_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loaded || _blink == null) return;

            int max = (int)Math.Round(SldMax.Value);
            _blink.MaxBlinkIntervalMs = max * 1000;
            if (_blink.MaxBlinkIntervalMs < _blink.MinBlinkIntervalMs)
            {
                _blink.MinBlinkIntervalMs = _blink.MaxBlinkIntervalMs;
                SldMin.Value = max;
            }

            UpdateLabels();
            PersistIntervals();
        }

        private void SldClose_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loaded || _blink == null) return;

            int close = (int)Math.Round(SldClose.Value);
            _blink.CloseDurationMs = close;
            UpdateLabels();
            if (_pupils != null) { _pupils.BlinkDurationMs=close; _save?.Invoke(); }
            else ConfigurationService.UpdateDefaultProfile(p => p.BlinkCloseDurationMs = close);
        }

        private void BtnTestBlink_Click(object sender, RoutedEventArgs e)
        {
            _blink?.BlinkNow();
        }

        private void PersistIntervals()
        {
            if (_blink == null) return;
            if (_pupils != null)
            {
                _pupils.BlinkIntervalMs=_blink.MinBlinkIntervalMs;
                _pupils.MaxBlinkIntervalMs=_blink.MaxBlinkIntervalMs;
                _save?.Invoke();
                return;
            }
            ConfigurationService.UpdateDefaultProfile(p =>
            {
                p.MinBlinkIntervalMs = _blink.MinBlinkIntervalMs;
                p.MaxBlinkIntervalMs = _blink.MaxBlinkIntervalMs;
            });
        }

        private void UpdateLabels()
        {
            if (_blink == null) return;
            TxtMin.Text = (_blink.MinBlinkIntervalMs / 1000.0).ToString("0.#") + " с";
            TxtMax.Text = (_blink.MaxBlinkIntervalMs / 1000.0).ToString("0.#") + " с";
            TxtClose.Text = _blink.CloseDurationMs + " мс";
        }
    }
}
