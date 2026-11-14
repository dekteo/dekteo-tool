namespace VeadotubeMy.Services;

/// <summary>A named set of the four mouth/blink images. IDs survive renaming.</summary>
public sealed class AvatarEmotion : System.ComponentModel.INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double MouthTransitionMs { get; set; }
    public MouthLayerOptions MouthLayer { get; set; } = new();
    public List<AccessoryOptions> Accessories { get; set; } = new();
    public HandsOptions Hands {get;set;}=new();
    public ClothingOptions Clothing {get;set;}=new();
    public VoiceLightsOptions VoiceLights {get;set;}=new();
    public RobotScreenOptions RobotScreen {get;set;}=new();
    public IdleExpressionOptions IdleExpression { get; set; } = new();
    public ScreenLightOptions ScreenLight { get; set; } = new();
    public List<string> HiddenParts { get; set; } = new();
    public Dictionary<string,ColorCorrectionOptions> ColorCorrections { get; set; } = new();
    public double SceneX { get; set; }
    public double SceneY { get; set; }
    public double SceneScale { get; set; } = 1;
    public double HeadX { get; set; }
    public double HeadY { get; set; }
    public double HeadScale { get; set; } = 1;
    public double BodyX { get; set; }
    public double BodyY { get; set; }
    public double BodyScale { get; set; } = 1;
    public List<string>? AddedEffectCards { get; set; }
    public bool LightEnabled { get; set; }
    public string LightImagePath { get; set; } = "";
    public double LightOpacity { get; set; } = .35;
    public HeadOptions Head { get; set; } = new();
    public PupilOptions Pupils { get; set; } = new();
    public List<CharacterEffect> Effects { get; set; } = new();
    public LightingOptions Lighting { get; set; } = new();
    public ScreenEffectOptions ScreenEffect { get; set; } = new();
    private string _name = "Персонаж 1";
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
        }
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string MouthOpenEyesOpenPath { get; set; } = "";
    public string MouthOpenEyesClosedPath { get; set; } = "";
    public string MouthClosedEyesOpenPath { get; set; } = "";
    public string MouthClosedEyesClosedPath { get; set; } = "";

    public bool UseVowels { get; set; }
    public List<VowelShape>? EnabledVowels { get; set; }
    public bool HasVowel(VowelShape shape) => shape != VowelShape.Unknown &&
        (EnabledVowels?.Contains(shape) ?? Enumerable.Range(0, 8).Any(i => i / 2 + 1 == (int)shape && !string.IsNullOrWhiteSpace(VowelPaths.ElementAtOrDefault(i))));
    public void SetVowelEnabled(VowelShape shape, bool enabled)
    {
        if (shape < VowelShape.A || shape > VowelShape.IE) return;
        EnabledVowels ??= Enum.GetValues<VowelShape>().Where(HasVowel).ToList();
        if (enabled && !EnabledVowels.Contains(shape)) EnabledVowels.Add(shape);
        if (!enabled) EnabledVowels.Remove(shape);
    }
    public string[] VowelPaths { get; set; } = new string[8];
    public bool AdvancedMouth { get; set; }
    public double WideMouthThreshold { get; set; } = 65;
    public string MouthWideEyesOpenPath { get; set; } = "";
    public string MouthWideEyesClosedPath { get; set; } = "";

    public IEnumerable<int> VisibleSlots()
    {
        int step = Pupils.Enabled ? 2 : 1;
        for (int slot = 1; slot <= 4; slot += step) yield return slot;
        if (!AdvancedMouth) yield break;
        if (!UseVowels) { for (int slot = 5; slot <= 6; slot += step) yield return slot; yield break; }
        for (int i = 0; i < 8; i += step)
            if (HasVowel((VowelShape)(i / 2 + 1))) yield return i + 7;
    }
    public string[] GetPaths() => [MouthOpenEyesOpenPath, MouthOpenEyesClosedPath,
        MouthClosedEyesOpenPath, MouthClosedEyesClosedPath, MouthWideEyesOpenPath, MouthWideEyesClosedPath, .. VowelPaths];

    public void SetPath(string slot, string path)
    {
        switch (slot)
        {
            case "1": MouthOpenEyesOpenPath = path; break;
            case "2": MouthOpenEyesClosedPath = path; break;
            case "3": MouthClosedEyesOpenPath = path; break;
            case "4": MouthClosedEyesClosedPath = path; break;
            case "5": MouthWideEyesOpenPath = path; break;
            case "6": MouthWideEyesClosedPath = path; break;
            default:
                if (int.TryParse(slot, out int index) && index >= 7 && index <= 14) VowelPaths[index - 7] = path;
                else throw new ArgumentOutOfRangeException(nameof(slot));
                break;
        }
    }

    public AvatarEmotion Duplicate() => new()
    {
        Name = Name + " — копия",
        EnabledVowels = EnabledVowels?.ToList(),
        UseVowels = UseVowels, VowelPaths = (string[])VowelPaths.Clone(),
        AdvancedMouth = AdvancedMouth, WideMouthThreshold = WideMouthThreshold,
        MouthWideEyesOpenPath = MouthWideEyesOpenPath, MouthWideEyesClosedPath = MouthWideEyesClosedPath,
        MouthTransitionMs = MouthTransitionMs,
        MouthLayer = MouthLayer.Copy(),
        SceneX = SceneX, SceneY = SceneY, SceneScale = SceneScale, HeadX = HeadX, HeadY = HeadY, HeadScale = HeadScale,
        BodyX = BodyX, BodyY = BodyY, BodyScale = BodyScale,
        AddedEffectCards = AddedEffectCards?.ToList(),
        LightEnabled = LightEnabled, LightImagePath = LightImagePath, LightOpacity = LightOpacity,
        Head = Head.Copy(),
        ScreenLight = (ScreenLight ?? new()).Copy(),
        IdleExpression = (IdleExpression ?? new()).Copy(),
        RobotScreen = (RobotScreen ?? new()).Copy(),
        Hands = (Hands ?? new()).Copy(),
        Clothing = (Clothing ?? new()).Copy(),
        VoiceLights = (VoiceLights ?? new()).Copy(),
        Accessories = (Accessories ?? new()).Select(a=>a.Copy()).ToList(),
        HiddenParts = HiddenParts?.ToList() ?? new(),
        ColorCorrections = (ColorCorrections ?? new()).ToDictionary(p=>p.Key,p=>p.Value.Copy()),
        Pupils = Pupils.Copy(),
        Effects = Effects.Select(e => e.Copy()).ToList(),
        Lighting = Lighting.Copy(),
        ScreenEffect = ScreenEffect.Copy(),
        MouthOpenEyesOpenPath = MouthOpenEyesOpenPath,
        MouthOpenEyesClosedPath = MouthOpenEyesClosedPath,
        MouthClosedEyesOpenPath = MouthClosedEyesOpenPath,
        MouthClosedEyesClosedPath = MouthClosedEyesClosedPath
    };

    public static void Normalize(AvatarProfile profile)
    {
        profile.Emotions ??= [];
        profile.Emotions.RemoveAll(e => e is null);
        if (profile.Emotions.Count == 0)
            profile.Emotions.Add(new AvatarEmotion
            {
                MouthOpenEyesOpenPath = profile.MouthOpenEyesOpenPath,
                MouthOpenEyesClosedPath = profile.MouthOpenEyesClosedPath,
                MouthClosedEyesOpenPath = profile.MouthClosedEyesOpenPath,
                MouthClosedEyesClosedPath = profile.MouthClosedEyesClosedPath
            });
        var ids = new HashSet<string>();
        foreach (var emotion in profile.Emotions)
        {
            var vowelPaths = emotion.VowelPaths ?? [];
            Array.Resize(ref vowelPaths, 8); emotion.VowelPaths = vowelPaths;
            emotion.EnabledVowels ??= Enum.GetValues<VowelShape>().Where(emotion.HasVowel).ToList();
            emotion.EnabledVowels = emotion.EnabledVowels.Where(v => v >= VowelShape.A && v <= VowelShape.IE).Distinct().ToList();
            emotion.Head ??= new();
            emotion.MouthLayer ??= new();
            emotion.MouthLayer.VowelPaths ??= new();
            emotion.ScreenEffect ??= new();
            emotion.Pupils ??= new();
            emotion.Pupils.Left ??= new();
            emotion.Pupils.Right ??= new() { X = .55 };
            emotion.Effects ??= new();
            emotion.Effects.RemoveAll(e => e is null);
            if (string.IsNullOrWhiteSpace(emotion.Id) || !ids.Add(emotion.Id))
            {
                emotion.Id = Guid.NewGuid().ToString("N");
                ids.Add(emotion.Id);
            }
            emotion.Name = string.IsNullOrWhiteSpace(emotion.Name) ? "Без названия" : emotion.Name.Trim();
        }
        if (!profile.Emotions.Any(e => e.Id == profile.SelectedEmotionId))
            profile.SelectedEmotionId = profile.Emotions[0].Id;
        if (profile.PupilSettingsVersion == 0)
        {
            var selected = profile.Emotions.First(e => e.Id == profile.SelectedEmotionId);
            if (string.IsNullOrEmpty(selected.Pupils.Left.ImagePath)) selected.Pupils.Left.ImagePath = profile.LeftPupilImagePath;
            if (string.IsNullOrEmpty(selected.Pupils.Right.ImagePath)) selected.Pupils.Right.ImagePath = profile.RightPupilImagePath;
            // Old regions used window coordinates; keep the PNGs but require fresh image-local calibration.
            profile.PupilSettingsVersion = 1;
        }
    }
}
