using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Animations;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedStrobe : BaseAnimatedElement
{
    [SerializeField]
    private Graphic image;

    public float sin;

    protected override void ResetImplementation()
    {
    }

    protected override IEnumerator Animate()
    {
        while (true)
        {
            float currentBeat = Jukebox.Instance.Sync.currentBeat % 2;
            sin = Mathf.Sin(360 * Mathf.Deg2Rad * currentBeat / 2f);  
            image.enabled = sin > 0;
            yield return null;
            // yield return new WaitForSeconds(2f / Jukebox.Instance.Sync.beatsPerMeasure);
            // image.enabled = false;
            // yield return new WaitForSeconds(2f / Jukebox.Instance.Sync.beatsPerMeasure);
        }
    }

    protected override void InitializeImplementation()
    {

    }
}
