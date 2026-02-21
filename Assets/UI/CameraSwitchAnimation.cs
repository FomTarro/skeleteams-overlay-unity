using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using Skeletom.Essentials.Animations;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.UI;

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
