using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    public class TwitchIntegration : Integration<TwitchIntegration, TwitchIntegration.IntegrationData>
    {
        public override string FileName => "twitch.json";

        private const string CLIENT_ID = "x2rikvl9behn8k54flc95ulhbq265m";
        private const string USER_TOKEN_ENDPOINT = "https://id.twitch.tv/oauth2/authorize";
        private const string USER_TOKEN_REDIRECT = "http://localhost:61616/twitch/oauth2";
        private string USER_TOKEN = "NO_TOKEN_SET";
        private string BROADCASTER_ID = "NO_ID_SET";

        private const string USERS_ENDPOINT = "https://api.twitch.tv/helix/users";
        private const string EVENTSUB_SUBSCRIPTION_ENDPOINT = "https://api.twitch.tv/helix/eventsub/subscriptions";
        private const string EMOTES_ENDPOINT = "https://api.twitch.tv/helix/chat/emotes/set";
        private const string VALIDATE_ENDPOINT = "https://id.twitch.tv/oauth2/validate";

        private static readonly string[] USER_TOKEN_SCOPES = {
            "chat:read",
            "user:read:chat",
            "channel:read:redemptions",
            "channel:read:subscriptions",
            "moderator:read:followers",
        };

        [Serializable]
        public struct EmoteFrames
        {
            public string name;
            public List<GifToTextureDecoder.Frame> frames;
            public EmoteFrames(string name, List<GifToTextureDecoder.Frame> frames)
            {
                this.name = name;
                this.frames = frames;
            }
        }

        [SerializeField]
        private List<EmoteFrames> _emoteCache = new List<EmoteFrames>();

        [Header("Networking")]
        [SerializeField]
        private WebServer _webServer;
        [SerializeField]
        private TextAsset _tokenRedirectPage;
        private WebSocket _socket = new WebSocket();

        private Dictionary<string, Action<string>> _subscriptions = new Dictionary<string, Action<string>>();

        private class TokenData : BaseSaveData
        {
            public string token;
        }

        public override void Initialize()
        {
            // Set up token ingest endpoints 
            _webServer.RegisterEndpoint(new Endpoint("/twitch/oauth2", (req) =>
            {
                return new EndpointResponse(200, _tokenRedirectPage.text);
            }));
            _webServer.RegisterEndpoint(new Endpoint("/twitch/token", (req) =>
            {
                TokenData data = JsonUtility.FromJson<TokenData>(req.body);
                SetToken(data.token);
                return new EndpointResponse(200, "OK");
            }));
            FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        }

        public void RequestToken()
        {
            string userToken = USER_TOKEN_ENDPOINT
            + "?client_id=" + CLIENT_ID
            + "&redirect_uri=" + USER_TOKEN_REDIRECT
            + "&response_type=token"
            + "&state=" + "http://localhost:" + _webServer.Port + "/twitch/token"
            + "&scope=" + string.Join(" ", USER_TOKEN_SCOPES);

            Application.OpenURL(userToken);
        }

        public void SetToken(string token)
        {
            USER_TOKEN = token;
            // TODO: figure out way to ignore write if the caller is a read function
            SaveDataManager.Instance.WriteSaveData(this);
            GetSelfUserInfo((user) =>
            {
                BROADCASTER_ID = user.id;
                // TODO: this probably sets off all the registration and subscription events, lol
                _socket.Start("wss://eventsub.wss.twitch.tv/ws",
                () =>
                {
                    Debug.Log("Twitch EventSub Socket connected!");
                },
                () =>
                {
                    Debug.Log("Twitch EventSub Socket disconnected.");
                },
                (err) =>
                {
                    Debug.LogError($"Twitch EventSub Socket error: {err}");
                }
            );
            }, (err) =>
            {
                Debug.LogError(err);
            });
        }

        private void ValidateToken(string token, Action<bool> onValidation)
        {
            HttpUtils.HttpHeaders headers = new HttpUtils.HttpHeaders()
            {
                authorization = token
            };
            StartCoroutine(
                HttpUtils.GetRequest(VALIDATE_ENDPOINT, headers,
                    (str) =>
                    {
                        TokenValidationResponse response = JsonUtility.FromJson<TokenValidationResponse>(str);
                        bool scopesEqual = CollectionUtils.CheckEqualElements(USER_TOKEN_SCOPES, response.scopes);
                        onValidation(scopesEqual);
                    },
                    (err) =>
                    {
                        onValidation(false);
                    }
                )
            );
        }
        public void GetUserInfo(List<string> users, Action<List<UserData>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            HttpUtils.HttpHeaders headers = new HttpUtils.HttpHeaders()
            {
                authorization = USER_TOKEN,
                customHeaders = new Dictionary<string, string>()
                {
                    {"Client-Id", CLIENT_ID}
                }
            };
            StartCoroutine(
                HttpUtils.GetRequest(USERS_ENDPOINT + "?" + string.Join('&', users.Select(user => "login=" + user)), headers,
                    (str) =>
                    {
                        onSuccess(JsonUtility.FromJson<DataResponse<UserData>>(str).data);
                    },
                    (err) =>
                    {
                        onError(err);
                    }
                )
            );
        }

        public void GetSelfUserInfo(Action<UserData> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            GetUserInfo(new List<string>(), (list) =>
            {
                onSuccess(list[0]);
            }, onError);
        }

        public void GetEmotesBySetId(string setId, Action<List<Texture2D>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            HttpUtils.HttpHeaders headers = new HttpUtils.HttpHeaders()
            {
                authorization = USER_TOKEN,
                customHeaders = new Dictionary<string, string>()
                {
                    {"Client-Id", CLIENT_ID}
                }
            };
            StartCoroutine(
                HttpUtils.GetRequest(EMOTES_ENDPOINT + $"?emote_set_id={setId}", headers,
                    (str) =>
                    {
                        EmoteDataResponse response = JsonUtility.FromJson<EmoteDataResponse>(str);
                        foreach (EmoteData data in response.data)
                        {
                            string format = data.format[^1];
                            string url = response.template
                            .Replace("{{id}}", data.id)
                            .Replace("{{format}}", format)
                            .Replace("{{theme_mode}}", "light")
                            .Replace("{{scale}}", data.scale[^1]);
                            StartCoroutine(
                                HttpUtils.GetBytesRequest(url, headers,
                                    (bytes) =>
                                    {
                                        if ("animated".Equals(format))
                                        {
                                            var frames = GifToTextureDecoder.Decode(bytes);
                                            _emoteCache.Add(new EmoteFrames(data.name, frames));
                                        }
                                        else
                                        {
                                            var tex = new Texture2D(2, 2);
                                            if (tex.LoadImage(bytes))
                                            {
                                                _emoteCache.Add(new EmoteFrames(data.name, new List<GifToTextureDecoder.Frame>()
                                                {
                                                    new GifToTextureDecoder.Frame(tex, -1)
                                                }));
                                            }
                                        }
                                    },
                                    onError
                                )
                            );
                        }
                        onSuccess(null);
                    },
                    onError
                )
            );
        }


        [Serializable]
        public class TwitchChatMessageEvent : UnityEvent<EventSub.ChatMessageEvent> { }
        public TwitchChatMessageEvent onTwitchChatMessage = new TwitchChatMessageEvent();

        private void Update()
        {
            _socket.Tick(Time.deltaTime);
            string data = null;
            do
            {
                if (this._socket != null)
                {
                    data = this._socket.GetNextResponse();
                    if (data != null)
                    {
                        ProcessMessage(data);
                    }
                }
            } while (data != null);
        }

        private void ProcessMessage(string msg)
        {
            try
            {
                EventSub.EventMessage<EventSub.EventPayload<string>> message = JsonUtility.FromJson<EventSub.EventMessage<EventSub.EventPayload<string>>>(msg);
                if ("session_welcome".Equals(message.metadata.message_type))
                {
                    EventSub.EventMessage<EventSub.WelcomePayload> session = JsonUtility.FromJson<EventSub.EventMessage<EventSub.WelcomePayload>>(msg);
                    string sessionId = session.payload.session.id;
                    _subscriptions.Clear();
                    // TODO: kick off all HTTP subscriptions
                    SubscribeToEvent<EventSub.ChatMessageEvent>(
                        new EventSub.ChatMessageSubscriptionRequest(sessionId)
                        {
                            condition = new EventSub.ChatMessageEventCondition()
                            {
                                broadcaster_user_id = BROADCASTER_ID,
                                user_id = BROADCASTER_ID
                            }
                        },
                        (message) =>
                        {
                            onTwitchChatMessage.Invoke(message);
                            ISet<string> uniqueSetIds = new HashSet<string>();
                            foreach (EventSub.ChatMessageFragment fragment in message.message.fragments)
                            {
                                if (fragment.emote != null)
                                {
                                    uniqueSetIds.Add(fragment.emote.emote_set_id);
                                }
                            }
                            foreach (string setId in uniqueSetIds)
                            {
                                GetEmotesBySetId(setId, (tex) => { }, (err) => { Debug.LogError(err); });
                            }
                        }
                    );

                }
                else if ("notification".Equals(message.metadata.message_type))
                {
                    Debug.Log(msg);
                    if (_subscriptions.ContainsKey(message.payload.subscription.id))
                    {
                        _subscriptions[message.payload.subscription.id](msg);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private void SubscribeToEvent<T>(EventSub.IEventSubscriptionRequest payload, Action<T> onEvent) where T : EventSub.IEventSubEvent
        {
            HttpUtils.HttpHeaders headers = new HttpUtils.HttpHeaders()
            {
                authorization = USER_TOKEN,
                customHeaders = {
                    {"Client-Id", CLIENT_ID}
                }
            };
            string eventType = payload.GetSubscriptionType();
            StartCoroutine(
                HttpUtils.PostRequest(
                    EVENTSUB_SUBSCRIPTION_ENDPOINT,
                    JsonUtility.ToJson(payload),
                    headers,
                    (success) =>
                    {
                        EventSub.SubscriptionResponse response = JsonUtility.FromJson<EventSub.SubscriptionResponse>(success);
                        EventSub.SubscriptionData data = response.data[0];
                        _subscriptions[data.id] = (msg) =>
                        {
                            EventSub.EventMessage<EventSub.EventPayload<T>> obj = JsonUtility.FromJson<EventSub.EventMessage<EventSub.EventPayload<T>>>(msg);
                            onEvent(obj.payload.@event);
                        };
                        Debug.Log($"Subscribed to {eventType} - {data.id}");
                    },
                    (err) =>
                    {
                        Debug.LogError($"Error subscribing to {eventType}: {err}");
                    }
                )
            );
        }

        public override void FromSaveData(IntegrationData data)
        {
            ValidateToken(data.token, (isValid) =>
            {
                if (!isValid)
                {
                    RequestToken();
                }
                else
                {
                    SetToken(data.token);
                }
            });
        }

        public override IntegrationData ToSaveData()
        {
            IntegrationData data = new IntegrationData()
            {
                token = USER_TOKEN
            };
            return data;
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
        }
    }
}
