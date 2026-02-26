using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Animations;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedStrobe : BaseAnimatedElement
{
    [SerializeField]
    private Graphic image;

    protected override void ResetImplementation()
    {
    }

    protected override IEnumerator Animate()
    {
        while (true)
        {
            image.enabled = true;
            yield return new WaitForSeconds(2f / Jukebox.Instance.Sync.beatsPerMeasure);
            image.enabled = false;
            yield return new WaitForSeconds(2f / Jukebox.Instance.Sync.beatsPerMeasure);
        }
    }

    protected override void InitializeImplementation()
    {

    }
}
