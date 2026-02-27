using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ScrollingUV : MonoBehaviour
{
    public Vector2 uvAnimationRate = new Vector2(0.15f, 0.15f);
    public string textureName = "_MainTex";
    private Material _matClone;

    Vector2 uvOffset = Vector2.zero;

    void Start()
    {
        _matClone = Instantiate(GetComponent<Image>().material);
        GetComponent<Image>().material = _matClone;
    }

    void LateUpdate()
    {
        //uvOffset = new Vector2(0.25f/2 * Jukebox.Progression, 0);
        _matClone.SetTextureOffset(textureName, new Vector2(Jukebox.Instance.Sync.currentBeat, Jukebox.Instance.Sync.currentBeat) / Jukebox.Instance.Sync.beatsPerMeasure);
    }
}
