using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Animations;
using UnityEngine;
using UnityEngine.Events;

public class AnimatedFade : BaseAnimatedElement
{
    [SerializeField]
    private CanvasGroup _group;

    private float _target;
    private float _duration;

    public void FadeTo(float target, float duration, UnityAction onFinishCallback = null)
    {
        StopAnimation(false);
        _target = target;
        _duration = duration;
        StartAnimation(onFinishCallback);
    }

    protected override IEnumerator Animate()
    {
        float t = 0;
        float initial = _group.alpha;
        if (_target > _group.alpha)
        {
            do
            {
                _group.alpha = Mathf.Lerp(initial, _target, t / _duration);
                t += Time.deltaTime;
                yield return null;
            } while (_group.alpha < _target);
        }
        else
        {
            do
            {
                _group.alpha = Mathf.Lerp(initial, _target, t / _duration);
                t += Time.deltaTime;
                yield return null;
            } while (_group.alpha > _target);
        }
    }

    protected override void InitializeImplementation()
    {
        _group.alpha = 0;
    }

    protected override void ResetImplementation()
    {
        _group.alpha = 0;
    }
}
