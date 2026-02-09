using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using Skeletom.BattleStation.Server;
using UnityEngine;

public class LayoutManager : MonoBehaviour
{

    [SerializeField]
    private WebServer _webServer;

    [SerializeField]
    private GameObject _collabBar;
    [SerializeField]
    private SpoutReceiver _mainWindow;

    // Start is called before the first frame update
    void Start()
    {
        // _webServer.RegisterEndpoint(new Endpoint("/camera/toggle", (req) =>
        // {
        //     if (_mainWindow.sourceName.Equals(_facecamSpoutName))
        //     {
        //         _collabBar.SetActive(true);
        //         _mainWindow.sourceName = _captureSpoutName;
        //     }
        //     else
        //     {
        //         _collabBar.SetActive(false);
        //         _mainWindow.sourceName = _facecamSpoutName;
        //     }
        //     return new EndpointResponse(200, "");
        // }));
    }

    // Update is called once per frame
    void Update()
    {

    }
}
