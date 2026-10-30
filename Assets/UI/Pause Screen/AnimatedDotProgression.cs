using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Audio;
using Skeletom.Essentials.Animations;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedDotProgression : BaseAnimatedElement
{
    [SerializeField]
    private List<Image> _dots;
    [SerializeField]
    private Color _onColor;
    [SerializeField]
    private Color _offColor;
    [SerializeField]
    [Range(0.5f, 1.5f)]
    private float _scaleMultiplier;
    protected override IEnumerator Animate()
    {
        while (true)
        {
            int prog = (int)MusicManager.Instance.Beat.currentBeat;
            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].color = prog == i ? _onColor : _offColor;
                _dots[i].transform.localScale = prog == i ? Vector3.one * _scaleMultiplier : Vector3.one;
            }
            yield return null;

        }
    }

    protected override void InitializeImplementation()
    {
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].color =_offColor;
            _dots[i].transform.localScale = Vector3.one;
        }
    }

    protected override void ResetImplementation()
    {
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].color =_offColor;
            _dots[i].transform.localScale = Vector3.one;
        }
    }
}
