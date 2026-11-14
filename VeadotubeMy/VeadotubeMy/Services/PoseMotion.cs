namespace VeadotubeMy.Services;

// Slow lean, hold, return, then an irregular pause. Time advances only while rendered.
public sealed class PoseMotion
{
    private readonly Random _random;
    private double _elapsed, _wait=8, _body, _head;
    private bool _testing;
    public PoseMotion(int? seed=null) => _random=seed.HasValue?new Random(seed.Value):new Random();
    public bool Moving => _testing || Math.Abs(_body)>.0001 || Math.Abs(_head)>.0001;
    public void Reset(){_elapsed=0;_wait=8;_body=_head=0;_testing=false;}
    public void Preview(){_elapsed=0;_wait=0;_testing=true;}
    public void CancelPreview(){_testing=false;_elapsed=0;_wait=8;}
    public (double Body,double Head) Step(double dt,bool enabled,double amount)
    {
        dt=double.IsFinite(dt)?Math.Clamp(dt,0,.1):0;
        amount=double.IsFinite(amount)?Math.Clamp(amount,0,2):0;
        double target=0;
        if(enabled || _testing)
        {
            _elapsed+=dt;
            double t=_elapsed-_wait;
            static double Ease(double x){x=Math.Clamp(x,0,1);return x*x*x*(x*(x*6-15)+10);}
            if(t>=0 && t<3)target=Ease(t/3);
            else if(t<6 && t>=3)target=1;
            else if(t>=6 && t<10)target=1-Ease((t-6)/4);
            else if(t>=10){_elapsed=0;_wait=12+_random.NextDouble()*16;_testing=false;}
        }
        else {_elapsed=0;_wait=8;}
        _body+=(target*amount-_body)*(1-Math.Exp(-dt/.35));
        _head+=(_body-_head)*(1-Math.Exp(-dt/.55));
        if(!enabled && !_testing && !Moving)_body=_head=0;
        return (_body,_head);
    }
}
