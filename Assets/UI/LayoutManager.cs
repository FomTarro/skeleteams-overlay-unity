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
    private StreamParticipantDisplay _popOutDisplay;
    [SerializeField]
    private SpoutReceiver _mainWindow;
    [SerializeField]
    private MicVolumeBorder _mainWindowMicBorder;
    [SerializeField]
    private CameraSwitchAnimation _cameraAnimation;

    [SerializeField]
    private StreamParticipantDisplay _mainDisplay;

    [Header("Main Scenes")]
    [SerializeField]
    private CanvasGroup _waiting;
    [SerializeField]
    private CanvasGroup _chatting;

    [SerializeField]
    private PauseScreen _pause;

    [Serializable]
    public class ChatUserCountEvent : UnityEvent<int> { }
    [Header("Events")]
    public ChatUserCountEvent onChatUserCounted = new();

    [Serializable]
    public class AdTimeEvent : UnityEvent<DateTime> { }
    public AdTimeEvent onNextAdTimeChecked = new();

    [Serializable]
    public class StreamTimeEvent : UnityEvent<DateTime> { }
    public StreamTimeEvent onStreamTimeChecked = new();

    private readonly float _pollingInterval = 5f;

    private float _sceneRelativeVolume = 1f;
    private readonly string _chattingSong = "Cafe";
    private readonly string _waitingSong = "Main";

    [Serializable]
    public class StreamParticipant
    {
        public bool enabled;
        public string key;
        public string name;
        public Color borderColor;
        public string faceSourceName;
        public string screenSourceName;
        public string micSourceName;
        public Sprite icon;
    }
    [SerializeField]
    private StreamParticipantDisplay _participantPrefab;
    private List<StreamParticipantDisplay> _participantDisplays = new List<StreamParticipantDisplay>();
    [SerializeField]
    private List<StreamParticipant> _participants;
    private StreamParticipant _currentPresenter;

    [Serializable]
    private enum PresentationMode
    {
        None,
        Face,
        Screen,
        PopOut
    };

    private PresentationMode _currentMode = PresentationMode.None;

    // Start is called before the first frame update
    void Start()
    {
        foreach (StreamParticipant participant in _participants)
        {
            if (participant.enabled)
            {
                StreamParticipantDisplay display = Instantiate(_participantPrefab, _collabBar.transform);
                display.Configure(participant);
                _participantDisplays.Add(display);
                _webServer.RegisterEndpoint(new Endpoint("/camera/presenter/" + participant.key, (req) =>
                {
                    // set their face cam to the main display, hide pop-out, hide from collab bar
                    SetCurrentPresenter(participant);
                    return new EndpointResponse(200, "");
                }));
            }
        }
        _webServer.RegisterEndpoint(new Endpoint("/camera/face", (req) =>
        {
            SetMode(PresentationMode.Face);
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/camera/screen", (req) =>
        {
            SetMode(PresentationMode.Screen);
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/camera/pop", (req) =>
        {
            SetMode(PresentationMode.PopOut);
            return new EndpointResponse(200, "");
        }));
        // _webServer.RegisterEndpoint(new Endpoint("/camera/bar/toggle", (req) =>
        // {
        //     if (_collabBar.activeSelf)
        //     {
        //         _cameraAnimation.StartAnimation(() =>
        //         {
        //             _collabBar.SetActive(false);
        //             _mainWindowMicBorder.enabled = true;
        //         });
        //         _mainWindow.sourceName = _facecamSpoutName;
        //     }
        //     else if (_popOut.alpha > 0.5f)
        //     {
        //         _collabBar.SetActive(true);
        //         _popOut.alpha = 0f;
        //     }
        //     else
        //     {
        //         _popOut.alpha = 0f;
        //         _cameraAnimation.StartAnimation(() =>
        //         {
        //             _collabBar.SetActive(true);
        //             _mainWindowMicBorder.enabled = false;
        //         });
        //         _mainWindow.sourceName = _captureSpoutName;
        //     }
        //     return new EndpointResponse(200, "");
        // }));

        // _webServer.RegisterEndpoint(new Endpoint("/camera/pop/toggle", (req) =>
        // {
        //     if (_popOut.alpha > 0.5f)
        //     {
        //         _popOut.alpha = 0f;
        //         _cameraAnimation.StartAnimation(() =>
        //         {
        //             _mainWindowMicBorder.enabled = true;
        //         });
        //         _mainWindow.sourceName = _facecamSpoutName;
        //     }
        //     else if (_collabBar.activeSelf)
        //     {
        //         _collabBar.SetActive(false);
        //         _popOut.alpha = 1f;
        //     }
        //     else
        //     {
        //         _cameraAnimation.StartAnimation(() =>
        //         {
        //             _collabBar.SetActive(false);
        //             _popOut.alpha = 1f;
        //             _mainWindowMicBorder.enabled = false;
        //         });
        //         _mainWindow.sourceName = _captureSpoutName;
        //     }
        //     return new EndpointResponse(200, "");
        // }));

        _webServer.RegisterEndpoint(new Endpoint("/scene/waiting", (req) =>
        {
            _waiting.alpha = 1;
            _chatting.alpha = 0;
            // TODO: track these target volumes independently
            _sceneRelativeVolume = 1f;
            Jukebox.Instance.ChangeSong(_waitingSong, _sceneRelativeVolume);
            return new EndpointResponse(200, "");
        }));
        _webServer.RegisterEndpoint(new Endpoint("/scene/chatting", (req) =>
        {
            _waiting.alpha = 0;
            _chatting.alpha = 1;
            _sceneRelativeVolume = 0.25f;
            Jukebox.Instance.ChangeSong(_chattingSong, _sceneRelativeVolume);
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/pause/toggle", (req) =>
        {
            var state = _pause.Toggle();
            if (state)
            {
                Jukebox.Instance.ChangeSong(_waitingSong, 1f);
            }
            else
            {
                Jukebox.Instance.ChangeSong(_chattingSong, _sceneRelativeVolume);
            }
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/music/volume", (req) =>
        {
            var queryParam = new List<QueryParameter>(req.queryParameters).Find(param => param.key.Equals("increment"));
            if (queryParam != null)
            {
                float.TryParse(queryParam.value, out float increment);
                Jukebox.Instance.SetVolume(Jukebox.VolumeGroup.MUSIC_MASTER, Jukebox.Instance.GetVolume(Jukebox.VolumeGroup.MUSIC_MASTER) + increment);
            }
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/music/song", (req) =>
        {
            var queryParam = new List<QueryParameter>(req.queryParameters).Find(param => param.key.Equals("title"));
            if (queryParam != null)
            {
                Jukebox.Instance.ChangeSong(queryParam.value, _sceneRelativeVolume);
            }
            return new EndpointResponse(200, "");
        }));

        _popOut.alpha = 0;
        SetCurrentPresenter(_participants[0]);
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
                onNextAdTimeChecked.Invoke(DateTimeOffset.FromUnixTimeMilliseconds(schedule.next_ad_at * 1000).DateTime);
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

    private void SetCurrentPresenter(StreamParticipant participant)
    {
        _currentPresenter = participant;
        _mainDisplay.Configure(participant);
        _popOutDisplay.Configure(participant);
        SetMode(PresentationMode.Face);
    }

    private void SetMode(PresentationMode mode)
    {
        _currentMode = mode;
        if (mode == PresentationMode.Face)
        {
            _mainWindowMicBorder.enabled = false;
            _cameraAnimation.StartAnimation(() =>
            {
                foreach (StreamParticipantDisplay display in _participantDisplays)
                {
                    display.gameObject.SetActive(!_currentPresenter.key.Equals(display.Key));
                }
                if (_participantDisplays.Count <= 1)
                {
                    _collabBar.SetActive(false);
                }
                _mainWindowMicBorder.enabled = true;
            });
            _popOut.alpha = 0;
            _mainWindow.sourceName = _currentPresenter.faceSourceName;
        }
        else if (mode == PresentationMode.Screen)
        {
            _mainWindowMicBorder.enabled = false;
            _cameraAnimation.StartAnimation(() =>
            {
                foreach (StreamParticipantDisplay display in _participantDisplays)
                {
                    display.gameObject.SetActive(true);
                }
                _collabBar.SetActive(true);
            });
            _popOut.alpha = 0;
            _mainWindow.sourceName = _currentPresenter.screenSourceName;
        }
        else if (mode == PresentationMode.PopOut)
        {
            _mainWindowMicBorder.enabled = false;
            _cameraAnimation.StartAnimation(() =>
            {
                foreach (StreamParticipantDisplay display in _participantDisplays)
                {
                    display.gameObject.SetActive(!_currentPresenter.key.Equals(display.Key));
                }
                if (_participantDisplays.Count <= 1)
                {
                    _collabBar.SetActive(false);
                }
                _popOut.alpha = 1;
            });
            _mainWindow.sourceName = _currentPresenter.screenSourceName;
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
