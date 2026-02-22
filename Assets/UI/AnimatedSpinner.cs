using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Animations;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedSpinner : BaseAnimatedElement
{

    [SerializeField]
    private Image image;

    public Vector3 rotationDirSpeed = Vector3.zero;

    protected override void ResetImplementation()
    {
    }

    protected override IEnumerator Animate()
    {
        while (true)
        {
            Vector3 rotateOffset = new Vector3(0, 0, (360f * Jukebox.Instance.SyncInfo.currentBeat));
            image.transform.eulerAngles = rotateOffset;
            yield return null;
        }
    }

    protected override void InitializeImplementation()
    {

    }
}
