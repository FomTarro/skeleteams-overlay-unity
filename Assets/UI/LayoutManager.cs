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
        _webServer.RegisterEndpoint(new Endpoint("/camera/toggle/collab", (req) =>
        {
            ToggleCollabCameraBar(true);
            TogglePopOutCamera(false);
            ToggleMainScreenCamera(false);
            return new EndpointResponse(200, "");
        }));
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ToggleCollabCameraBar(bool toggle)
    {
        
    }

    public void ToggleMainScreenCamera(bool toggle)
    {
        
    }

    public void TogglePopOutCamera(bool toggle)
    {
        
    }

    public void SetMainScreenSpoutSource(string source)
    {
        _mainWindow.sourceName = source;
    }
}
