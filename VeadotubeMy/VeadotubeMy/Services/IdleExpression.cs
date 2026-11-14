namespace VeadotubeMy.Services;

public sealed class IdleExpressionOptions
{
    public bool Enabled { get; set; }
    public bool Eyes { get; set; } = true;
    public bool Mouth { get; set; } = true;
    public bool Squint { get; set; } = true;
    public bool Widen { get; set; } = true;
    public bool Bend { get; set; } = true;
    public bool Corner { get; set; } = true;
    public bool Narrow { get; set; } = true;
    public double Strength { get; set; } = .6;
    public double Interval { get; set; } = 20;
    public bool Allows(int kind) => kind switch {0=>Eyes&&Squint,1=>Eyes&&Widen,2=>Mouth&&Bend,3=>Mouth&&Corner,4=>Mouth&&Narrow,_=>false};
    public IdleExpressionOptions Copy() => (IdleExpressionOptions)MemberwiseClone();
}

public sealed class IdleExpression
{
    public static readonly string[] Names=["Прищур","Лёгкое раскрытие глаз","Изгиб рта","Поднятие уголка","Сужение рта"];
    private readonly Random _random;
    private double _wait=-1,_time,_duration,_quiet,_amount;
    private int _kind,_side=1;
    private bool _playing,_preview;
    private readonly Queue<int> _sequence=new();
    public bool Moving => _playing || _amount>.0001 || _sequence.Count>0;
    public string Status => _preview ? "Тест: "+Names[_kind] : _playing ? Names[_kind] : "Ожидание";
    public double Left { get; private set; }
    public double Right { get; private set; }
    public double Mouth { get; private set; }
    public double Asymmetry { get; private set; }
    public double Narrowing { get; private set; }
    public IdleExpression(int? seed=null)=>_random=seed.HasValue?new Random(seed.Value):new Random();
    public void Reset(){_wait=-1;_time=_quiet=_amount=0;_playing=_preview=false;_sequence.Clear();Left=Right=Mouth=Asymmetry=Narrowing=0;}
    public void Preview(int kind=2){_sequence.Clear();Start(Math.Clamp(kind,0,4),true);}
    public void PreviewAll(IdleExpressionOptions o){_sequence.Clear();for(int i=0;i<5;i++)if(o.Allows(i))_sequence.Enqueue(i);if(_sequence.Count>0)Start(_sequence.Dequeue(),true);}
    private void Start(int kind,bool preview){_kind=kind;_playing=true;_preview=preview;_time=0;_duration=preview?2.8:1.8+_random.NextDouble()*1.2;_side=_random.Next(2)==0?-1:1;}
    public void StopPreview(){if(_preview){_playing=false;_sequence.Clear();_wait=-1;}}
    public void Step(double dt,IdleExpressionOptions o,bool talking)
    {
        dt=double.IsFinite(dt)?Math.Clamp(dt,0,.1):0;
        double interval=double.IsFinite(o.Interval)?Math.Clamp(o.Interval,6,90):20;
        double strength=double.IsFinite(o.Strength)?Math.Clamp(o.Strength,0,1):.6;
        double Delay()=>interval*(.6+_random.NextDouble()*.8);
        if(_wait<0)_wait=Delay();
        _quiet=talking?0:_quiet+dt;
        if(talking){if(_playing)_wait=Delay();_playing=false;_sequence.Clear();}
        if((!o.Enabled&&!_preview)||!o.Allows(_kind))_playing=false;
        if(!_playing && _amount==0)
        {
            if(_sequence.Count>0&&!talking){int next=_sequence.Dequeue();if(o.Allows(next))Start(next,true);}
            else _preview=false;
            if(!_playing&&!_preview&&o.Enabled&&!talking&&_quiet>=3)
            {
                _wait-=dt;
                if(_wait<=0){int count=0;for(int i=0;i<5;i++)if(o.Allows(i))count++;if(count>0){int selected=_random.Next(count);for(int i=0;i<5;i++)if(o.Allows(i)&&selected--==0){Start(i,false);break;}}_wait=Delay();}
            }
        }
        double target=0;
        if(_playing){_time+=dt;if(_time>=_duration){_playing=false;_wait=Delay();}else target=Math.Pow(Math.Sin(Math.PI*_time/_duration),2)*strength;}
        _amount+=(target-_amount)*(1-Math.Exp(-dt/(talking?.12:.16)));
        if(_amount<.0001)_amount=0;
        double eyes=o.Eyes?(_kind==0?_amount:_kind==1?-_amount:0):0;
        Left=eyes*(_side<0?1:.7);Right=eyes*(_side<0?.7:1);
        Mouth=o.Mouth&&_kind==2?_amount:0;
        Asymmetry=o.Mouth&&_kind==3?_amount*_side:0;
        Narrowing=o.Mouth&&_kind==4?_amount:0;
    }
}
