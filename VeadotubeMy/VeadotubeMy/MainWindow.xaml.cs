using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

namespace VeadotubeMy
{
    public partial class MainWindow : Window
    {
        private double _meterTarget = -1;
        private bool _syncingControls;
        private readonly ObservableCollection<StateSlot> _stateSlots = new();
        private string? _currentOverlayKey;
        private bool _cleanSceneMode = false;
        private DispatcherTimer? _toastTimer;
        private DispatcherTimer? _uiIdleTimer;
        private Brush AccentBrush => (Brush)FindResource("AccentBrush");
        private Brush GrayBrush => new SolidColorBrush(Color.FromRgb(153, 153, 153));

        public MainWindow():this(null) { }
        public MainWindow(AvatarEmotion? previewCharacter)
        {
            InitializeComponent();

            OverlayHost.Visibility = Visibility.Collapsed;

            var slotView = System.Windows.Data.CollectionViewSource.GetDefaultView(_stateSlots);
            slotView.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(StateSlot.MouthGroup)));
            StateList.ItemsSource = slotView;
            if(previewCharacter==null)InitializeEmotions();
            else{
                _loadingEmotions=true;_selectedEmotion=previewCharacter;_emotions.Add(previewCharacter);
                EmotionList.ItemsSource=_emotions;EmotionList.SelectedItem=previewCharacter;_loadingEmotions=false;
                AvatarView.ConfigureCharacter(previewCharacter);BuildStateSlots();
            }

            var mic = (Application.Current as App)?.MicrophoneService;
            if (mic != null)
                mic.LevelUpdated += MicService_LevelUpdated;
            Closed += MainWindow_Closed;

            AvatarView.SceneStateChanged += (_, _) => UpdateActiveState();
            AvatarView.RegionEditingChanged += (_, editing) =>
            {
                RegionHint.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
                if (_currentOverlayKey == "pupils")
                    OverlayHost.Visibility = editing ? Visibility.Hidden : Visibility.Visible;
            };

            Loaded += MainWindow_Loaded;

            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewMouseDown += (_, _) => ResetUiIdleTimer();
            PreviewMouseMove += (_, _) => ResetUiIdleTimer();
            Loaded += (_, _) => StartUiIdleTimer();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var mic = (Application.Current as App)?.MicrophoneService;

            _syncingControls = true;
            BtnMic.IsChecked = mic?.IsMonitoring ?? false;
            BtnMouth.IsChecked = true;
            BtnEyes.IsChecked = AvatarView?.IsBlinkActive ?? false;
            BtnPupils.IsChecked = AvatarView.IsPupilsTracking;
            SceneThreshold.Value = mic?.Threshold ?? 20;
            SceneDelay.Value = mic?.ReleaseDelayMs ?? 200;
            _syncingControls = false;

