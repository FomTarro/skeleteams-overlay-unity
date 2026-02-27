using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Animations;
using Skeletom.Essentials.Utils;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedSpinner : BaseAnimatedElement
{

    [SerializeField]
    private Image image;

    public Vector3 rotationDirSpeed = Vector3.zero;

    public int beatsPerCycle = 4;

    protected override void ResetImplementation()
    {
    }

    protected override IEnumerator Animate()
    {
        while (true)
        {
            float deg = MathUtils.Normalize(Jukebox.Instance.Sync.currentBeat, 0, Jukebox.Instance.Sync.beatsPerMeasure, 0, 360);
            image.transform.eulerAngles = new Vector3(0, 0, deg);
            yield return null;
        }
    }

    protected override void InitializeImplementation()
    {

    }
}
