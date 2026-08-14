using System.Collections;
using Skeletom.Essentials.Animations;
using Skeletom.Essentials.Utils;
using UnityEngine;

public class CameraSwitchAnimation : BaseAnimatedElement
{
    [SerializeField]
    private float _delay = 0.75f;
    [SerializeField]
    private CanvasGroup _canvas;

    protected override IEnumerator Animate()
    {
        _canvas.alpha = 0;
        yield return EnumUtils.GenericWaitForSeconds(_delay, (frame) => { });
        _canvas.alpha = 1;
    }

    protected override void InitializeImplementation()
    {

    }

    protected override void ResetImplementation()
    {

    }
}
