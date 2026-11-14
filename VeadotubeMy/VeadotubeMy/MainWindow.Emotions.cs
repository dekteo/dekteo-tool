using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VeadotubeMy.Services;

namespace VeadotubeMy;

public partial class MainWindow
{
    private readonly ObservableCollection<AvatarEmotion> _emotions = new();
    private AvatarEmotion? _selectedEmotion;
    private bool _loadingEmotions;

    private void InitializeEmotions()
    {
        var profile = ConfigurationService.LoadOrCreateDefault();
        AvatarEmotion.Normalize(profile);
        _loadingEmotions = true;
        foreach (var emotion in profile.Emotions) _emotions.Add(emotion);
        EmotionList.ItemsSource = _emotions;
        EmotionList.SelectedItem = _emotions.First(e => e.Id == profile.SelectedEmotionId);
        _loadingEmotions = false;
        SelectEmotion((AvatarEmotion)EmotionList.SelectedItem, persist: false);
    }

    private void EmotionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingEmotions && EmotionList.SelectedItem is AvatarEmotion emotion)
            SelectEmotion(emotion);
    }

    private void SelectEmotion(AvatarEmotion emotion, bool persist = true)
    {
        if (_currentOverlayKey != null) HideOverlay();
        _selectedEmotion = emotion;
        int missing = 0;
        var paths = emotion.GetPaths();
        for (int i = 0; i < paths.Length; i++)
        {
            var bitmap = string.IsNullOrWhiteSpace(paths[i]) ? null : LoadBitmap(paths[i], notify: false);
            if (!string.IsNullOrWhiteSpace(paths[i]) && bitmap == null) missing++;
            ApplyStateImage((i + 1).ToString(), bitmap);
        }
        EmotionImageWarning.Text = missing > 0 ? $"Недоступно изображений: {missing}. Нажмите + в нужном слоте, чтобы заменить файл." : "";
        EmotionImageWarning.Visibility = missing > 0 ? Visibility.Visible : Visibility.Collapsed;
        BuildStateSlots();
        AvatarView.ConfigureCharacter(emotion);
        AvatarView.RefreshFromService();
        UpdateActiveState();
        if (persist) SaveEmotions();
    }

    private void SaveEmotions()
    {
        if (_selectedEmotion == null) return;
        bool saved = ConfigurationService.UpdateDefaultProfile(p =>
        {
            p.FormatVersion = "2.0";
            p.Emotions = _emotions.ToList();
            p.SelectedEmotionId = _selectedEmotion.Id;
            // Keep the last selected image set readable by older versions.
            p.MouthOpenEyesOpenPath = _selectedEmotion.MouthOpenEyesOpenPath;
            p.MouthOpenEyesClosedPath = _selectedEmotion.MouthOpenEyesClosedPath;
            p.MouthClosedEyesOpenPath = _selectedEmotion.MouthClosedEyesOpenPath;
            p.MouthClosedEyesClosedPath = _selectedEmotion.MouthClosedEyesClosedPath;
        });
        if (!saved) ShowToast("Не удалось сохранить персонажей. Проверьте доступ к папке профилей.");
    }

    private void CreateHandsSample()
    {
        try
        {
            string folder=System.IO.Path.Combine(AppContext.BaseDirectory,"HandsSample");
            var c=System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(System.IO.File.ReadAllText(System.IO.Path.Combine(folder,"character.json")),new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            c.Id=Guid.NewGuid().ToString("N");
            c.MouthLayer.BasePath=System.IO.Path.Combine(folder,"02-head.png");
            c.Head.BodyImagePath=System.IO.Path.Combine(folder,"01-body.png");
            c.Hands.Gamepad=HandsRig.Import(System.IO.Path.Combine(folder,"hands-pack.json"));c.Hands.Gamepad.Y=0;c.Hands.Enabled=true;
            var desk=System.IO.Path.Combine(folder,"Desk","hands-pack.json");if(System.IO.File.Exists(desk)){c.Hands.Desk=HandsRig.Import(desk);c.Hands.Desk.Y=0;if(!c.Hands.Gamepad.LayeredArms)c.Hands.AutoSwitch=true;}
            _emotions.Add(c);EmotionList.SelectedItem=c;
        }
        catch(Exception ex){MessageBox.Show(this,"Не удалось создать тестового персонажа: "+ex.Message);}
    }

    private void AddEmotion_Click(object sender, RoutedEventArgs e)
    {
        var emotion = new AvatarEmotion { Name = $"Персонаж {_emotions.Count + 1}" };
        _emotions.Add(emotion);
        EmotionList.SelectedItem = emotion;
        EditEmotion_Click(sender, e);
    }

    private void DuplicateEmotion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEmotion == null) return;
        var copy = _selectedEmotion.Duplicate();
        _emotions.Add(copy);
        EmotionList.SelectedItem = copy;
    }

    private void EditEmotion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEmotion is not { } emotion) return;
        ShowOverlay("emotion", "Переименовать персонажа", () =>
        {
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = "Имя персонажа", Margin = new Thickness(0, 0, 0, 8) });
            var name = new TextBox { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 33, 36)), Foreground = System.Windows.Media.Brushes.White, CaretBrush = System.Windows.Media.Brushes.White, Text = emotion.Name, MaxLength = 60, FontSize = 14, Padding = new Thickness(8) };
            panel.Children.Add(name);
            var save = new Button { Content = "Сохранить название", Margin = new Thickness(0, 12, 0, 12) };
            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return; }
                emotion.Name = name.Text.Trim();
                SaveEmotions();
                HideOverlay();
            };
            panel.Children.Add(save);
            return new UserControl { Content = panel };
        });
    }

    private void DeleteEmotion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEmotion is not { } emotion) return;
        if (_emotions.Count <= 1) { ShowToast("Нельзя удалить последнего персонажа. Сначала добавьте нового."); return; }
        if (MessageBox.Show(this, $"Удалить персонажа «{emotion.Name}»? PNG-файлы останутся на диске.",
            "Удаление персонажа", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        HideOverlay();
        _loadingEmotions = true;
        _emotions.Remove(emotion);
        EmotionList.SelectedItem = _emotions[0];
        _loadingEmotions = false;
        SelectEmotion(_emotions[0]);
    }

    private bool HandleEmotionShortcut(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is TextBox) return false;
        int index = (int)e.Key - (int)Key.D1;
        if (index < 0 || index >= Math.Min(9, _emotions.Count)) return false;
        EmotionList.SelectedIndex = index;
        e.Handled = true;
        return true;
    }
}
