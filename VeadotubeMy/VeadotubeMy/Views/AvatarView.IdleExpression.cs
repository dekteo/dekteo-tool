using System.Windows.Media;
using VeadotubeMy.Services;
namespace VeadotubeMy.Views;
public partial class AvatarView
{
    private readonly IdleExpression _idleExpression=new();
    private readonly ScaleTransform _leftExpression=new(),_rightExpression=new();
    private void InitializeIdleExpression()
    {
        ((TransformGroup)LeftPupilSprite.RenderTransform).Children.Add(_leftExpression);
        ((TransformGroup)RightPupilSprite.RenderTransform).Children.Add(_rightExpression);
    }
    public string IdleExpressionStatus=>_idleExpression.Status;
    public event EventHandler? IdleExpressionChanged;
    private string _expressionStatus="";
    public void PreviewIdleExpression(){_idleExpression.PreviewAll(_character!.IdleExpression);UpdateRenderingSubscription();}
    public void StopIdleExpressionPreview(){_idleExpression.StopPreview();UpdateRenderingSubscription();}
    private void ResetIdleExpression(){_idleExpression.Reset();_leftExpression.ScaleX=_leftExpression.ScaleY=_rightExpression.ScaleX=_rightExpression.ScaleY=1;}
    private void RenderIdleExpression(double dt)
    {
        if(_editingEye!=null || IsMouthCurveEditing || _placingMouth || IsAccessoryEditing || _screenEditing || MouthLayerPreview){ResetIdleExpression();return;}
        _idleExpression.Step(dt,_character!.IdleExpression,AvatarService.Instance.IsTalking);
        if(_expressionStatus!=_idleExpression.Status){_expressionStatus=_idleExpression.Status;IdleExpressionChanged?.Invoke(this,EventArgs.Empty);}
        bool eyes=_character.Pupils.Enabled;
        _leftExpression.ScaleY=1-(eyes?_idleExpression.Left*.42:0);
        _rightExpression.ScaleY=1-(eyes?_idleExpression.Right*.42:0);
        _leftExpression.ScaleX=1+(eyes?_idleExpression.Left*.06:0);
        _rightExpression.ScaleX=1+(eyes?_idleExpression.Right*.06:0);
    }
}