            UpdateActiveState();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_visualSaveTimer?.IsEnabled == true) FlushVisualSettings(null, EventArgs.Empty);
            _toastTimer?.Stop();
            var mic = (Application.Current as App)?.MicrophoneService;
            if (mic != null)
                mic.LevelUpdated -= MicService_LevelUpdated;

            _uiIdleTimer?.Stop();
        }

        // ================= Permanent mic scale =================

        private void MicService_LevelUpdated(object? sender, int percent)
        {
            Dispatcher.Invoke(() => UpdateMicIndicators(percent));
        }

        private void UpdateMicIndicators(int percent)
        {
            var mic = (Application.Current as App)?.MicrophoneService;
            bool monitoring = mic?.IsMonitoring ?? false;

            double target = monitoring ? Math.Clamp(percent / 100.0, 0, 1) : 0;
            if (target != _meterTarget)
            {
                _meterTarget = target;
                double current = MicLevelScale.ScaleX;
                if (!monitoring || _cleanSceneMode)
                {
                    MicLevelScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    MicLevelScale.ScaleX = target;
                }
                else
                {
                    // Only the visual meter is smoothed; speech still consumes the raw level.
                    MicLevelScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(target > current ? 30 : 180)),
                        HandoffBehavior.SnapshotAndReplace);
                }
            }
            TxtMicPercent.Text = monitoring ? percent + "%" : "—";
            bool talking = monitoring && ((Application.Current as App)?.AudioStateService.IsTalking ?? false);
            TxtMicState.ToolTip = mic?.Status;
            TxtMicState.Text = mic?.LastException != null ? "Ошибка" : !monitoring ? "Выкл" : mic?.IsMuted == true ? "Заглушён" : mic?.DeviceVolumePercent == 0 ? "Громкость 0" : (talking ? "Говорит" : "Тишина");
            TxtMicState.Foreground = talking ? AccentBrush : GrayBrush;
            _syncingControls = true;
            BtnMic.IsChecked = monitoring;
            SceneThreshold.Value = mic?.Threshold ?? 20;
            SceneDelay.Value = mic?.ReleaseDelayMs ?? 200;
            _syncingControls = false;
        }

        // ================= Left toolbar toggles =================

        private void TglFunction_Checked(object sender, RoutedEventArgs e) =>
            ApplyToggle(sender as ToggleButton, true);

        private void TglFunction_Unchecked(object sender, RoutedEventArgs e) =>
            ApplyToggle(sender as ToggleButton, false);

        private void ApplyToggle(ToggleButton? toggle, bool enabled)
        {
            if (_syncingControls || toggle?.Tag is not string tag) return;
            var mic = (Application.Current as App)?.MicrophoneService;

            switch (tag)
            {
                case "mic":
                    if (enabled) mic?.Start(); else mic?.Stop();
                    ConfigurationService.UpdateDefaultProfile(p => p.IsMicrophoneMonitoring = mic?.IsMonitoring == true);
                    UpdateMicIndicators(0);
                    if (enabled && mic?.IsMonitoring != true) ShowToast("Микрофон недоступен. Выберите устройство в настройках.");
                    break;
                case "mouth":
                    AvatarView?.SetMouthEnabled(enabled);
                    break;
                case "eyes":
                    AvatarView?.SetEyesEnabled(enabled);
                    ConfigurationService.UpdateDefaultProfile(p => p.BlinkEnabled = enabled);
                    break;
                case "pupils":
                    AvatarView?.SetPupilsEnabled(enabled);
                    SaveEmotions();
                    break;
            }
        }

        private void BtnDev_Click(object sender, RoutedEventArgs e)
        {
            string msg = (sender as Button)?.Tag switch
            {
                "mouse" => "Отслеживание мыши и кнопок находится в разработке",
                "keyboard" => "Клавиатура находится в разработке",
                "gamepad" => "Геймпад находится в разработке",
                _ => "Функция находится в разработке"
            };
            ShowToast(msg);
        }

        // ================= Side panels toggle =================

        private void BtnTogglePanels_Click(object sender, RoutedEventArgs e)
        {
            ToggleCleanSceneMode();
        }

        private void BtnShowInterface_Click(object sender, RoutedEventArgs e)
        {
            ToggleCleanSceneMode();
        }

        private void ToggleCleanSceneMode() => SetInterfaceHidden(!_cleanSceneMode);

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            ResetUiIdleTimer();
            if (e.Key == Key.F8) { ToggleCleanSceneMode(); e.Handled = true; return; }
            if (_cleanSceneMode) return;
            if (HandleEmotionShortcut(e)) return;
            if (e.Key == Key.Escape)
            {
                if (RegionHint.Visibility == Visibility.Visible) AvatarView.EditEyeRegion(null);
                else HideOverlay();
            }
        }

        private bool _autoHideInterface = true;
        private void ApplyInterfacePreferences()
        {
            var profile = ConfigurationService.LoadOrCreateDefault();
            _autoHideInterface = profile.AutoHideInterface;
            if (_uiIdleTimer == null) return;
            _uiIdleTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(profile.InterfaceIdleSeconds, 3, 60));
            ResetUiIdleTimer();
        }

        private void StartUiIdleTimer()
        {
            _uiIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _uiIdleTimer.Tick += (_, _) =>
            {
                if (_autoHideInterface && _currentOverlayKey == null && Mouse.Captured == null && !LeftPanelBorder.IsMouseOver && !RightPanelBorder.IsMouseOver && !BottomTools.IsMouseOver)
                    SetInterfaceHidden(true);
            };
            ApplyInterfacePreferences();
            _uiIdleTimer.Start();
        }

        private void ResetUiIdleTimer()
        {
            if (_cleanSceneMode || _uiIdleTimer == null) return;
            _uiIdleTimer.Stop(); _uiIdleTimer.Start();
        }

        private void SetInterfaceHidden(bool hidden)
        {
            _cleanSceneMode = hidden;
            BottomTools.Visibility = LeftPanelBorder.Visibility = RightPanelBorder.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
            AvatarView.SetCleanScene(hidden);
            BtnShowInterface.Visibility = Visibility.Collapsed;
            if (hidden)
            {
                HideOverlay();
                ToastHost.Visibility = Visibility.Collapsed;
                _uiIdleTimer?.Stop();
            }
            else ResetUiIdleTimer();
        }

        private void Scene_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_cleanSceneMode) return;
            SetInterfaceHidden(false); e.Handled = true;
        }

        private void SceneThreshold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingControls || !IsLoaded) return;
            if (Application.Current is App app) app.MicrophoneService.Threshold = (int)e.NewValue;
            ConfigurationService.UpdateDefaultProfile(p => p.TalkThreshold = (float)e.NewValue);
        }

        private void SceneDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingControls || !IsLoaded) return;
            int delay = (int)Math.Round(e.NewValue);
            if (Application.Current is App app) app.MicrophoneService.ReleaseDelayMs = delay;
            ConfigurationService.UpdateDefaultProfile(p => p.MouthCloseDelayMs = delay);
        }

        private void ApplyRegion_Click(object sender, RoutedEventArgs e) => AvatarView.ApplyEyeRegion();

        private void CancelRegion_Click(object sender, RoutedEventArgs e) => AvatarView.EditEyeRegion(null);

        // ================= Right-click actions =================

        private void BtnMic_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ShowOverlay("mic", "Микрофон", () => new MicrophoneView());
        }

        private void BtnEyes_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ShowOverlay("blink", "Глаза и моргание", () => new BlinkView(AvatarView?.BlinkService));
        }

        private void Scene_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_cleanSceneMode || OverlayHost.IsMouseOver) return;
            e.Handled = true;

            var menu = new ContextMenu();

            var background = new MenuItem { Header = "Фон сцены" };
            background.Click += (_, _) => ShowOverlay("background", "Фон сцены", () => new ObsView());

            var general = new MenuItem { Header = "Общие настройки" };
            general.Click += (_, _) => ShowOverlay("settings", "Общие", () => new SettingsView(ApplyInterfacePreferences));

            menu.Items.Add(background);
            menu.Items.Add(general);
            menu.PlacementTarget = sender as UIElement;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void BtnOverlayClose_Click(object sender, RoutedEventArgs e) => HideOverlay();

        private void ShowOverlay(string key, string title, Func<UserControl> createView)
        {
            if (_currentOverlayKey == key)
            {
                return;
            }

            OverlayHost.RenderTransform = new TranslateTransform();
            OverlayHost.HorizontalAlignment = HorizontalAlignment.Right;
            OverlayHost.VerticalAlignment = VerticalAlignment.Top;
            OverlayHost.Margin = new Thickness(16);
            OverlayHost.Width = 430;
            OverlayHost.MaxHeight = double.PositiveInfinity;
            var inspectorView=createView();
            if(inspectorView is not EffectsView && inspectorView is not PupilsView)InspectorTabs.Organize(inspectorView);
            OverlayContent.Content = inspectorView;
            OverlayTitle.Text = title;
            OverlayHost.Visibility = Visibility.Visible;
            _currentOverlayKey = key;
        }

        private DispatcherTimer? _visualSaveTimer;
        private void SaveVisualSettings()
        {
            AvatarView.UpdateRenderingSubscription();
            _visualSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _visualSaveTimer.Tick -= FlushVisualSettings;
            _visualSaveTimer.Tick += FlushVisualSettings;
            _visualSaveTimer.Stop(); _visualSaveTimer.Start();
        }
        private void FlushVisualSettings(object? sender, EventArgs e) { _visualSaveTimer?.Stop(); SaveEmotions(); BuildLayerImages(); }

        private void HideOverlay()
        {
            if (_visualSaveTimer?.IsEnabled == true) FlushVisualSettings(null, EventArgs.Empty);
            _currentOverlayKey = null;
            OverlayHost.Visibility = Visibility.Collapsed;
            OverlayContent.Content = null;
            _currentOverlayKey = null;
            AvatarView.EditEyeRegion(null);
            _syncingControls = true;
            if (_slotsSeparateEyes != (_selectedEmotion?.Pupils.Enabled == true)) BuildStateSlots();
            BtnEyes.IsEnabled = BtnBlinkSettings.IsEnabled = !AvatarView.IsPupilsTracking;
            BtnEyes.ToolTip = AvatarView.IsPupilsTracking ? "Моргание отдельных глаз — в настройках зрачков" : "Моргание на PNG персонажа";
            BtnEyes.IsChecked = !AvatarView.IsPupilsTracking && AvatarView.IsBlinkActive;
            BtnPupils.IsChecked = AvatarView.IsPupilsTracking;
            _syncingControls = false;
            _currentOverlayKey = null;
        }

        private Point? _overlayDrag;
        private Point _overlayOrigin;
        private void OverlayDrag_Down(object sender, MouseButtonEventArgs e)
        {
            if (BtnOverlayClose.IsMouseOver) return;
            _overlayDrag = e.GetPosition(this);
            var transform = OverlayHost.RenderTransform as TranslateTransform ?? new TranslateTransform();
            OverlayHost.RenderTransform = transform;
            _overlayOrigin = new Point(transform.X, transform.Y);
            ((UIElement)sender).CaptureMouse(); e.Handled = true;
        }
        private void OverlayDrag_Move(object sender, MouseEventArgs e)
        {
            if (_overlayDrag is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = e.GetPosition(this) - start;
            var transform = (TranslateTransform)OverlayHost.RenderTransform;
            var position = OverlayHost.TranslatePoint(new Point(), this);
            double baseX = position.X - transform.X, baseY = position.Y - transform.Y;
            transform.X = Math.Clamp(_overlayOrigin.X + delta.X, -baseX + 8, Math.Max(-baseX + 8, ActualWidth - baseX - OverlayHost.ActualWidth - 8));
            transform.Y = Math.Clamp(_overlayOrigin.Y + delta.Y, -baseY + 8, Math.Max(-baseY + 8, ActualHeight - baseY - OverlayHost.ActualHeight - 8));
        }
        private void OverlayDrag_Up(object sender, MouseButtonEventArgs e)
        {
            _overlayDrag = null; ((UIElement)sender).ReleaseMouseCapture();
        }

        // ================= Toast =================

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastHost.Visibility = _cleanSceneMode ? Visibility.Collapsed : Visibility.Visible;

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer?.Stop();
                ToastHost.Visibility = Visibility.Collapsed;
            };
            _toastTimer.Start();
        }

        // ================= PNG state slots (right panel) =================

        private void BuildStateSlots()
        {
            BuildLayerImages();
            _stateSlots.Clear();
            var s = AvatarService.Instance;
            _loadingMouthMode = true;
            _slotsSeparateEyes = _selectedEmotion?.Pupils.Enabled == true;
            EyeMode.SelectedIndex = _slotsSeparateEyes ? 1 : 0;
            MouthMode.SelectedIndex = _selectedEmotion?.AdvancedMouth == true ? (_selectedEmotion.UseVowels ? 2 : 1) : 0;
            WideMouthThreshold.Value = _selectedEmotion?.WideMouthThreshold ?? 65;
            MouthDriver.SelectedIndex = _selectedEmotion?.UseVowels == true ? 1 : 0;
            MouthDriver.Visibility = Visibility.Collapsed;
            VowelStatus.Visibility = MouthMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            WideMouthSettings.Visibility = MouthMode.SelectedIndex == 1 && MouthDriver.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            VowelActions.Visibility = VowelStatus.Visibility;
            _loadingMouthMode = false;
            _stateSlots.Add(new StateSlot { Key = "1", Caption = _selectedEmotion?.AdvancedMouth == true && !_selectedEmotion.UseVowels ? "Рот приоткрыт · Глаза открыты" : "Рот открыт · Глаза открыты", ToolTip = "Рот открыт, глаза открыты", Thumbnail = s.MouthOpenEyesOpen });
            _stateSlots.Add(new StateSlot { Key = "2", Caption = _selectedEmotion?.AdvancedMouth == true && !_selectedEmotion.UseVowels ? "Рот приоткрыт · Глаза закрыты" : "Рот открыт · Глаза закрыты", ToolTip = "Рот открыт, глаза закрыты", Thumbnail = s.MouthOpenEyesClosed });
            _stateSlots.Add(new StateSlot { Key = "3", Caption = "Рот закрыт · Глаза открыты", ToolTip = "Рот закрыт, глаза открыты", Thumbnail = s.MouthClosedEyesOpen });
            _stateSlots.Add(new StateSlot { Key = "4", Caption = "Рот закрыт · Глаза закрыты", ToolTip = "Рот закрыт, глаза закрыты", Thumbnail = s.MouthClosedEyesClosed });
            if (_selectedEmotion?.AdvancedMouth == true && !_selectedEmotion.UseVowels)
            {
                _stateSlots.Add(new StateSlot { Key = "5", Caption = "Рот широко открыт · Глаза открыты", ToolTip = "Громкая речь, глаза открыты", Thumbnail = s.MouthWideEyesOpen });
                _stateSlots.Add(new StateSlot { Key = "6", Caption = "Рот широко открыт · Глаза закрыты", ToolTip = "Громкая речь, глаза закрыты", Thumbnail = s.MouthWideEyesClosed });
            }
            if (_selectedEmotion?.AdvancedMouth == true && _selectedEmotion.UseVowels)
            {
                string[] names = ["А", "О", "У", "И / Э"];
                for (int i = 0; i < 8; i++)
                    if (_selectedEmotion.HasVowel((VowelShape)(i / 2 + 1)))
                    _stateSlots.Add(new StateSlot { Key = (i + 7).ToString(), Caption = $"Рот {names[i / 2]} · Глаза {(i % 2 == 0 ? "открыты" : "закрыты")}",
                        ToolTip = $"Целый персонаж: форма рта {names[i / 2]}, глаза {(i % 2 == 0 ? "открыты" : "закрыты")}", Thumbnail = s.VowelImages[i] });
            }
            if (_selectedEmotion != null)
            {
                var visible = _selectedEmotion.VisibleSlots().ToHashSet();
                foreach (var slot in _stateSlots.ToArray())
                {
                    if (!visible.Contains(int.Parse(slot.Key))) { _stateSlots.Remove(slot); continue; }
                    if (_slotsSeparateEyes)
                    {
                        slot.Caption = slot.Caption.Split('·')[0].Trim();
                        slot.ToolTip = slot.Caption + ". PNG персонажа без зрачков; моргание настраивается отдельно.";
                    }
                }
            }
        }

        private bool _slotsSeparateEyes;
        private void EyeMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingMouthMode || _selectedEmotion == null) return;
            AvatarView.SetPupilsEnabled(EyeMode.SelectedIndex == 1);
            SaveEmotions(); BuildStateSlots();
        }
        private void AddVowel_Click(object sender, RoutedEventArgs e) => ShowVowelMenu((Button)sender, true);
        private void RemoveVowel_Click(object sender, RoutedEventArgs e) => ShowVowelMenu((Button)sender, false);
        private void ShowVowelMenu(Button anchor, bool adding)
        {
            if (_selectedEmotion is not { } character) return;
            var menu = new ContextMenu();
            string[] labels = ["А", "О", "У", "И / Э"];
            foreach (var shape in Enum.GetValues<VowelShape>().Where(v => v >= VowelShape.A && v <= VowelShape.IE && character.HasVowel(v) != adding))
            {
                var item = new MenuItem { Header = labels[(int)shape - 1] };
                item.Click += (_, _) =>
                {
                    character.SetVowelEnabled(shape, adding);
                    SaveEmotions(); BuildStateSlots(); AvatarView.RefreshFromService();
                };
                menu.Items.Add(item);
            }
            if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = adding ? "Все звуки уже добавлены" : "Добавленных звуков нет", IsEnabled = false });
            menu.PlacementTarget = anchor; menu.IsOpen = true;
        }
        private void MouthDriver_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingMouthMode || _selectedEmotion == null) return;
            _selectedEmotion.UseVowels = MouthDriver.SelectedIndex == 1;
            SaveEmotions(); BuildStateSlots(); AvatarView.RefreshFromService();
        }
        private bool _loadingMouthMode;
        private void MouthMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingMouthMode || _selectedEmotion == null) return;
            _selectedEmotion.AdvancedMouth = MouthMode.SelectedIndex > 0;
            _selectedEmotion.UseVowels = MouthMode.SelectedIndex == 2;
            SaveEmotions(); BuildStateSlots(); AvatarView.RefreshFromService();
        }
        private void WideMouthThreshold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loadingMouthMode || _selectedEmotion == null) return;
            _selectedEmotion.WideMouthThreshold = e.NewValue; SaveEmotions();
        }
        private void MouthModeHelp_Click(object sender, RoutedEventArgs e) => ShowOverlay("mouthHelp", "Режимы рта", () => new UserControl { Content = new TextBlock
        {
            Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap,
            Text = "Открыт / закрыт — персонаж открывает рот, когда вы говорите.\n\nПо громкости — тихая речь слегка открывает рот, громкая открывает шире.\n\nПо звукам — экспериментальное определение А, О, У и И/Э. Нажмите «+ Добавить звук», чтобы выбрать нужные формы. При неопределённом звуке используется обычный рот.\n\nЗагружайте изображения целого персонажа одинакового размера и положения. С отдельными зрачками нужна одна картинка на форму рта; с глазами на персонаже — две, для открытых и закрытых глаз. Способ глаз выбирается слева в разделе «Глаза и моргание».\n\nПереключение режима сохраняет загруженные картинки. Для речи сначала выберите микрофон и настройте порог слева. Анализ звуков работает локально; шум и особенности голоса могут влиять на точность."
        }});

        private void StateAdd_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not StateSlot slot) return;

            var path = SelectPngFile();
            if (string.IsNullOrEmpty(path)) return;

            var bmp = LoadBitmap(path);
            if (bmp == null) return;

            ApplyStateImage(slot.Key, bmp);
            PersistImage(slot.Key, path);
            slot.Thumbnail = bmp;
            AvatarView?.RefreshFromService();
            UpdateActiveState();
        }

        private void StateClear_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not StateSlot slot) return;

            ApplyStateImage(slot.Key, null);
            PersistImage(slot.Key, "");
            slot.Thumbnail = null;
            AvatarView?.RefreshFromService();
            UpdateActiveState();
        }

        private void ApplyStateImage(string key, BitmapImage? image)
        {
            var s = AvatarService.Instance;
            switch (key)
            {
                // AvatarService properties are declared non-nullable but default to null (legacy).
                case "1": s.MouthOpenEyesOpen = image!; break;
                case "2": s.MouthOpenEyesClosed = image!; break;
                case "3": s.MouthClosedEyesOpen = image!; break;
                case "4": s.MouthClosedEyesClosed = image!; break;
                case "5": s.MouthWideEyesOpen = image; break;
                case "6": s.MouthWideEyesClosed = image; break;
                default:
                    if (int.TryParse(key, out int index) && index >= 7 && index <= 14) s.VowelImages[index - 7] = image;
                    break;
            }
        }

        private void PersistImage(string key, string path)
        {
            if (_selectedEmotion == null) return;
            _selectedEmotion.SetPath(key, path);
            SaveEmotions();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as Button)?.Tag as string)
            {
                case "color":
                    if (_selectedEmotion != null) ShowOverlay("color", "Коррекция цвета", () => new ColorCorrectionView(_selectedEmotion, AvatarView, SaveVisualSettings));
                    break;
                case "clothing":
                    if (_selectedEmotion != null) ShowOverlay("clothing", "Одежда · " + _selectedEmotion.Name, () => new ClothingView(_selectedEmotion, AvatarView, SaveVisualSettings));
                    break;
                case "handsRig":
                    if (_selectedEmotion != null) ShowOverlay("handsRig", "Руки и устройства · " + _selectedEmotion.Name, () => new HandsRigView(_selectedEmotion,AvatarView,SaveVisualSettings,CreateHandsSample));
                    break;
                case "accessories":
                    if (_selectedEmotion != null) ShowOverlay("accessories", "Аксессуары · " + _selectedEmotion.Name, () => new AccessoriesView(_selectedEmotion,AvatarView,SaveVisualSettings));
                    break;
                case "screenLight":
                    if (_selectedEmotion != null) ShowOverlay("screenLight", "Свет от игры", () => new ScreenLightView(_selectedEmotion,AvatarView,SaveVisualSettings));
                    break;
                case "mic": ShowOverlay("mic", "Микрофон", () => new MicrophoneView()); break;
                case "blink":
                    if (_selectedEmotion != null) ShowOverlay("blink", "Моргание", () => new BlinkView(_selectedEmotion, AvatarView, SaveVisualSettings));
                    break;
                case "pupils":
                    if (_selectedEmotion != null) ShowOverlay("pupils", "Глаза · " + _selectedEmotion.Name, () => new PupilsView(_selectedEmotion, AvatarView, SaveVisualSettings, true));
                    break;
                case "effects":
                    if (_selectedEmotion != null) ShowOverlay("effects", "Эффекты · " + _selectedEmotion.Name, () => new EffectsView(_selectedEmotion, AvatarView, SaveVisualSettings));
                    break;
                case "placement":
                    if (_selectedEmotion != null) ShowOverlay("placement", "Размещение", () => new PlacementView(_selectedEmotion, AvatarView, SaveVisualSettings));
                    break;
                case "mouthLayer":
                    if (_selectedEmotion != null) ShowOverlay("mouthLayer", "Рот · отдельный слой", () => new MouthLayerView(_selectedEmotion, AvatarView, SaveVisualSettings, true));
                    break;
                case "background": ShowOverlay("background", "Фон сцены", () => new ObsView()); break;
                case "help": ShowOverlay("help", "Сцена и захват в OBS", () => new ObsHelpView()); break;
                case "settings": ShowOverlay("settings", "Настройки", () => new SettingsView(ApplyInterfacePreferences)); break;
                case "extras": ShowToast("Зрачки, руки и устройства — следующий этап. Сейчас настраиваем основной аватар."); break;
                case "keyboard": ShowToast("Устройства находятся в разработке"); break;
                case "mouse": ShowToast("Устройства находятся в разработке"); break;
                case "gamepad": ShowToast("Устройства находятся в разработке"); break;
                case "hands": ShowToast("Устройства находятся в разработке"); break;
            }
        }

        private void UpdateActiveState()
        {
            _syncingControls = true;
            if (_slotsSeparateEyes != (_selectedEmotion?.Pupils.Enabled == true)) BuildStateSlots();
            BtnEyes.IsEnabled = BtnBlinkSettings.IsEnabled = !AvatarView.IsPupilsTracking;
            BtnEyes.ToolTip = AvatarView.IsPupilsTracking ? "Моргание отдельных глаз — в настройках зрачков" : "Моргание на PNG персонажа";
            BtnEyes.IsChecked = !AvatarView.IsPupilsTracking && AvatarView.IsBlinkActive;
            BtnPupils.IsChecked = AvatarView.IsPupilsTracking;
            if (AvatarView.IsMouthLayerActive)
            {
                foreach (var slot in _stateSlots) slot.IsActive = false;
                VowelStatus.Text = AvatarView.IsMouthLayerPreviewing ? "Проверка отдельного рта" : "Отдельный рот · голос управляет слоем";
                _syncingControls = false;
                return;
            }
            _syncingControls = false;
            bool mouthOpen = AvatarService.Instance.IsTalking;
            bool eyesClosed = AvatarView.IsEyesClosed;
            string active = (mouthOpen, eyesClosed) switch
            {
                (true, false) => "1",
                (true, true) => "2",
                (false, false) => "3",
                (false, true) => "4",
            };
            if (mouthOpen && AvatarView.IsWideMouth) active = eyesClosed ? "6" : "5";
            if (mouthOpen && AvatarView.CurrentVowel != VowelShape.Unknown && _selectedEmotion?.AdvancedMouth == true && _selectedEmotion.UseVowels)
                active = (7 + ((int)AvatarView.CurrentVowel - 1) * 2 + (eyesClosed ? 1 : 0)).ToString();
            VowelStatus.Text = "Форма: " + (mouthOpen ? AvatarView.CurrentVowel switch { VowelShape.A => "А", VowelShape.O => "О", VowelShape.U => "У", VowelShape.IE => "И / Э", _ => "обычный рот" } : "рот закрыт");
            foreach (var slot in _stateSlots)
                slot.IsActive = slot.Key == active;
        }

        private string? SelectPngFile()
        {
            var dlg = new OpenFileDialog
            {
                Filter = "PNG-файлы (*.png)|*.png|Все файлы (*.*)|*.*",
                Title = "Выберите PNG-изображение",
                CheckFileExists = true,
                Multiselect = false
            };

            bool? res = dlg.ShowDialog();
            if(res==true)AvatarView.ReloadImageFiles();
            return res == true ? dlg.FileName : null;
        }

        private BitmapImage? LoadBitmap(string path, bool notify = true)
        {
            try
            {
                var bmp = new BitmapImage();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = null;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                    bmp.Freeze();
                }
                return bmp;
            }
            catch (Exception)
            {
                if (notify) ShowToast("Не удалось загрузить PNG. Проверьте файл и попробуйте снова.");
                return null;
            }
        }
    }
}
