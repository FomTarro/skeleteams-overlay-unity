using System;
using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using Skeletom.BattleStation.Server;
using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{

    [SerializeField]
    private WebServer _webServer;

    [SerializeField]
    private SpoutReceiver _main;
    [SerializeField]
    private GameObject _collabBar;

    [SerializeField]
    private string _facecamSpoutName;
    [SerializeField]
    private string _captureSpoutName;

    [SerializeField]
    private CameraSwitchAnimation _animation;

    // Start is called before the first frame update
    void Start()
    {
        _webServer.RegisterEndpoint(new Endpoint("/camera/toggle", (req) =>
        {
            if (_main.sourceName.Equals(_facecamSpoutName))
            {
                _animation.StartAnimation(() => { });
                _collabBar.SetActive(true);
                _main.sourceName = _captureSpoutName;
            }
            else
            {
                _animation.StartAnimation(() =>
                {
                    _collabBar.SetActive(false);
                    _main.sourceName = _facecamSpoutName;
                });
            }
            return new EndpointResponse(200, "");
        }));
    }
}
