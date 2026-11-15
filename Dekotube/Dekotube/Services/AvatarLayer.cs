using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dekotube.Services
{
    public enum LayerType
    {
        Base,
        LeftEye,
        RightEye,
        LeftPupil,
        RightPupil,
        Blink,
        Mouth,
        Additional
    }

    public class AvatarLayer
    {
        public LayerType Type { get; set; }
        public string Name { get; set; } = "";
        public BitmapImage ImageSource { get; set; }
        public double ZIndex { get; set; } = 0;
        public Visibility Visibility { get; set; } = Visibility.Visible;
        public Stretch Stretch { get; set; } = Stretch.Uniform;
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Center;
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;
        public Thickness Margin { get; set; } = new Thickness(0);

        public AvatarLayer(LayerType type, string name = "")
        {
            Type = type;
            Name = name;
        }
    }
}