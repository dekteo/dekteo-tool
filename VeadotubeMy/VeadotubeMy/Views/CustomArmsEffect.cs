using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public sealed class CustomArmsEffect:ShaderEffect
{
    static readonly PixelShader Shader=new(){UriSource=new Uri("pack://application:,,,/VeadotubeMy;component/Shaders/CustomArms.ps")};
    public static readonly DependencyProperty InputProperty=RegisterPixelShaderSamplerProperty("Input",typeof(CustomArmsEffect),0);
    public Brush Input {get=>(Brush)GetValue(InputProperty);set=>SetValue(InputProperty,value);}
    static readonly DependencyProperty Areas=RegisterPixelShaderSamplerProperty("Areas",typeof(CustomArmsEffect),1);
    static readonly DependencyProperty Fingers=RegisterPixelShaderSamplerProperty("Fingers",typeof(CustomArmsEffect),2);
    static readonly DependencyProperty Device=RegisterPixelShaderSamplerProperty("Device",typeof(CustomArmsEffect),3);
    static DependencyProperty Register(string name,int index)=>DependencyProperty.Register(name,typeof(Point),typeof(CustomArmsEffect),new UIPropertyMetadata(new Point(),PixelShaderConstantCallback(index)));
    static readonly DependencyProperty Left=Register("Left",0),Right=Register("Right",1),Press=Register("Press",2),RightThumb=Register("RightThumb",3),LeftThumb=Register("LeftThumb",4);
    public CustomArmsEffect(){PixelShader=Shader;foreach(var p in new[]{InputProperty,Areas,Fingers,Device,Left,Right,Press,RightThumb,LeftThumb})UpdateShaderValue(p);}
    public void Configure(ArmMotionMap map,int width,int height,bool desk=false){if(desk){map=map.Copy();for(int i=4;i<map.Parts.Count;i++)map.Parts[i].Area=new();}var textures=map.Bake(width,height);var properties=new[]{Areas,Fingers,Device};for(int i=0;i<3;i++){var brush=new ImageBrush(textures[i]){Stretch=Stretch.Fill};brush.Freeze();SetValue(properties[i],brush);}}
    public void Set(Point left,Point right,Point press,Point rightThumb=default,Point leftThumb=default){SetValue(Left,left);SetValue(Right,right);SetValue(Press,press);SetValue(RightThumb,rightThumb);SetValue(LeftThumb,leftThumb);}
}
