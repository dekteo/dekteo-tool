using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Dekotube.Services;

namespace Dekotube.Views;

public sealed class ColorCorrectionEffect : ShaderEffect
{
    public static readonly Brush NoProtection = CreateNoProtection();
    private static Brush CreateNoProtection(){var image=System.Windows.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,0,0,255},4);image.Freeze();var brush=new ImageBrush(image);brush.Freeze();return brush;}
    private static readonly PixelShader Shader = new() { UriSource = new Uri("pack://application:,,,/Dekotube;component/Shaders/ColorCorrection.ps") };
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input",typeof(ColorCorrectionEffect),0);
    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty,value); }
    public static readonly DependencyProperty ProtectionProperty = RegisterPixelShaderSamplerProperty("Protection",typeof(ColorCorrectionEffect),1);
    public Brush Protection { get => (Brush)GetValue(ProtectionProperty); set => SetValue(ProtectionProperty,value); }
    public static readonly DependencyProperty DarknessProperty = DependencyProperty.Register("Darkness",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(0.0,PixelShaderConstantCallback(9)));
    public void SetDarkness(double value)=>SetValue(DarknessProperty,Math.Clamp(value,0,.9));
    public static readonly DependencyProperty BrightnessProperty = DependencyProperty.Register("Brightness",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(0.0,PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty ContrastProperty = DependencyProperty.Register("Contrast",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(1.0,PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty GammaProperty = DependencyProperty.Register("Gamma",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(1.0,PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty HueProperty = DependencyProperty.Register("Hue",typeof(Point),typeof(ColorCorrectionEffect),new UIPropertyMetadata(new Point(1,0),PixelShaderConstantCallback(3)));
    public static readonly DependencyProperty LightLeftProperty = DependencyProperty.Register("LightLeft",typeof(Color),typeof(ColorCorrectionEffect),new UIPropertyMetadata(Colors.Black,PixelShaderConstantCallback(4)));
    public static readonly DependencyProperty LightRightProperty = DependencyProperty.Register("LightRight",typeof(Color),typeof(ColorCorrectionEffect),new UIPropertyMetadata(Colors.Black,PixelShaderConstantCallback(5)));
    public static readonly DependencyProperty LightStrengthProperty = DependencyProperty.Register("LightStrength",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(0.0,PixelShaderConstantCallback(6)));
    public static readonly DependencyProperty GlintPositionProperty = DependencyProperty.Register("GlintPosition",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(0.5,PixelShaderConstantCallback(7)));
    public static readonly DependencyProperty GlintStrengthProperty = DependencyProperty.Register("GlintStrength",typeof(double),typeof(ColorCorrectionEffect),new UIPropertyMetadata(0.0,PixelShaderConstantCallback(8)));
    public static readonly DependencyProperty ShineStartProperty=DependencyProperty.Register("ShineStart",typeof(Point),typeof(ColorCorrectionEffect),new UIPropertyMetadata(new Point(0,0),PixelShaderConstantCallback(10)));
    public static readonly DependencyProperty ShineEndProperty=DependencyProperty.Register("ShineEnd",typeof(Point),typeof(ColorCorrectionEffect),new UIPropertyMetadata(new Point(1,1),PixelShaderConstantCallback(11)));
    public void SetShineRegion(AccessoryOptions o){SetValue(ShineStartProperty,o.ShineRegion?new Point(o.ShineX,o.ShineY):new Point(0,0));SetValue(ShineEndProperty,o.ShineRegion?new Point(o.ShineX+o.ShineWidth,o.ShineY+o.ShineHeight):new Point(1,1));}
    public void SetGlint(double position,double strength){SetValue(GlintPositionProperty,position);SetValue(GlintStrengthProperty,Math.Clamp(strength,0,1));}
    public void SetLight(Color left,Color right,double strength){SetValue(LightLeftProperty,left);SetValue(LightRightProperty,right);SetValue(LightStrengthProperty,Math.Clamp(strength,0,1));}
    public ColorCorrectionEffect()
    {
        PixelShader=Shader;
        UpdateShaderValue(ShineStartProperty);UpdateShaderValue(ShineEndProperty);
        Protection=NoProtection;UpdateShaderValue(ProtectionProperty);UpdateShaderValue(DarknessProperty);
        foreach(var property in new[]{InputProperty,BrightnessProperty,ContrastProperty,GammaProperty,HueProperty,LightLeftProperty,LightRightProperty,LightStrengthProperty,GlintPositionProperty,GlintStrengthProperty}) UpdateShaderValue(property);
    }
    public void Apply(ColorCorrectionOptions o)
    {
        static double Safe(double v,double min,double max,double fallback) => double.IsFinite(v)?Math.Clamp(v,min,max):fallback;
        SetValue(BrightnessProperty,Safe(o.Brightness,-1,1,0));
        SetValue(ContrastProperty,Safe(o.Contrast,0,3,1));
        SetValue(GammaProperty,Safe(o.Gamma,.2,3,1));
        double angle=Safe(o.Hue,-180,180,0)*Math.PI/180;
        SetValue(HueProperty,new Point(Math.Cos(angle),Math.Sin(angle)));
    }
}
