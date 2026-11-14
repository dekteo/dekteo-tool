using System.ComponentModel;
using System.Windows.Media;

namespace VeadotubeMy
{
    /// <summary>
    /// A single PNG state slot in the right panel. Data-driven so the panel
    /// is not hard-wired to exactly four elements (future avatars/states).
    /// </summary>
    public class StateSlot : INotifyPropertyChanged
    {
        /// <summary>Stable key mapping to an AvatarService state property ("1".."4").</summary>
        public string Key { get; set; } = "";

        /// <summary>Short caption shown under the thumbnail.</summary>
        public string Caption { get; set; } = "";
        public string MouthGroup => Caption.Split('·')[0].Trim();
        public string EyeCaption => Caption.Contains('·') ? Caption.Split('·')[1].Trim() : "Изображение персонажа";

        /// <summary>Full state description for the ToolTip.</summary>
        public string ToolTip { get; set; } = "";

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
                OnPropertyChanged(nameof(IsFilled));
            }
        }

        public bool IsFilled => Thumbnail != null;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
