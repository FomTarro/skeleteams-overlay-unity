using System;
using System.Collections.Generic;
using System.Linq;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.Collections;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    public class TwitchIntegration : StreamIntegration<TwitchIntegration, TwitchIntegration.IntegrationData>
    {
        public override string FileName => "twitch.json";

        private const string CLIENT_ID = "x2rikvl9behn8k54flc95ulhbq265m";
        private string USER_TOKEN = "NO_TOKEN_SET";
        private string BROADCASTER_ID = "NO_ID_SET";

        // This allows us to swap out endpoints for mock ones against a testing engine
        private readonly IEndpoints API = new TwitchAPI();

        private readonly string[] USER_TOKEN_SCOPES = {
            "chat:read",
            "user:read:chat",
            "channel:read:redemptions",
            "channel:read:subscriptions",
            "moderator:read:followers",
            "moderator:read:chatters"
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

        private List<Endpoint> WebServerEndpoints { 
            get { 
                return new()
                {
                    new Endpoint("/twitch/oauth2", (req) =>
                    {
                        return new EndpointResponse(200, _tokenRedirectPage.text);
                    }),
                    new Endpoint("/twitch/token", (req) =>
                    {
                        TokenRedirectData data = JsonUtility.FromJson<TokenRedirectData>(req.body);
                        SetToken(data.token);
                        return new EndpointResponse(200, "OK");
                    }),
                    // TODO: this probably goes in the integration manager
                    new Endpoint("/twitch/reload", (req) =>
                    {
                        Enable();
                        return new EndpointResponse(200, "Reload Requested");
                    })
                };
         }
        }

        public override void Enable()
        {
            // Set up token ingest endpoints 
            foreach(Endpoint endpoint in WebServerEndpoints)
            {
                _webServer.RegisterEndpoint(endpoint);
            }
            FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        }

        public override void Disable()
        {
            foreach(Endpoint endpoint in WebServerEndpoints)
            {
                _webServer.UnregisterEndpoint(endpoint);
            }
            _socket.Stop();
        }

        public override void Initialize()
        {
            // TODO: move this to a manager
            Enable();
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

        // Rolling cache for preventing duplicate messages from being processed
        private readonly LRUDictionary<string, EventSub.EventMessage<EventSub.EventPayload<string>>> RECENT_EVENTS = new(1000, (del) => {});

        private void ProcessEventSubEvent(string msg)
        {
            try
            {
                EventSub.EventMessage<EventSub.EventPayload<string>> message = JsonUtility.FromJson<EventSub.EventMessage<EventSub.EventPayload<string>>>(msg);
                if(!RECENT_EVENTS.ContainsKey(message.metadata.message_id))
                {
                    RECENT_EVENTS.Add(message.metadata.message_id, message);
                    if ("session_welcome".Equals(message.metadata.message_type))
                    {
                        EventSub.EventMessage<EventSub.WelcomePayload> session = JsonUtility.FromJson<EventSub.EventMessage<EventSub.WelcomePayload>>(msg);
                        string sessionId = session.payload.session.id;
                        _subscriptions.Clear();
                        DependencyManager subscriptionsManager = new(
                            () =>
                            {
                                Debug.Log("EventSub subscriptions complete, caching emotes and badges...");
                                // Subscribing to events is time-sensitive (sessionId will be invalidated after 10s of inactivity),
                                // So let's do our subscriptions before we do the heavy caching operation
                                // GetChannelEmotes(BROADCASTER_ID, (emotes) => { }, (err) => { Debug.LogError(err); });
                                // GetGlobalEmotes((emotes) => { }, (err) => { Debug.LogError(err); });
                                // GetChannelBadges(BROADCASTER_ID, (badges) => { }, (err) => { Debug.LogError(err); });
                                // GetGlobalBadges((badges) => { }, (err) => { Debug.LogError(err); });
                                GetChatters((chatters) => {Debug.Log(string.Join(", ", chatters.Select(chatter => chatter.user_name))); }, (err) => { Debug.LogError(err); });
                            },
                            (key, pending) =>
                            {

                            }
                        );

                        string Subscribe<T>(Action<string, Action<string>, Action<StreamError>, Action<T>> action, Action<T> onEvent) where T : EventSub.IEventSubEvent
                        {
                            string id = Guid.NewGuid().ToString();
                            subscriptionsManager.AddDependency(id);
                            action(sessionId,
                            (success) =>
                            {
                                subscriptionsManager.ResolveDependency(id);
                            },
                            (err) =>
                            {
                                subscriptionsManager.ResolveDependency(id);
                            }, onEvent);
                            return id;
                        }
                        // Kick off all HTTP subscriptions
                        Subscribe<EventSub.ChatMessageEvent>(SubscribeToChatMessageEvent, PrepareChatMessage);
                        Subscribe<EventSub.ChatMessageDeletionEvent>(SubscribeToChatMessageDeletionEvent, PrepareChatMessageDeletion);
                        Subscribe<EventSub.ChannelPointRedeemEvent>(SubscribeToChannelPointRedeemEvent, PrepareChannelRedeem);   
                        Subscribe<EventSub.ChannelFollowEvent>(SubscribeToChannelFollowEvent, PrepareChannelFollow);
                        Subscribe<EventSub.ChannelUpdateEvent>(SubscribeToChannelUpdateEvent, PrepareChannelUpdate);
                        subscriptionsManager.Enable(true);
                    }
                    else if ("notification".Equals(message.metadata.message_type))
                    {
                        if (_subscriptions.ContainsKey(message.payload.subscription.id))
                        {
                            _subscriptions[message.payload.subscription.id](msg);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Duplicate event recieved with ID: {message.metadata.message_id}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        #region Tokens

        public void RequestToken()
        {
            string userToken = API.USER_TOKEN_ENDPOINT
            + "?client_id=" + CLIENT_ID
            + "&redirect_uri=" + API.USER_TOKEN_REDIRECT
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
                Debug.Log($"Acquired Broadcaster ID: {BROADCASTER_ID}");
                GetSelfChannelInfo((channel) =>
                {
                    StreamInfo info = new()
                    {
                        title = channel.title,
                        language = channel.broadcaster_language,
                        categoryId = channel.game_id,
                        categoryName = channel.game_name,
                        tags = new List<string>(channel.tags)
                    };
                    onStreamInfoUpdate.Invoke(info);
                }, (err) =>
                {
                    Debug.LogError(err);
                });
                _socket.Start(API.EVENTSUB_SOCKET_ENDPOINT,
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
                HttpUtils.GetRequest(API.TOKEN_VALIDATION_ENDPOINT, headers,
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

        #endregion

        #region Channel Info

        public void GetChannelInfo(ICollection<string> channels, Action<List<ChannelData>> onSuccess, Action<StreamError> onError)
        {
            string query = channels.Count > 0 ? $"?{string.Join('&', channels.Select(channel => "broadcaster_id=" + channel))}" : "";
            string url = $"{API.CHANNEL_INFO_ENDPOINT}{query}";
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        var channels = JsonUtility.FromJson<DataResponse<ChannelData>>(str).data;
                        onSuccess(channels);
                    },
                    (err) =>
                    {
                        onError(new StreamError(err));
                    }
                )
            );
        }

        public void GetSelfChannelInfo(Action<ChannelData> onSuccess, Action<StreamError> onError)
        {
            GetChannelInfo(new string[1]{BROADCASTER_ID}, (list) =>
            {
                onSuccess(list[0]);
            }, onError);
        }

        #endregion

        #region User Info

        public void GetUserInfo(ICollection<string> users, Action<List<UserData>> onSuccess, Action<StreamError> onError)
        {
            string query = users.Count > 0 ? $"?{string.Join('&', users.Select(user => "login=" + user))}" : "";
            string url = $"{API.USER_INFO_ENDPOINT}{query}";
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        var users = JsonUtility.FromJson<DataResponse<UserData>>(str).data;
                        foreach (UserData user in users)
                        {
                            ImageHandler.GetFromRemote(
                                user.profile_image_url, Headers,
                                $"avatar_{user.login}", (success) =>
                                {
                                    // TODO: how to get user avatars intelligently when we often only have the login id
                                    // Can we cache user data, then fetch from the stored URL as needed?
                                },
                                (err) =>
                                {

                                }
                            );
                        }
                        onSuccess(users);
                    },
                    (err) =>
                    {
                        onError(new StreamError(err));
                    }
                )
            );
        }

        public void GetSelfUserInfo(Action<UserData> onSuccess, Action<StreamError> onError)
        {
            GetUserInfo(new string[0], (list) =>
            {
                onSuccess(list[0]);
            }, onError);
        }

        #endregion

        #region Chatters

        public void GetChatters(Action<List<ChatterData>> onSuccess, Action<StreamError> onError)
        {
            string url = $"{API.CHATTERS_ENDPOINT}?broadcaster_id={BROADCASTER_ID}&moderator_id={BROADCASTER_ID}";
            List<ChatterData> chatters = new List<ChatterData>();
            void GetNextPage(string after = null)
            {
                string paginatedUrl = $"{url}{(after != null ? $"&after={after}" : "")}";
                StartCoroutine(
                    HttpUtils.GetRequest(paginatedUrl, Headers,
                        (str) =>
                        {
                            var page = JsonUtility.FromJson<PaginatedDataResponse<ChatterData>>(str);
                            chatters.AddRange(page.data);
                            if(page.pagination != null && !string.IsNullOrEmpty(page.pagination.cursor))
                            {
                                GetNextPage(page.pagination.cursor);
                            }
                            else
                            {
                                onSuccess(chatters);   
                            }
                        },
                        (err) =>
                        {
                            onError(new StreamError(err));
                        }
                    )
                );
            }
            GetNextPage();
        }

        #endregion

        #region Emotes

        public void GetGlobalEmotes(Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log("Fetching global emotes.");
            string url = API.EMOTES_GLOBAL_ENDPOINT;
            GetBatchEmotes(url, onSuccess, onError);
        }

        public void GetChannelEmotes(string broadcasterId, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log($"Fetching channel emotes for Broadcaster ID: {broadcasterId}");
            string url = $"{API.EMOTES_CHANNEL_ENDPOINT}?broadcaster_id={broadcasterId}";
            GetBatchEmotes(url, onSuccess, onError);
        }

        public void GetSetEmotes(string setId, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log($"Fetching set emotes for set ID: {setId}");
            string url = $"{API.EMOTES_SET_ENDPOINT}?emote_set_id={setId}";
            GetBatchEmotes(url, onSuccess, onError);
        }

        private void GetBatchEmotes(string url, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        EmoteDataResponse response = JsonUtility.FromJson<EmoteDataResponse>(str);
                        List<StreamImage> emotes = new List<StreamImage>();
                        // EMOTES_INDIVIDUAL_ENDPOINT = response.template;
                        var chunks = CollectionUtils.Chunk(response.data, 50);
                        int count = chunks.Count;
                        if(count > 0)
                        {
                            void Batch(int index)
                            {
                                if(index < count){
                                    Debug.Log($"Handling emote chunk {index} of {count}...");
                                    DependencyManager chunkManager = new(
                                        () =>
                                        {
                                            Debug.Log($"{emotes.Count} total emotes resolved!");
                                            onSuccess(emotes);
                                            Batch(index+1);
                                        },
                                        (key, pending) =>
                                        {
                                            Debug.Log($"Waiting on {pending} more emotes to resolve...");
                                        }
                                    );
                                    foreach(var data in chunks[index])
                                    {
                                        string taskId = Guid.NewGuid().ToString();
                                        chunkManager.AddDependency(taskId);
                                        GetIndividualEmote(data,
                                        (success) =>
                                        {
                                            emotes.Add(success);
                                            chunkManager.ResolveDependency(taskId);
                                        },
                                        (err) =>
                                        {
                                            Debug.LogError(err);
                                            chunkManager.ResolveDependency(taskId);
                                        });
                                    }
                                    chunkManager.Enable(true);
                                }
                                else
                                {
                                    onSuccess(emotes);
                                }
                            }
                            Batch(0);
                        }
                        else
                        {
                            onSuccess(emotes);
                        }
                    },
                    (err) =>
                    {
                        onError(new StreamError(err));
                    }
                )
            );
        }

        public void GetIndividualEmote(EmoteData data, Action<StreamImage> onSuccess, Action<StreamError> onError)
        {
            string key = data.name;
            string format = data.format[^1];
            string scale = data.scale[^1];
            string url = API.EMOTES_INDIVIDUAL_ENDPOINT
            .Replace("{{id}}", data.id)
            .Replace("{{format}}", format)
            .Replace("{{theme_mode}}", "light")
            .Replace("{{scale}}", scale);
            ImageHandler.GetFromRemote(url, Headers, key, onSuccess, onError);
        }

        #endregion

        #region Badges

        public void GetGlobalBadges(Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log("Fetching global badges.");
            GetBatchBadges(API.BADGES_GLOBAL_ENDPOINT, onSuccess, onError);
        }

        public void GetChannelBadges(string broadcasterId, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log($"Fetching channel badges for Broadcaster ID: {broadcasterId}");
            string url = $"{API.BADGES_CHANNEL_ENDPOINT}?broadcaster_id={broadcasterId}";
            GetBatchBadges(url, onSuccess, onError);
        }

        private void GetBatchBadges(string url, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            StartCoroutine(
               HttpUtils.GetRequest(url, Headers,
               (str) =>
               {
                    DataResponse<BadgeSetData> response = JsonUtility.FromJson<DataResponse<BadgeSetData>>(str);
                    List<StreamImage> badges = new List<StreamImage>();
                    var chunks = CollectionUtils.Chunk(response.data, 50);
                    int count = chunks.Count;
                    if(count > 0)
                    {
                        void Batch(int index)
                        {
                            if(index < count){
                                Debug.Log($"Handling badge chunk {index} of {count}...");
                                DependencyManager chunkManager = new(
                                    () =>
                                    {
                                        Debug.Log($"{badges.Count} total badges resolved!");
                                        onSuccess(badges);
                                        Batch(index+1);
                                    },
                                    (key, pending) =>
                                    {
                                        Debug.Log($"Waiting on {pending} more badges to resolve...");
                                    }
                                );
                                foreach (BadgeSetData data in chunks[index])
                                {
                                    foreach (BadgeVersionData version in data.versions)
                                    {
                                        string taskId = Guid.NewGuid().ToString();
                                        chunkManager.AddDependency(taskId);
                                        ImageHandler.GetFromRemote(version.image_url_1x, Headers,
                                        $"badge_{data.set_id}_{version.id}",
                                        (success) =>
                                        {
                                            badges.Add(success);
                                            chunkManager.ResolveDependency(taskId);
                                        },
                                        (err) =>
                                        {
                                            Debug.LogError(err);
                                            chunkManager.ResolveDependency(taskId);
                                        });
                                    }
                                }
                                chunkManager.Enable(true);
                            }
                            else
                            {
                                onSuccess(badges);
                            }
                        }
                        Batch(0);
                    }
                    else
                    {
                        onSuccess(badges);
                    }
               },
               (err) =>
               {
                   onError(new StreamError(err));
               })
            );
        }

        #endregion

        #region EventSub

        private void SubscribeToEvent<T>(EventSub.IEventSubscriptionRequest payload, Action<string> onSuccess, Action<StreamError> onError, Action<T> onEvent) where T : EventSub.IEventSubEvent
        {
            string eventType = payload.GetSubscriptionType();
            StartCoroutine(
                HttpUtils.PostRequest(API.EVENTSUB_SUBSCRIPTION_ENDPOINT, JsonUtility.ToJson(payload), Headers,
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
                        onError(new StreamError(err));
                    }
                )
            );
        }

        private void SubscribeToChatMessageEvent(string sessionId, Action<string> onSuccess, Action<StreamError> onError, Action<EventSub.ChatMessageEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChatMessageSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChatMessageEventCondition()
                    {
                        broadcaster_user_id = BROADCASTER_ID,
                        user_id = BROADCASTER_ID
                    }
                },
                onSuccess, onError, onEvent
            );
        }

        private void SubscribeToChatMessageDeletionEvent(string sessionId, Action<string> onSuccess, Action<StreamError> onError, Action<EventSub.ChatMessageDeletionEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChatMessageDeletionSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChatMessageDeletionEventCondition()
                    {
                        broadcaster_user_id = BROADCASTER_ID,
                        user_id = BROADCASTER_ID
                    }
                },
                onSuccess, onError, onEvent
            );
        }

        private void PrepareChatMessage(EventSub.ChatMessageEvent chatEvent)
        {
            StreamChatUser chatter = new StreamChatUser(chatEvent.chatter_user_name, chatEvent.chatter_user_id, chatEvent.color);
            List<StreamChatMessage.Fragment> fragments = new List<StreamChatMessage.Fragment>();
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () => { onChatMessage.Invoke(new StreamChatMessage(chatEvent.message_id, chatter, fragments)); }
            );
            // TODO: get user avatar? Seems like too much for every chat message.
            foreach (EventSub.ChatMessageFragment fragment in chatEvent.message.fragments)
            {
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                if ("emote".Equals(fragment.type))
                {
                    var newFragment = new StreamChatMessage.Fragment(StreamChatMessage.Fragment.Type.EMOTE, fragment.text);
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
                    var newFragment = new StreamChatMessage.Fragment(StreamChatMessage.Fragment.Type.TEXT, fragment.text);
                    fragments.Add(newFragment);
                    manager.ResolveDependency(taskId);
                }
            }
            foreach (EventSub.ChatMessageBadge badge in chatEvent.badges)
            {
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                StreamBadge newBadge = new StreamBadge(badge.info, badge.id);
                chatter.badges.Add(newBadge);
                ImageHandler.GetFromCache(
                    $"badge_{badge.set_id}_{badge.id}",
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

        private void PrepareChatMessageDeletion(EventSub.ChatMessageDeletionEvent chatEvent)
        {
            StreamChatMessageDeletion deletion = new StreamChatMessageDeletion(chatEvent.message_id);
            onChatMessageDelete.Invoke(deletion);
        }

        private void SubscribeToChannelPointRedeemEvent(string sessionId, Action<string> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelPointRedeemEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChannelPointRedeemSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChannelPointRedeemEventCondition()
                    {
                        broadcaster_user_id = BROADCASTER_ID,
                    }
                },
                onSuccess, onError, onEvent
            );
        }

        private void PrepareChannelRedeem(EventSub.ChannelPointRedeemEvent redeemEvent)
        {
            StreamChatUser chatter = new StreamChatUser(redeemEvent.user_name, redeemEvent.user_id);
            DependencyManager manager = new(
                () => { onChatRedeem.Invoke(new StreamChatRedeem(chatter, redeemEvent.reward.title, redeemEvent.reward.id, redeemEvent.reward.cost)); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        private void SubscribeToChannelFollowEvent(string sessionId, Action<string> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelFollowEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChannelFollowSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChannelFollowEventCondition()
                    {
                        broadcaster_user_id = BROADCASTER_ID,
                        moderator_user_id = BROADCASTER_ID
                    }
                },
                onSuccess, onError, onEvent
            );
        }

        private void PrepareChannelFollow(EventSub.ChannelFollowEvent followEvent)
        {
            StreamChatUser chatter = new StreamChatUser(followEvent.user_name, followEvent.user_id);
            DependencyManager manager = new(
                () => { onChannelFollow.Invoke(new StreamChannelFollow(chatter, followEvent.followed_at)); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        private void SubscribeToChannelUpdateEvent(string sessionId, Action<string> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelUpdateEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChannelUpdateSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChannelUpdateEventCondition()
                    {
                        broadcaster_user_id = BROADCASTER_ID,
                    }
                },
                onSuccess, onError, onEvent
            );
        }

        private void PrepareChannelUpdate(EventSub.ChannelUpdateEvent updateEvent)
        {
            StreamInfo info = new()
            {
                title = updateEvent.title,
                language = updateEvent.language,
                categoryId = updateEvent.category_id,
                categoryName = updateEvent.category_name,
                tags = new List<string>()
            };
            DependencyManager manager = new(
                () => { onStreamInfoUpdate.Invoke(info); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        #endregion

        #region File IO

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

        #endregion
    }
}
