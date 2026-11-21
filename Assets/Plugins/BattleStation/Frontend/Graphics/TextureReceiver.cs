using Klak.Spout;
using UnityEngine;
using UnityEngine.UI;

public class TextureReceiver : MonoBehaviour
{
    [SerializeField]
    private RawImage _target;

    private SpoutReceiver _receiver;
    private bool _sourceExists;
    private RenderTexture _tex;

    private void OnEnable()
    {
        _receiver = GetComponent<SpoutReceiver>();
        RenderTexture runtimeRenderTexture = new(1920, 1080, 24, RenderTextureFormat.ARGB32);
        runtimeRenderTexture.useMipMap = true;
        runtimeRenderTexture.Create();
        _receiver.targetTexture = runtimeRenderTexture;
        _target.texture = runtimeRenderTexture;
    }

    private void Update()
    {
        bool sourceCheck = CheckSourceExists();
        if (_sourceExists && !sourceCheck)
        {
            // source stopped, delete texture
            _target.enabled = false;
            if (_tex)
            {
                _receiver.targetTexture = null;
                _target.texture = null;
                Destroy(_tex);
            }
        }
        else if (!_sourceExists && sourceCheck)
        {
            // source started, create texture
            _tex = new(_receiver.receivedTexture.width, _receiver.receivedTexture.height, 24, RenderTextureFormat.ARGB32)
            {
                useMipMap = true,
                filterMode = FilterMode.Trilinear
            };
            _tex.Create();
            _receiver.targetTexture = _tex;
            _target.texture = _tex;
            _target.enabled = true;
        }
        _sourceExists = sourceCheck;
    }

    private bool CheckSourceExists()
    {
        foreach (var name in SpoutManager.GetSourceNames())
        {
            if (name == _receiver.sourceName && _receiver.receivedTexture != null)
            {
                return true;
            }
        }
        return false;
    }

}
