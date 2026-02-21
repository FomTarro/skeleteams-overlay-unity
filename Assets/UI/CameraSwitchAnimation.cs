using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using Skeletom.Essentials.Animations;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.UI;

public class CameraSwitchAnimation : BaseAnimatedElement
{
    [SerializeField]
    private RawImage _mainSpoutDisplay;
    [SerializeField]
    private SpoutReceiver _receiver;
    [SerializeField]
    private string _facecamSpoutName;
    [SerializeField]
    private string _captureSpoutName;
    protected override IEnumerator Animate()
    {
        _mainSpoutDisplay.color = Color.clear;
        yield return EnumUtils.GenericWaitForSeconds(0.75f, (frame) => { });
        // if (_receiver.sourceName.Equals(_facecamSpoutName))
        // {
        //     _receiver.sourceName = _captureSpoutName;
        // }
        // else
        // {
        //     _receiver.sourceName = _facecamSpoutName;
        // }
        _mainSpoutDisplay.color = Color.white;
    }

    protected override void InitializeImplementation()
    {

    }

    protected override void ResetImplementation()
    {

    }
}
