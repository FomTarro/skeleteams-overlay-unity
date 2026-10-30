using Klak.Spout;
using UnityEngine;
using UnityEngine.UI;

public class TextureReceiver : MonoBehaviour
{
    [SerializeField]
    private RawImage _target;

    private SpoutReceiver _receiver;

    private bool _sourceExists;

    private void OnEnable()
    {
        _receiver = GetComponent<SpoutReceiver>();
    }

    private void Update()
    {
        _sourceExists = CheckSourceExists();
        if (_target != null && _receiver != null)
        {
            _target.texture = _receiver.receivedTexture;
            _target.enabled = _sourceExists && _receiver.receivedTexture != null;
        }
    }

    private bool CheckSourceExists()
    {
        foreach (var name in SpoutManager.GetSourceNames())
        {
            if (name == _receiver.sourceName)
            {
                return true;
            }
        }
        return false;
    }

}
