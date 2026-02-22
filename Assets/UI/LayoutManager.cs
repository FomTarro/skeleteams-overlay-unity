using System;
using System.Collections;
using System.Collections.Generic;
using Klak.Spout;
using Skeletom.BattleStation.Integrations.OBS;
using Skeletom.BattleStation.Integrations.Twitch;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Events;

public class LayoutManager : Singleton<LayoutManager>
{

    [SerializeField]
    private WebServer _webServer;

    [Header("Webcam Views")]
    [SerializeField]
    private GameObject _collabBar;
    [SerializeField]
    private CanvasGroup _popOut;
    [SerializeField]
    private SpoutReceiver _mainWindow;
    [SerializeField]
    private string _facecamSpoutName;
    [SerializeField]
    private string _captureSpoutName;
    [SerializeField]
    private MicVolumeBorder _mainWindowMicBorder;
    [SerializeField]
    private CameraSwitchAnimation _cameraAnimation;

    [Header("Main Scenes")]
    [SerializeField]
    private CanvasGroup _waiting;
    [SerializeField]
    private CanvasGroup _chatting;

    [SerializeField]
    private PauseScreen _pause;

    [Serializable]
    public class ChatUserCountEvent : UnityEvent<int> { }
    public ChatUserCountEvent onChatUserCounted = new();

    [Serializable]
    public class AdTimeEvent : UnityEvent<DateTime> { }
    public AdTimeEvent onNextAdTimeChecked = new();

    [Serializable]
    public class StreamTimeEvent : UnityEvent<DateTime> { }
    public StreamTimeEvent onStreamTimeChecked = new();

    private float _pollingInterval = 5f;

    // Start is called before the first frame update
    void Start()
    {
        _popOut.alpha = 0;
        _webServer.RegisterEndpoint(new Endpoint("/camera/bar/toggle", (req) =>
        {
            if (_collabBar.activeSelf)
            {
                _cameraAnimation.StartAnimation(() =>
                {
                    _collabBar.SetActive(false);
                    _mainWindowMicBorder.enabled = true;
                });
                _mainWindow.sourceName = _facecamSpoutName;
            }
            else if (_popOut.alpha > 0.5f)
            {
                _collabBar.SetActive(true);
                _popOut.alpha = 0f;
            }
            else
            {
                _popOut.alpha = 0f;
                _cameraAnimation.StartAnimation(() =>
                {
                    _collabBar.SetActive(true);
                    _mainWindowMicBorder.enabled = false;
                });
                _mainWindow.sourceName = _captureSpoutName;
            }
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/camera/pop/toggle", (req) =>
        {
            if (_popOut.alpha > 0.5f)
            {
                _popOut.alpha = 0f;
                _cameraAnimation.StartAnimation(() =>
                {
                    _mainWindowMicBorder.enabled = true;
                });
                _mainWindow.sourceName = _facecamSpoutName;
            }
            else if (_collabBar.activeSelf)
            {
                _collabBar.SetActive(false);
                _popOut.alpha = 1f;
            }
            else
            {
                _cameraAnimation.StartAnimation(() =>
                {
                    _collabBar.SetActive(false);
                    _popOut.alpha = 1f;
                    _mainWindowMicBorder.enabled = false;
                });
                _mainWindow.sourceName = _captureSpoutName;
            }
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/camera/face", (req) =>
        {
            _collabBar.SetActive(false);
            _popOut.alpha = 0f;
            _cameraAnimation.StartAnimation(() =>
            {
                _mainWindowMicBorder.enabled = true;
            });
            _mainWindow.sourceName = _facecamSpoutName;
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/scene/waiting", (req) =>
        {
            _waiting.alpha = 1;
            _chatting.alpha = 0;
            return new EndpointResponse(200, "");
        }));
        _webServer.RegisterEndpoint(new Endpoint("/scene/chatting", (req) =>
        {
            _waiting.alpha = 0;
            _chatting.alpha = 1;
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/pause/toggle", (req) =>
        {
            _pause.Toggle();
            return new EndpointResponse(200, "");
        }));
    }

    // Update is called once per frame
    private float _pollingDelta = 0f;
    void Update()
    {
        _pollingDelta += Time.deltaTime;
        if (_pollingDelta > _pollingInterval)
        {
            TwitchIntegration.Instance.GetCurrentChatUsers((list) =>
            {
                onChatUserCounted.Invoke(list.Count);
            }, (err) =>
            {
                Debug.LogError(err.message);
            });
            TwitchIntegration.Instance.GetAdSchedule((schedule) =>
            {
                onNextAdTimeChecked.Invoke(DateTimeOffset.FromUnixTimeMilliseconds(schedule.next_ad_at).DateTime);
            }, (err) =>
            {
                Debug.LogError(err.message);
                onNextAdTimeChecked.Invoke(DateTime.Now);
            });
            OBSIntegration.Instance.GetRecordingStatus((status) =>
            {
                onStreamTimeChecked.Invoke(DateTime.Now.AddMilliseconds(-1 * status.outputDuration));
            },
            (err) =>
            {
                Debug.LogError(err);
            });
            _pollingDelta = 0f;
        }
    }

    public void SetMainScreenSpoutSource(string source)
    {
        _mainWindow.sourceName = source;
    }

    public override void Initialize()
    {

    }
}
