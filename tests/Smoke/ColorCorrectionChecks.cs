using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VeadotubeMy.Services;
using VeadotubeMy.Views;

internal static class ColorCorrectionChecks
{
    public static void Run()
    {
        var app=new VeadotubeMy.App();app.InitializeComponent();
        byte[] Render(ColorCorrectionOptions? options)
        {
            var effect=new ColorCorrectionEffect(); if(options!=null)effect.Apply(options);
            var border=new Border { Width=24,Height=24,Background=new SolidColorBrush(Color.FromArgb(128,120,60,30)),Effect=options==null?null:effect };
            border.Measure(new Size(24,24));border.Arrange(new Rect(0,0,24,24));border.UpdateLayout();
            var bitmap=new RenderTargetBitmap(24,24,96,96,PixelFormats.Pbgra32);bitmap.Render(border);
            byte[] pixels=new byte[24*24*4];bitmap.CopyPixels(pixels,24*4,0);return pixels.Skip((12*24+12)*4).Take(4).ToArray();
        }
        void Check(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine(text);}
        var original=Render(null);var neutral=Render(new());
        Check(original.Zip(neutral).All(v=>Math.Abs(v.First-v.Second)<=2),"Neutral shader preserves source");
        var bright=Render(new(){Brightness=.2});Check(bright[2]>neutral[2]+15,"Brightness changes rendered pixels");
        var gamma=Render(new(){Gamma=2});Check(gamma[1]>neutral[1]+10,"Gamma changes rendered pixels");
        var hue=Render(new(){Hue=120});Check(Math.Abs(hue[1]-neutral[1])>10,"Hue changes rendered pixels");
        Check(new[]{bright,gamma,hue}.All(p=>p[3]==original[3]),"Alpha preserved");
        var contrast=Render(new(){Contrast=2});Check(contrast[1]<neutral[1],"Contrast changes rendered pixels");
        var character=new AvatarEmotion();character.ColorCorrections["all"]=new(){Enabled=true,Gamma=2};
        var copy=character.Duplicate();copy.ColorCorrections["all"].Gamma=1;Check(character.ColorCorrections["all"].Gamma==2,"Copy is independent");
        var json=System.Text.Json.JsonSerializer.Serialize(character);
        Check(System.Text.Json.JsonSerializer.Deserialize<AvatarEmotion>(json)!.ColorCorrections["all"].Gamma==2,"Settings round trip");
        var scene=new AvatarView();scene.ConfigureCharacter(character);scene.RefreshColorCorrection();
        Check(((UIElement)scene.FindName("LivingRig")).Effect is ColorCorrectionEffect,"Global effect attached");
        scene.ConfigureCharacter(new AvatarEmotion());Check(((UIElement)scene.FindName("LivingRig")).Effect==null,"Character switch clears correction");
        var view=new ColorCorrectionView(character,scene,()=>{});
        var root=new Border { Width=460,Height=610,Background=new SolidColorBrush(Color.FromRgb(20,20,20)),Child=view };
        root.Measure(new Size(460,610));root.Arrange(new Rect(0,0,460,610));root.UpdateLayout();
        var preview=new RenderTargetBitmap(460,610,96,96,PixelFormats.Pbgra32);preview.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(preview));
        using var file=System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory,"color-menu.png"));encoder.Save(file);
    }
}
