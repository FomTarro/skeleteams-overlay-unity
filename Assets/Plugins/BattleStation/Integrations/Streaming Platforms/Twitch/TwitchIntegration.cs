using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    public class TwitchIntegration : StreamIntegration<TwitchIntegration, TwitchIntegration.IntegrationData>
    {
        public override string FileName => "twitch.json";

        private const string CLIENT_ID = "x2rikvl9behn8k54flc95ulhbq265m";
        private const string USER_TOKEN_ENDPOINT = "https://id.twitch.tv/oauth2/authorize";
        private const string USER_TOKEN_REDIRECT = "http://localhost:61616/twitch/oauth2";
        private string USER_TOKEN = "NO_TOKEN_SET";
        private string BROADCASTER_ID = "NO_ID_SET";

        private const string USERS_ENDPOINT = "https://api.twitch.tv/helix/users";
        private const string EVENTSUB_SUBSCRIPTION_ENDPOINT = "https://api.twitch.tv/helix/eventsub/subscriptions";

        private const string EMOTES_SET_ENDPOINT = "https://api.twitch.tv/helix/chat/emotes/set";
        private string EMOTES_INDIVIDUAL_ENDPOINT = "";
        private const string EMOTES_GLOBAL_ENDPOINT = "https://api.twitch.tv/helix/chat/emotes/global";
        private const string EMOTES_CHANNEL_ENDPOINT = "https://api.twitch.tv/helix/chat/emotes";

        private const string BADGES_GLOBAL_ENDPOINT = "https://api.twitch.tv/helix/chat/badges/global";
        public const string BADGES_CHANNEL_ENDPOINT = "https://api.twitch.tv/helix/chat/badges";

        private const string VALIDATE_ENDPOINT = "https://id.twitch.tv/oauth2/validate";

        private static readonly string[] USER_TOKEN_SCOPES = {
            "chat:read",
            "user:read:chat",
            "channel:read:redemptions",
            "channel:read:subscriptions",
            "moderator:read:followers",
        };

        [Header("Networking")]
        [SerializeField]
        private WebServer _webServer;
        [SerializeField]
        private TextAsset _tokenRedirectPage;
        private class TokenRedirectData
        {
            public string token;
        }

        private readonly WebSocket _socket = new WebSocket();

        private readonly Dictionary<string, Action<string>> _subscriptions = new Dictionary<string, Action<string>>();

        private HttpUtils.HttpHeaders Headers
        {
            get
            {
                return new HttpUtils.HttpHeaders()
                {
                    authorization = USER_TOKEN,
                    customHeaders = {
                        {"Client-Id", CLIENT_ID}
                    }
                };
            }
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
                TokenRedirectData data = JsonUtility.FromJson<TokenRedirectData>(req.body);
                SetToken(data.token);
                return new EndpointResponse(200, "OK");
            }));
            FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        }

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
                        ProcessEventSubEvent(data);
                    }
                }
            } while (data != null);
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

        public void GetUserInfo(ICollection<string> users, Action<List<UserData>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            string query = users.Count > 0 ? string.Join('&', users.Select(user => "login=" + user)) : "";
            string url = $"{USERS_ENDPOINT}{query}";
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
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
            GetUserInfo(new string[0], (list) =>
            {
                onSuccess(list[0]);
            }, onError);
        }

        private void ProcessEventSubEvent(string msg)
        {
            try
            {
                EventSub.EventMessage<EventSub.EventPayload<string>> message = JsonUtility.FromJson<EventSub.EventMessage<EventSub.EventPayload<string>>>(msg);
                if ("session_welcome".Equals(message.metadata.message_type))
                {
                    EventSub.EventMessage<EventSub.WelcomePayload> session = JsonUtility.FromJson<EventSub.EventMessage<EventSub.WelcomePayload>>(msg);
                    string sessionId = session.payload.session.id;
                    _subscriptions.Clear();
                    // Kick off all HTTP subscriptions
                    SubscribeToEvent<EventSub.ChatMessageEvent>(
                        new EventSub.ChatMessageSubscriptionRequest(sessionId)
                        {
                            condition = new EventSub.ChatMessageEventCondition()
                            {
                                broadcaster_user_id = BROADCASTER_ID,
                                user_id = BROADCASTER_ID
                            }
                        },
                        (success) =>
                        {
                            GetChannelEmotes(BROADCASTER_ID, (emotes) => { }, (err) => { Debug.LogError(err); });
                            GetGlobalEmotes((emotes) => { }, (err) => { Debug.LogError(err); });
                            GetChannelBadges(BROADCASTER_ID, (badges) => { }, (err) => { Debug.LogError(err); });
                            GetGlobalBadges((badges) => { }, (err) => { Debug.LogError(err); });
                        },
                        (err) =>
                        {

                        },
                        (message) =>
                        {
                            PrepareChatMessage(message, onChatMessage.Invoke);
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

        public void GetGlobalEmotes(Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            Debug.Log("Fetching global emotes.");
            string url = EMOTES_GLOBAL_ENDPOINT;
            GetBatchEmotes(url, onSuccess, onError);
        }

        public void GetChannelEmotes(string broadcasterId, Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            Debug.Log($"Fetching channel emotes for Broadcaster ID: {broadcasterId}");
            string url = $"{EMOTES_CHANNEL_ENDPOINT}?broadcaster_id={broadcasterId}";
            GetBatchEmotes(url, onSuccess, onError);
        }

        public void GetSetEmotes(string setId, Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            Debug.Log($"Fetching set emotes for set ID: {setId}");
            string url = $"{EMOTES_SET_ENDPOINT}?emote_set_id={setId}";
            GetBatchEmotes(url, onSuccess, onError);
        }

        private void GetBatchEmotes(string url, Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        EmoteDataResponse response = JsonUtility.FromJson<EmoteDataResponse>(str);
                        List<StreamingPlatformImage> emotes = new List<StreamingPlatformImage>();
                        DependencyManager manager = new(
                            () =>
                            {
                                Debug.Log($"{emotes.Count} total emotes resolved!");
                                onSuccess(emotes);
                            },
                            (key, pending) =>
                            {
                                Debug.Log($"Waiting on {pending} more emotes to resolve...");
                            }
                        );
                        EMOTES_INDIVIDUAL_ENDPOINT = response.template;
                        foreach (EmoteData data in response.data)
                        {
                            string taskId = Guid.NewGuid().ToString();
                            manager.AddDependency(taskId);
                            GetIndividualEmote(data,
                            (success) =>
                            {
                                emotes.Add(success);
                                manager.ResolveDependency(taskId);
                            },
                            (err) =>
                            {
                                Debug.LogError(err);
                                manager.ResolveDependency(taskId);
                            });
                        }
                        manager.Enable(true);
                    },
                    onError
                )
            );
        }

        public void GetIndividualEmote(EmoteData data, Action<StreamingPlatformImage> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            string key = data.name;
            string format = data.format[^1];
            string scale = data.scale[^1];
            string url = EMOTES_INDIVIDUAL_ENDPOINT
            .Replace("{{id}}", data.id)
            .Replace("{{format}}", format)
            .Replace("{{theme_mode}}", "light")
            .Replace("{{scale}}", scale);
            ImageHandler.GetImageFromRemote(url, Headers, key, onSuccess, onError);
        }

        public void GetGlobalBadges(Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            Debug.Log("Fetching global badges.");
            GetBatchBadges(BADGES_GLOBAL_ENDPOINT, onSuccess, onError);
        }

        public void GetChannelBadges(string broadcasterId, Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            Debug.Log($"Fetching channel badges for Broadcaster ID: {broadcasterId}");
            string url = $"{BADGES_CHANNEL_ENDPOINT}?broadcaster_id={broadcasterId}";
            GetBatchBadges(url, onSuccess, onError);
        }

        private void GetBatchBadges(string url, Action<List<StreamingPlatformImage>> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            StartCoroutine(
               HttpUtils.GetRequest(url, Headers,
               (str) =>
               {
                   BadgeSetDataResponse response = JsonUtility.FromJson<BadgeSetDataResponse>(str);
                   List<StreamingPlatformImage> badges = new List<StreamingPlatformImage>();
                   DependencyManager manager = new(
                       () =>
                       {
                           Debug.Log($"{badges.Count} total badges resolved!");
                           onSuccess(badges);
                       },
                       (key, pending) =>
                       {
                           Debug.Log($"Waiting on {pending} more badges to resolve...");
                       }
                   );
                   foreach (BadgeSetData data in response.data)
                   {
                       foreach (BadgeVersionData version in data.versions)
                       {
                           string taskId = Guid.NewGuid().ToString();
                           manager.AddDependency(taskId);
                           ImageHandler.GetImageFromRemote(version.image_url_1x, Headers, $"badge_{data.set_id}_{version.id}",
                           (success) =>
                           {
                               badges.Add(success);
                               manager.ResolveDependency(taskId);
                           },
                           (err) =>
                           {
                               Debug.LogError(err);
                               manager.ResolveDependency(taskId);
                           });
                       }
                   }
                   manager.Enable(true);
               },
               onError)
            );
        }

        private void PrepareChatMessage(EventSub.ChatMessageEvent chatEvent, Action<StreamingPlatformChatMessage> onMessageReady)
        {
            StreamingPlatformChatUser chatter = new StreamingPlatformChatUser(chatEvent.chatter_user_name, chatEvent.color, chatEvent.chatter_user_id);
            List<StreamingPlatformChatMessage.Fragment> fragments = new List<StreamingPlatformChatMessage.Fragment>();
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () => { onMessageReady(new StreamingPlatformChatMessage(chatter, fragments)); }
            );
            foreach (EventSub.ChatMessageFragment fragment in chatEvent.message.fragments)
            {
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                if ("emote".Equals(fragment.type))
                {
                    var newFragment = new StreamingPlatformChatMessage.Fragment(StreamingPlatformChatMessage.Fragment.Type.EMOTE, fragment.text);
                    fragments.Add(newFragment);
                    GetIndividualEmote(new EmoteData(fragment.text, fragment.emote),
                    (success) =>
                    {
                        newFragment.image = success;
                        manager.ResolveDependency(taskId);
                    },
                    (err) =>
                    {
                        manager.ResolveDependency(taskId);
                    });
                }
                else
                {
                    var newFragment = new StreamingPlatformChatMessage.Fragment(StreamingPlatformChatMessage.Fragment.Type.TEXT, fragment.text);
                    fragments.Add(newFragment);
                    manager.ResolveDependency(taskId);
                }
            }
            foreach (EventSub.ChatMessageBadge badge in chatEvent.badges)
            {
                StreamingPlatformBadge newBadge = new StreamingPlatformBadge(badge.info, badge.id);
                chatter.badges.Add(newBadge);
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                ImageHandler.GetImageFromCache($"badge_{badge.set_id}_{badge.id}",
                    (success) =>
                    {
                        newBadge.image = success;
                        manager.ResolveDependency(taskId);
                    },
                    (err) =>
                    {
                        manager.ResolveDependency(taskId);
                    }
                );
            }
            manager.Enable(true);
        }

        private void SubscribeToEvent<T>(EventSub.IEventSubscriptionRequest payload, Action<string> onSuccess, Action<HttpUtils.HttpError> onError, Action<T> onEvent) where T : EventSub.IEventSubEvent
        {
            string eventType = payload.GetSubscriptionType();
            StartCoroutine(
                HttpUtils.PostRequest(EVENTSUB_SUBSCRIPTION_ENDPOINT, JsonUtility.ToJson(payload), Headers,
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
                        onSuccess(success);
                    },
                    (err) =>
                    {
                        Debug.LogError($"Error subscribing to {eventType} - {err}");
                        onError(err);
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
            return new IntegrationData()
            {
                token = USER_TOKEN
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
        }
    }
}
