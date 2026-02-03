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

        private readonly string[] USER_TOKEN_SCOPES = {
            "chat:read",
            "bits:read",
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

        // This allows us to swap out endpoints for mock ones against a testing engine
        private readonly IEndpoints TWITCH_API = new TwitchAPI();

        private readonly WebSocket _socket = new();

        // EventSub event handler registry
        private readonly Dictionary<string, Action<string>> EVENTSUB_HANDLERS = new();

        // Rolling cache for preventing duplicate messages from being processed
        private readonly LRUDictionary<string, EventSub.EventMessage<EventSub.EventPayload<string>>> RECENT_EVENTS = new(1000, (del) => {});


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

        #region Lifecycle

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
                if (_socket != null)
                {
                    data = _socket.GetNextResponse();
                    if (data != null)
                    {
                        ProcessSocketMessage(data);
                    }
                }
            } while (data != null);
        }

        private void ProcessSocketMessage(string msg)
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
                        EVENTSUB_HANDLERS.Clear();
                        DependencyManager subscriptionsManager = new(
                            () =>
                            {
                                Debug.Log("EventSub subscriptions complete, caching emotes and badges...");
                                // Subscribing to events is time-sensitive (sessionId will be invalidated after 10s of inactivity),
                                // So let's do our subscriptions before we do the heavy caching operation
                                // GetChannelEmotes(BROADCASTER_ID, (emotes) => { }, (err) => { Debug.LogError(err); });
                                // GetGlobalEmotes((emotes) => { }, (err) => { Debug.LogError(err); });
                                GetChannelBadges(BROADCASTER_ID, (badges) => { }, (err) => { Debug.LogError(err); });
                                GetGlobalBadges((badges) => { }, (err) => { Debug.LogError(err); });
                                // GetCurrentChatUsers((chatters) => {Debug.Log(string.Join(", ", chatters.Select(chatter => chatter.displayName))); }, (err) => { Debug.LogError(err); });
                            },
                            (key, pending) =>
                            {

                            }
                        );

                        string Subscribe<T>(Action<string, Action<EventSub.SubscriptionResponse>, Action<StreamError>, Action<T>> action, Action<T> onEvent) where T : EventSub.IEventSubEvent
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
                        Subscribe<EventSub.ChannelSubNewEvent>(SubscribeToChannelSubNewEvent, PrepareChannelSubscription);
                        Subscribe<EventSub.ChannelRaidEvent>(SubscribeToChannelRaidEvent, PrepareChannelRaid);
                        Subscribe<EventSub.ChannelUpdateEvent>(SubscribeToChannelUpdateEvent, PrepareChannelUpdate);
                        subscriptionsManager.Enable(true);
                    }
                    else if ("notification".Equals(message.metadata.message_type))
                    {
                        if (EVENTSUB_HANDLERS.ContainsKey(message.payload.subscription.type))
                        {
                            EVENTSUB_HANDLERS[message.payload.subscription.type](msg);
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

        #endregion

        #region Tokens

        public void RequestToken()
        {
            string userToken = TWITCH_API.USER_TOKEN_ENDPOINT
            + "?client_id=" + CLIENT_ID
            + "&redirect_uri=" + TWITCH_API.USER_TOKEN_REDIRECT
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
                Debug.Log($"Broadcaster ID: {BROADCASTER_ID}");
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
                _socket.Start(TWITCH_API.EVENTSUB_SOCKET_ENDPOINT,
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
                HttpUtils.GetRequest(TWITCH_API.TOKEN_VALIDATION_ENDPOINT, headers,
                    (str) =>
                    {
                        API.TokenValidationResponse response = JsonUtility.FromJson<API.TokenValidationResponse>(str);
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

        public void GetChannelInfo(ICollection<string> channels, Action<List<API.ChannelData>> onSuccess, Action<StreamError> onError)
        {
            string query = channels.Count > 0 ? $"?{string.Join('&', channels.Select(channel => "broadcaster_id=" + channel))}" : "";
            string url = $"{TWITCH_API.CHANNEL_INFO_ENDPOINT}{query}";
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        var channels = JsonUtility.FromJson<API.DataResponse<API.ChannelData>>(str).data;
                        onSuccess(channels);
                    },
                    (err) =>
                    {
                        onError(new StreamError(err));
                    }
                )
            );
        }

        public void GetSelfChannelInfo(Action<API.ChannelData> onSuccess, Action<StreamError> onError)
        {
            GetChannelInfo(new string[]{BROADCASTER_ID}, (list) =>
            {
                onSuccess(list[0]);
            }, onError);
        }

        #endregion

        #region User Info

        public void GetUserInfo(ICollection<string> userIds, Action<List<API.UserData>> onSuccess, Action<StreamError> onError)
        {
            string query = userIds.Count > 0 ? $"?{string.Join('&', userIds.Select(id => "id=" + id))}" : "";
            string url = $"{TWITCH_API.USER_INFO_ENDPOINT}{query}";
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        var users = JsonUtility.FromJson<API.DataResponse<API.UserData>>(str).data;
                        foreach (API.UserData user in users)
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

        public void GetSelfUserInfo(Action<API.UserData> onSuccess, Action<StreamError> onError)
        {
            GetUserInfo(new string[0], (list) =>
            {
                onSuccess(list[0]);
            }, onError);
        }

        #endregion

        #region Chatters

        // TODO: should this return a standardized Stream object, or the raw Twitch data model?
        public override void GetCurrentChatUsers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError)
        {
            string url = $"{TWITCH_API.CHATTERS_ENDPOINT}?broadcaster_id={BROADCASTER_ID}&moderator_id={BROADCASTER_ID}";
            List<StreamUser> chatters = new();
            void GetPage(string after = null)
            {
                string paginatedUrl = $"{url}{(after != null ? $"&after={after}" : "")}";
                StartCoroutine(
                    HttpUtils.GetRequest(paginatedUrl, Headers,
                        (str) =>
                        {
                            var page = JsonUtility.FromJson<API.PaginatedDataResponse<API.ChatterData>>(str);
                            foreach(API.ChatterData chatter in page.data)
                            {
                                chatters.Add(new StreamUser(chatter.user_name, chatter.user_id));
                            }
                            if(page.pagination != null && !string.IsNullOrEmpty(page.pagination.cursor))
                            {
                                GetPage(page.pagination.cursor);
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
            GetPage();
        }

        #endregion

        #region Emotes

        public void GetGlobalEmotes(Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log("Fetching global emotes.");
            string url = TWITCH_API.EMOTES_GLOBAL_ENDPOINT;
            GetBatchEmotes(url, onSuccess, onError);
        }

        public void GetChannelEmotes(string broadcasterId, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log($"Fetching channel emotes for Broadcaster ID: {broadcasterId}");
            string url = $"{TWITCH_API.EMOTES_CHANNEL_ENDPOINT}?broadcaster_id={broadcasterId}";
            GetBatchEmotes(url, onSuccess, onError);
        }

        public void GetSetEmotes(string setId, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log($"Fetching set emotes for set ID: {setId}");
            string url = $"{TWITCH_API.EMOTES_SET_ENDPOINT}?emote_set_id={setId}";
            GetBatchEmotes(url, onSuccess, onError);
        }

        private void GetBatchEmotes(string url, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            StartCoroutine(
                HttpUtils.GetRequest(url, Headers,
                    (str) =>
                    {
                        API.EmoteDataResponse response = JsonUtility.FromJson<API.EmoteDataResponse>(str);
                        List<StreamImage> emotes = new();
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

        public void GetIndividualEmote(API.EmoteData data, Action<StreamImage> onSuccess, Action<StreamError> onError)
        {
            string key = data.name;
            string format = data.format[^1];
            string scale = data.scale[^1];
            string url = TWITCH_API.EMOTES_INDIVIDUAL_ENDPOINT
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
            GetBatchBadges(TWITCH_API.BADGES_GLOBAL_ENDPOINT, onSuccess, onError);
        }

        public void GetChannelBadges(string broadcasterId, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            Debug.Log($"Fetching channel badges for Broadcaster ID: {broadcasterId}");
            string url = $"{TWITCH_API.BADGES_CHANNEL_ENDPOINT}?broadcaster_id={broadcasterId}";
            GetBatchBadges(url, onSuccess, onError);
        }

        private void GetBatchBadges(string url, Action<List<StreamImage>> onSuccess, Action<StreamError> onError)
        {
            StartCoroutine(
               HttpUtils.GetRequest(url, Headers,
               (str) =>
               {
                    API.DataResponse<API.BadgeSetData> response = JsonUtility.FromJson<API.DataResponse<API.BadgeSetData>>(str);
                    List<StreamImage> badges = new List<StreamImage>();
                    var chunks = CollectionUtils.Chunk(response.data, 50);
                    int count = chunks.Count;
                    if(count > 0)
                    {
                        void GetBatch(int index)
                        {
                            if(index < count){
                                Debug.Log($"Handling badge chunk {index} of {count}...");
                                DependencyManager chunkManager = new(
                                    () =>
                                    {
                                        Debug.Log($"{badges.Count} total badges resolved!");
                                        onSuccess(badges);
                                        GetBatch(index+1);
                                    },
                                    (key, pending) =>
                                    {
                                        Debug.Log($"Waiting on {pending} more badges to resolve...");
                                    }
                                );
                                foreach (API.BadgeSetData data in chunks[index])
                                {
                                    foreach (API.BadgeVersionData version in data.versions)
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
                        GetBatch(0);
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

        // Generic
        private void SubscribeToEvent<T>(EventSub.IEventSubscriptionRequest payload, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<T> onEvent) where T : EventSub.IEventSubEvent
        {
            string eventType = payload.GetSubscriptionType();
            StartCoroutine(
                HttpUtils.PostRequest(TWITCH_API.EVENTSUB_SUBSCRIPTION_ENDPOINT, JsonUtility.ToJson(payload), Headers,
                    (success) =>
                    {
                        EventSub.SubscriptionResponse response = JsonUtility.FromJson<EventSub.SubscriptionResponse>(success);
                        EventSub.SubscriptionData data = response.data[0];
                        // TODO: changed the key from id to type, which is not a good idea but works with the testing cli
                        EVENTSUB_HANDLERS[data.type] = (msg) =>
                        {
                            EventSub.EventMessage<EventSub.EventPayload<T>> obj = JsonUtility.FromJson<EventSub.EventMessage<EventSub.EventPayload<T>>>(msg);
                            onEvent(obj.payload.@event);
                        };
                        Debug.Log($"Subscribed to {eventType} - {data.id}");
                        onSuccess(response);
                    },
                    (err) =>
                    {
                        Debug.LogError($"Error subscribing to {eventType} - {err}");
                        onError(new StreamError(err));
                    }
                )
            );
        }

        // Chat Messages
        private void SubscribeToChatMessageEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChatMessageEvent> onEvent)
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
        private void SubscribeToChatMessageDeletionEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChatMessageDeletionEvent> onEvent)
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
            StreamUser chatter = new(chatEvent.chatter_user_name, chatEvent.chatter_user_id);
            List<StreamChatMessage.Fragment> fragments = new();
            List<StreamBadge> badges = new();
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () => { 
                    StreamChatMessage message = new(chatEvent.message_id, chatter, chatEvent.color, badges, fragments);
                    onChatMessage.Invoke(message); 
                }
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
                    GetIndividualEmote(new API.EmoteData(fragment.text, fragment.emote),
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
                StreamBadge newBadge = new(badge.info, badge.id);
                badges.Add(newBadge);
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
            StreamChatMessageDeletion deletion = new(chatEvent.message_id);
            onChatMessageDelete.Invoke(deletion);
        }

        // Redeems
        private void SubscribeToChannelPointRedeemEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelPointRedeemEvent> onEvent)
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
            StreamUser chatter = new(redeemEvent.user_name, redeemEvent.user_id);
            DependencyManager manager = new(
                () => { onChatRedeem.Invoke(new StreamChatRedeem(chatter, redeemEvent.reward.title, redeemEvent.reward.id, redeemEvent.reward.cost)); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        // Follow
        private void SubscribeToChannelFollowEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelFollowEvent> onEvent)
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
            StreamUser chatter = new(followEvent.user_name, followEvent.user_id);
            DependencyManager manager = new(
                () => { onChannelFollow.Invoke(new StreamChannelFollow(chatter, followEvent.followed_at)); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        // Subscriptions
        private void SubscribeToChannelSubNewEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelSubNewEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChannelSubNewSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChannelSubNewEventCondition()
                    {
                        broadcaster_user_id = BROADCASTER_ID,
                    }
                },
                onSuccess, onError, onEvent
            );
        }
        private void PrepareChannelSubscription(EventSub.ChannelSubNewEvent subEvent)
        {
            StreamUser chatter = new(subEvent.user_name, subEvent.user_id);
            StreamChannelPaidSubscription sub = new(chatter)
            {
                tier = subEvent.tier,
                isGifted = subEvent.is_gift,
                streak = 0,
            };
            DependencyManager manager = new(
                () => { onChannelPaidSubscription.Invoke(sub); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        // TODO: This and gift subs
        // private void SubscribeToChannelSubRenewalEvent(string sessionId, Action<string> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelSubGiftEvent> onEvent)
        // {
        //     SubscribeToEvent(
        //         new EventSub.ChannelSubRenewalSubscriptionRequest(sessionId)
        //         {
        //             condition = new EventSub.ChannelSubRenewalEventCondition()
        //             {
        //                 broadcaster_user_id = BROADCASTER_ID,
        //             }
        //         },
        //         onSuccess, onError, onEvent
        //     );
        // }
        // private void PrepareChannelSubscription(EventSub.ChannelSubRenewalEvent subEvent)
        // {
        //     StreamUser chatter = new(subEvent.user_name, subEvent.user_id);
        //     StreamChannelPaidSubscription sub = new(chatter)
        //     {
        //         tier = subEvent.,
        //         isGifted = false,
        //         streak = 0,
        //     };
        //     DependencyManager manager = new(
        //         () => { onChannelPaidSubscription.Invoke(sub); }
        //     );
        //     // TODO: we're going to need to collect info at some point, just setting this up for later
        //     string taskId = Guid.NewGuid().ToString();
        //     manager.AddDependency(taskId);
        //     manager.ResolveDependency(taskId);
        //     manager.Enable(true);
        // }

        // Raids
        private void SubscribeToChannelRaidEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelRaidEvent> onEvent)
        {
            SubscribeToEvent(
                new EventSub.ChannelRaidSubscriptionRequest(sessionId)
                {
                    condition = new EventSub.ChannelRaidEventCondition()
                    {
                        to_broadcaster_user_id = BROADCASTER_ID,
                    }
                },
                onSuccess, onError, onEvent
            );
        }
        private void PrepareChannelRaid(EventSub.ChannelRaidEvent raidEvent)
        {
            StreamUser raider = new StreamUser(raidEvent.from_broadcaster_user_name, raidEvent.from_broadcaster_user_id);
            DependencyManager manager = new(
                () => { onChannelRaid.Invoke(new StreamRaid(raider, raidEvent.viewers)); }
            );
            // TODO: we're going to need to collect info at some point, just setting this up for later
            string taskId = Guid.NewGuid().ToString();
            manager.AddDependency(taskId);
            manager.ResolveDependency(taskId);
            manager.Enable(true);
        }

        // Channel Info
        private void SubscribeToChannelUpdateEvent(string sessionId, Action<EventSub.SubscriptionResponse> onSuccess, Action<StreamError> onError, Action<EventSub.ChannelUpdateEvent> onEvent)
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

        #region Helper Methods

        private void ChatMessageToStreamMessage(EventSub.ChatMessageEvent source, Action<StreamChatMessage> onComplete)
        {
            // TODO 
        }

        private void GenericMessageToStreamMessage(EventSub.GenericMessage source, Action<StreamChatMessage> onComplete)
        {
            
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
