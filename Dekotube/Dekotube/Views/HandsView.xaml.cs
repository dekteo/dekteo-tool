using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Dekotube.Services;

namespace Dekotube.Views;

public partial class HandsView : UserControl
{
    public HandsView() { InitializeComponent(); UpdateStatus(); Loaded += (_, _) => { ModeList.SelectedValue = ConfigurationService.LoadOrCreateDefault().HandsInputMode; UpdateStatus(); }; }
    private void Idle_Click(object sender, RoutedEventArgs e) => Select(true);
    private void Active_Click(object sender, RoutedEventArgs e) => Select(false);
    private void Select(bool idle)
    {
        var dialog = new OpenFileDialog { Filter = "PNG (*.png)|*.png|Все файлы (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        ConfigurationService.UpdateDefaultProfile(p => { if (idle) p.HandsIdleImagePath = dialog.FileName; else p.HandsActiveImagePath = dialog.FileName; });
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        var p = ConfigurationService.LoadOrCreateDefault();
        Status.Text = $"Покой: {(string.IsNullOrWhiteSpace(p.HandsIdleImagePath) ? "не выбран" : "выбран")}\nАктивные: {(string.IsNullOrWhiteSpace(p.HandsActiveImagePath) ? "не выбран" : "выбран")}";
        InputStatus.Text = "Последний ввод: " + AvatarService.Instance.LastInputDevice;
    }

    private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeList.SelectedValue is not string mode) return;
        ConfigurationService.UpdateDefaultProfile(p => p.HandsInputMode = mode);
        InputStatus.Text = "Режим: " + (mode == "auto" ? "автоматически" : mode == "gamepad" ? "геймпад" : "клавиатура и мышь");
    }
}
