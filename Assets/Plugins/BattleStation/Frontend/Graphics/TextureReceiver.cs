using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using UnityEngine;
using UnityEngine.UI;

public class TextureReceiver : MonoBehaviour
{
    [SerializeField]
    private RawImage _target;

    private SpoutReceiver _receiver;

    private void OnEnable()
    {
        _receiver = GetComponent<SpoutReceiver>();
    }

    private void Update()
    {
        if (_target != null && _receiver != null)
        {
            _target.texture = _receiver.receivedTexture;
        }
    }
    
}
