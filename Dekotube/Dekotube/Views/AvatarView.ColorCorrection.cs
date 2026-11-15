using System.Windows;
using Dekotube.Services;

namespace Dekotube.Views;

public partial class AvatarView
{
    public void RefreshColorCorrection()
    {
        if (_character == null) return;
        _character.ColorCorrections ??= new();
        _screenLightEffects.Clear();
        bool protect=_character.ScreenLight?.ProtectScreen==true;
        void Apply(UIElement target,string key)
        {
            bool lit=_character.ScreenLight?.Enabled==true && (_character.ScreenLight.Target==key || _character.ScreenLight.Target=="head" && key=="base");
            if(protect)
                lit=_character.ScreenLight?.Enabled==true && (key=="screenHead" && _character.ScreenLight.Target!="body" || (key=="body" || key=="accessories") && _character.ScreenLight.Target=="all" || key=="body" && _character.ScreenLight.Target=="body");
            _character.ColorCorrections.TryGetValue(key,out var options);
            if (options?.Enabled!=true && !lit)
            { if(target.Effect is ColorCorrectionEffect) target.Effect=null; return; }
            var effect=target.Effect as ColorCorrectionEffect ?? new ColorCorrectionEffect();
            effect.Apply(options?.Enabled==true?options:new ColorCorrectionOptions()); target.Effect=effect;
            effect.SetLight(_screenLeft,_screenRight,lit?_character.ScreenLight!.Strength:0);
            effect.Protection=key=="screenHead"?ScreenProtection():ColorCorrectionEffect.NoProtection;
            effect.SetDarkness(_screenLightReady && lit && _character.ScreenLight!.Darken?_character.ScreenLight.DarkenStrength:0);
            if(lit)_screenLightEffects.Add(effect);
        }
        Apply(LivingRig,"all");
        Apply(Composite,"screenHead");
        Apply(_accessoryHosts[0],"accessories");Apply(_accessoryHosts[1],"accessories");
        Apply(StateImage,"base"); Apply(PreviousStateImage,"base");
        Apply(_handsHost,"body"); Apply(_handsPrevious,"body");
        Apply(BodyImage,"body"); Apply(_mouthCanvas,"mouth");
        Apply(LeftPupilOverlay,"leftPupil"); Apply(RightPupilOverlay,"rightPupil");
        Apply(LeftEyeWhite,"leftWhite"); Apply(RightEyeWhite,"rightWhite");
        Apply(LeftEyeForegroundImage,"leftLids"); Apply(LeftHalfEyeImage,"leftLids"); Apply(LeftClosedEyeImage,"leftLids");
        Apply(RightEyeForegroundImage,"rightLids"); Apply(RightHalfEyeImage,"rightLids"); Apply(RightClosedEyeImage,"rightLids");
    }
}
