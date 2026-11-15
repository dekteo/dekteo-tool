using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dekotube.Services;
namespace Dekotube.Views;
public partial class SettingsView : UserControl
{
    public SettingsView(Action apply)
    {
        InitializeComponent();
        var profile = ConfigurationService.LoadOrCreateDefault();
        SettingsPanel.Children.Add(EditorControls.Label("Интерфейс приложения"));
        var hint = EditorControls.Label("");
        void Save()
        {
            bool saved = ConfigurationService.UpdateDefaultProfile(p => { p.AutoHideInterface = profile.AutoHideInterface; p.InterfaceIdleSeconds = profile.InterfaceIdleSeconds; });
            hint.Text = saved ? "Настройки сохранены" : "Не удалось сохранить настройки";
            if (saved) apply();
        }
        var enabled = new CheckBox { Content = "Скрывать управление при бездействии", IsChecked = profile.AutoHideInterface, Foreground = Brushes.White };
        SettingsPanel.Children.Add(enabled);
        var delay = EditorControls.Slider(SettingsPanel, "Время до скрытия (сек.)", profile.InterfaceIdleSeconds, 3, 60, v => { profile.InterfaceIdleSeconds = (int)Math.Round(v); }, "0");
        delay.IsSnapToTickEnabled = true; delay.TickFrequency = 1; delay.IsEnabled = profile.AutoHideInterface;
        enabled.Checked += (_, _) => { profile.AutoHideInterface = true; delay.IsEnabled = true; Save(); };
        enabled.Unchecked += (_, _) => { profile.AutoHideInterface = false; delay.IsEnabled = false; Save(); };
        delay.PreviewMouseLeftButtonUp += (_, _) => Save();
        delay.PreviewKeyUp += (_, _) => Save();
        Unloaded += (_, _) => Save();
        SettingsPanel.Children.Add(EditorControls.Label("Клик по сцене возвращает управление. F8 скрывает и показывает его вручную. Открытые настройки не исчезают автоматически."));
        SettingsPanel.Children.Add(hint);
    }
}
