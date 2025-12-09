using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SpoutReceiver))]
public class CustomSpoutReceiver : MonoBehaviour
{
    private SpoutReceiver _spoutReceiver;
    [SerializeField]
    private RenderTexture _runtimeTexture;
    [SerializeField]
    private AspectRatioFitter _fitter;
    [SerializeField]
    private RawImage _image;

    void Start()
    {
        _spoutReceiver = GetComponent<SpoutReceiver>();
        StartCoroutine(Setup());
    }

    private IEnumerator Setup()
    {
        if (_spoutReceiver != null)
        {
            RenderTexture temp = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32);
            _spoutReceiver.targetTexture = temp;
            yield return new WaitForSeconds(1);
            _runtimeTexture = new RenderTexture(
                _spoutReceiver.receivedTexture.width,
                _spoutReceiver.receivedTexture.height, 0,
                RenderTextureFormat.ARGB32
            );
            _runtimeTexture.name = _spoutReceiver.sourceName;
            _spoutReceiver.targetTexture = _runtimeTexture;
            _fitter.aspectRatio = (float)_runtimeTexture.width / (float)_runtimeTexture.height;
            _image.texture = _runtimeTexture;
            Destroy(temp);
        }
    }

    void OnDestroy()
    {
        Destroy(_runtimeTexture);
    }
}
