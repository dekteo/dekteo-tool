using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace VeadotubeMy.Views;
public sealed class LayeredArmsEffect:ShaderEffect
{
    static readonly PixelShader Shader=new(){UriSource=new Uri("pack://application:,,,/VeadotubeMy;component/Shaders/LayeredArms.ps")};
    public static readonly DependencyProperty InputProperty=RegisterPixelShaderSamplerProperty("Input",typeof(LayeredArmsEffect),0);
    public Brush Input {get=>(Brush)GetValue(InputProperty);set=>SetValue(InputProperty,value);}
    static DependencyProperty Register(string name,int index)=>DependencyProperty.Register(name,typeof(Point),typeof(LayeredArmsEffect),new UIPropertyMetadata(new Point(),PixelShaderConstantCallback(index)));
    static readonly DependencyProperty Left=Register("Left",0),Right=Register("Right",1),Press=Register("Press",2),Mode=Register("Mode",3);
    static readonly DependencyProperty RightThumb=Register("RightThumb",4),LeftThumb=Register("LeftThumb",5);
    public LayeredArmsEffect(){PixelShader=Shader;foreach(var p in new[]{InputProperty,Left,Right,Press,Mode,RightThumb,LeftThumb})UpdateShaderValue(p);}
    public void Set(Point left,Point right,Point press,bool desk,Point rightThumb=default,Point leftThumb=default){SetValue(Left,left);SetValue(Right,right);SetValue(Press,press);SetValue(Mode,new Point(desk?1:0,0));SetValue(RightThumb,rightThumb);SetValue(LeftThumb,leftThumb);}
}
