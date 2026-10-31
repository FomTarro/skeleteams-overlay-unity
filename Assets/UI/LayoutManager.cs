using System;
using System.Collections.Generic;
using Klak.Spout;
using Skeletom.BattleStation.Audio;
using Skeletom.BattleStation.Integrations;
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
    private CanvasGroup _ending;
    [SerializeField]
    private PauseScreen _pause;

    [Header("Events")]
    public ChatUserCountEvent onChatUserCounted = new();
    [Serializable]
    public class ChatUserCountEvent : UnityEvent<int> { }

    public AdTimeEvent onNextAdTimeChecked = new();
    [Serializable]
    public class AdTimeEvent : UnityEvent<DateTime> { }

    public StreamTimeEvent onStreamTimeChecked = new();
    [Serializable]
    public class StreamTimeEvent : UnityEvent<DateTime> { }

    private const float POLLING_INTERVAL = 5f;

    private float _sceneRelativeVolume = 1f;
    // TODO: this becomes part of the config
    private readonly string _chattingSong = "cafe";
    private readonly string _waitingSong = "hold";
    private readonly string _mainSong = "skelenova";

    [SerializeField]
    private AudioSource _sceneTransitionJingle;

    [SerializeField]
    private StreamParticipantDisplay _participantPrefab;
    private List<StreamParticipantDisplay> _participantDisplays = new();
    private ParticipantManager.StreamParticipant _currentPresenter;

    [Serializable]
    private enum PresentationMode
    {
        None,
        Face,
        Screen,
        PopOut
    };

    private PresentationMode _currentMode = PresentationMode.None;

    #region Lifecycle

    public override void Initialize()
    {
        ParticipantManager.Instance.onParticipantJoin.AddListener((participant) =>
        {
            StreamParticipantDisplay display = Instantiate(_participantPrefab, _collabBar.transform);
            display.Configure(participant);
            _participantDisplays.Add(display);
            _webServer.RegisterEndpoint(new Endpoint("/camera/presenter/" + participant.metadata.id, (req) =>
            {
                // set their face cam to the main display, hide pop-out, hide from collab bar
                Debug.Log("Setting Presenter to: " + participant.metadata.id);
                SetCurrentPresenter(participant);
                return new EndpointResponse(200, "");
            }));
            if (_currentPresenter == null)
            {
                SetCurrentPresenter(participant);
            }
            RefreshDiplays();
        });

        ParticipantManager.Instance.onParticipantLeave.AddListener((participantId) =>
        {
            _webServer.UnregisterEndpoint("/camera/presenter/" + participantId);
            if (_currentPresenter.metadata.id == participantId)
            {
                // TODO: go back to first presenter
            }
            StreamParticipantDisplay display = _participantDisplays.Find(item => item.ID == participantId);
            if (display != null)
            {
                _participantDisplays.Remove(display);
                Destroy(display.gameObject);
            }
            RefreshDiplays();
        });

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

        _webServer.RegisterEndpoint(new Endpoint("/scene/waiting", (req) =>
        {
            _waiting.alpha = 1;
            _chatting.alpha = 0;
            _ending.alpha = 0;
            MusicManager.Instance.RelativeSceneVolume = 0.80f;
            MusicManager.Instance.PlayTrack(_waitingSong, MusicManager.Instance.RelativeSceneVolume);
            return new EndpointResponse(200, "");
        }));
        _webServer.RegisterEndpoint(new Endpoint("/scene/chatting", (req) =>
        {
            _waiting.alpha = 0;
            _chatting.alpha = 1;
            _ending.alpha = 0;
            MusicManager.Instance.RelativeSceneVolume = 0.35f;
            _sceneTransitionJingle.Play();
            MusicManager.Instance.PlayTrack(_chattingSong, MusicManager.Instance.RelativeSceneVolume);
            return new EndpointResponse(200, "");
        }));
        _webServer.RegisterEndpoint(new Endpoint("/scene/ending", (req) =>
        {
            _waiting.alpha = 0;
            _chatting.alpha = 0;
            _ending.alpha = 1;
            MusicManager.Instance.RelativeSceneVolume = 0.80f;
            MusicManager.Instance.PlayTrack(_waitingSong, MusicManager.Instance.RelativeSceneVolume);
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/pause/toggle", (req) =>
        {
            bool state = _pause.Toggle();
            if (state)
            {
                MusicManager.Instance.PlayTrack(_waitingSong, 1f);
            }
            else
            {
                MusicManager.Instance.PlayTrack(_chattingSong, MusicManager.Instance.RelativeSceneVolume);
            }
            return new EndpointResponse(200, "");
        }));

        _popOut.alpha = 0;

        _webServer.RegisterEndpoint(new Endpoint("/test/raid", (req) =>
        {
            IntegrationManager.Instance.Twitch.onChannelRaid.Invoke(
                new StreamRaid(
                    new StreamUser("Skeletom", "12345"),
                    500
                ));
            return new EndpointResponse(200, "");
        }));

        _webServer.RegisterEndpoint(new Endpoint("/test/sub", (req) =>
        {
            IntegrationManager.Instance.Twitch.onChannelPaidSubscription.Invoke(
                new StreamChannelPaidSubscription(
                    new StreamUser("Skeletom", "12345")
                ));
            return new EndpointResponse(200, "");
        }));
    }

    // Update is called once per frame
    private float _pollingDelta = 0f;
    void Update()
    {
        _pollingDelta += Time.deltaTime;
        if (_pollingDelta > POLLING_INTERVAL)
        {
            IntegrationManager.Instance.Twitch.GetCurrentChatUsers((list) =>
            {
                onChatUserCounted.Invoke(list.Count);
            }, (err) =>
            {
                Debug.LogError(err.message);
            });
            IntegrationManager.Instance.Twitch.GetAdSchedule((schedule) =>
            {
                onNextAdTimeChecked.Invoke(DateTimeOffset.FromUnixTimeMilliseconds(schedule.next_ad_at * 1000).DateTime);
            }, (err) =>
            {
                Debug.LogError(err.message);
                onNextAdTimeChecked.Invoke(DateTime.Now);
            });
            IntegrationManager.Instance.OBS.GetRecordingStatus((status) =>
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

    #endregion

    private void SetCurrentPresenter(ParticipantManager.StreamParticipant participant)
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
                RefreshDiplays();
                _mainWindowMicBorder.enabled = true;
            });
            _popOut.alpha = 0;
            _mainWindow.sourceName = _currentPresenter.metadata.faceSource;
        }
        else if (mode == PresentationMode.Screen)
        {
            _mainWindowMicBorder.enabled = false;
            _cameraAnimation.StartAnimation(() =>
            {
                RefreshDiplays();
            });
            _popOut.alpha = 0;
            // TODO: there's a slight flicker when going from screen to face.
            _mainWindow.sourceName = _currentPresenter.metadata.screenSource;
        }
        else if (mode == PresentationMode.PopOut)
        {
            _mainWindowMicBorder.enabled = false;
            _cameraAnimation.StartAnimation(() =>
            {
                RefreshDiplays();
                _popOut.alpha = 1;
            });
            _mainWindow.sourceName = _currentPresenter.metadata.screenSource;
        }
    }

    private void RefreshDiplays()
    {
        if (_currentMode == PresentationMode.Face || _currentMode == PresentationMode.PopOut)
        {
            foreach (StreamParticipantDisplay display in _participantDisplays)
            {
                display.gameObject.SetActive(!_currentPresenter.metadata.id.Equals(display.ID));
            }
            if (_participantDisplays.Count <= 1)
            {
                _collabBar.SetActive(false);
            }
            else
            {
                _collabBar.SetActive(true);
            }
        }
        else if (_currentMode == PresentationMode.Screen)
        {
            foreach (StreamParticipantDisplay display in _participantDisplays)
            {
                display.gameObject.SetActive(true);
            }
            _collabBar.SetActive(true);
        }
    }
}
