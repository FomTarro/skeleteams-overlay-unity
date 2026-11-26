using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Skeletom.BattleStation.Integrations.Discord.IPC;
using Skeletom.BattleStation.Utils;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.Events;
// TODO: break these events out in to interfaces
using static Skeletom.BattleStation.Integrations.StreamIntegration<
Skeletom.BattleStation.Integrations.Discord.DiscordIntegration.IntegrationData
>;

namespace Skeletom.BattleStation.Integrations.Discord
{
    public class DiscordIntegration : Integration<DiscordIntegration.IntegrationData>
    {
        private readonly Utils.Logger LOGGER = new("Discord");
        public override string FileName => "discord.json";

        private const string CLIENT_ID = "1524610169516986429";
        private string USER_TOKEN = "NO_TOKEN_SET";
        public string SelfUserId { get; private set; } = "NO_ID_SET";
        private string TEXT_CHANNEL = "";

        // This allows us to swap out endpoints for mock ones against a testing engine
        private readonly IDiscordEndpoints DISCORD_API = new DiscordAPI();
        
        private IpcSocket _socket;
        private const int REQUEST_TIMEOUT_MS = 4000;
        private readonly Dictionary<string, RequestCallback> REQUEST_RESPONSE_HANDLERS = new();
        private readonly Dictionary<string, Action<string>> EVENT_SUBSCRIPTION_HANDLERS = new();

        private class RequestCallback
        {
            public readonly Action<string> onSuccess;
            public readonly Action<ErrorResponse> onError;
            public float timeout;
            public RequestCallback(
                Action<string> onSuccess, 
                Action<ErrorResponse> onError, 
                float timeout)
            {
                this.onSuccess = onSuccess;
                this.onError = onError;
                this.timeout = timeout;
            }
        }

        private HttpUtils.HttpHeaders Headers
        {
            get
            {
                return new HttpUtils.HttpHeaders()
                {
                    customHeaders = {
                        {"Authorization", $"Bot {USER_TOKEN}"}
                    }
                };
            }
        }

        #region Caches

        [SerializeField]
        private StreamImageHandler _imageHandler;
        public StreamImageHandler ImageHandler
        {
            get
            {
                if(_imageHandler == null)
                {
                    _imageHandler = GetComponent<StreamImageHandler>();
                    if(_imageHandler == null)
                    {
                        _imageHandler = gameObject.AddComponent<StreamImageHandler>();
                    }
                }
                return _imageHandler;
            }
        }

        private string LAST_MESSAGE_RECEIVED;      
        private string CURRENT_VOICE_CHANNEL_ID;
        [SerializeField]
        private List<StreamUser> CURRENT_CHANNEL_USERS = new();

        #endregion

        #region Events

        [Serializable]
        public class UserEvent : UnityEvent<StreamUser> { }
        public UserEvent onUserJoin = new();

        [Serializable]
        public class UserIdEvent : UnityEvent<string> { }
        public UserIdEvent onUserLeave = new();
        public UserIdEvent onUserSpeakStart = new();
        public UserIdEvent onUserSpeakStop = new();

        // TODO: break these out into an interface if possible
        public ChatMessageEvent onChatMessage = new();
        public ChatMessageDeletionEvent onChatMessageDelete = new();

        #endregion

        #region Lifecycle

        public override void Enable()
        {
            if(_socket == null){
                string[] names = new string[10];
                for(int i = 0; i < 10; i++)
                {
                    names[i] = $"discord-ipc-{i}";
                }
                _socket = new IpcSocket(names, REQUEST_TIMEOUT_MS + 1000);
            }
            FromSaveData(SaveDataManager.ReadSaveData(this));
        }

        public override void Disable()
        {
            _socket?.Disconnect();
            HandleSelfChannelLeave();
        }

        private void Update()
        {
            if(_socket != null)
            {
                _socket.Tick();
                string data;
                do
                {
                    data = _socket.GetNextResponse();
                    if (data != null)
                    {
                        ProcessSocketMessage(data);
                    }
                } while (data != null);
                // Process Timeouts
                ProcessRequestTimeouts(Time.deltaTime);
            }
        }

        /// <summary>
        /// Processes an incoming message from the IPC socket, 
        /// resolving pending requests if applicable
        /// </summary>
        /// <param name="data">The message to process</param>
        private void ProcessSocketMessage(string data)
        {
            LAST_MESSAGE_RECEIVED = data;
            DataResponse<string> header = JsonUtility.FromJson<DataResponse<string>>(data);
            if (header == null){
                return;
            }
            // Debug.Log(data);
            // Subscribed Events
            if ("DISPATCH".Equals(header.cmd) && !string.IsNullOrEmpty(header.evt))
            {
                if (EVENT_SUBSCRIPTION_HANDLERS.ContainsKey(header.evt))
                {
                    EVENT_SUBSCRIPTION_HANDLERS[header.evt](data);
                }
            } else
            // Commands
            {
                if(!string.IsNullOrEmpty(header.nonce) 
                && REQUEST_RESPONSE_HANDLERS.ContainsKey(header.nonce))
                {
                    if (!string.IsNullOrEmpty(header.evt) && "ERROR".Equals(header.evt))
                    {
                        ErrorResponse err = JsonUtility.FromJson<ErrorResponse>(data);
                        REQUEST_RESPONSE_HANDLERS[header.nonce].onError(err); 
                    }
                    else
                    {
                        REQUEST_RESPONSE_HANDLERS[header.nonce].onSuccess(data); 
                    }
                    REQUEST_RESPONSE_HANDLERS.Remove(header.nonce);  
                }
            }
        }


        private readonly List<string> TIMEOUTS = new();
        /// <summary>
        /// Decrements all pending request timeouts by the provided delta,
        /// and invokes the OnError callback for requests which expire.
        /// </summary>
        /// <param name="delta">The time delta in milliseconds</param>
        private void ProcessRequestTimeouts(float delta)
        {
            foreach(var kvp in REQUEST_RESPONSE_HANDLERS)
            {
                kvp.Value.timeout -= delta;
                if(kvp.Value.timeout < 0f)
                {
                    ErrorResponse err = new()
                    {
                        data = new()
                        {
                            code = 0,
                            message = "Request timeout."
                        }
                    };
                    kvp.Value.onError(err);
                    TIMEOUTS.Add(kvp.Key);
                }
            }
            for(int i = 0; i < TIMEOUTS.Count; i++)
            {
                REQUEST_RESPONSE_HANDLERS.Remove(TIMEOUTS[i]);
            }
            TIMEOUTS.Clear();
        }

        private void ConnectToIPC()
        {
            _socket.Connect(() =>
            {
                Handshake((s) =>
                {
                    LOGGER.Log($"IPC Handshake complete for user: {s.data.user.global_name}");
                    SelfUserId = s.data.user.id;
                    ValidateToken(USER_TOKEN, 
                    (s) =>
                    {
                        LOGGER.Log($"IPC Authentication successful with existing token!");
                    },
                    (err) =>
                    {
                        // Get a new token if validating our current token fails
                        RequestToken(
                        (s) =>
                        {
                            LOGGER.Log($"IPC Authentication successful with new token!");
                        },
                        (err) =>
                        {
                            LOGGER.Log($"IPC unable to authenticate: {err.data.message}");
                        });
                    });
                },
                (err) =>
                {
                    LOGGER.LogError($"IPC Handshake failed with error: {err.data.message}");
                });
            },
            () =>
            {
                LOGGER.LogWarning("IPC disconnected!");
            },
            (e) =>
            {
                LOGGER.LogError("IPC Error: " + e);
            });
        }

        #endregion

        #region Send

        private void Send(int opCode, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] header = new byte[8 + payload.Length];
            BitConverter.GetBytes(opCode).CopyTo(header, 0);
            BitConverter.GetBytes(payload.Length).CopyTo(header, 4);
            payload.CopyTo(header, 8);
            _socket.Send(header);
        }

        private void SendRequest<I, O>(I request, Action<O> onSuccess, Action<ErrorResponse> onError) where I : IpcMessage, IArgRequest
        {
            request.nonce = Guid.NewGuid().ToString();
            RequestCallback callbacks = new(
                (str) =>
                {
                    O response = JsonUtility.FromJson<O>(str);
                    onSuccess(response);
                },
                (err) =>
                {
                    onError(err);
                },
                REQUEST_TIMEOUT_MS
            );
            REQUEST_RESPONSE_HANDLERS[request.nonce] = callbacks;
            Send(1, JsonUtility.ToJson(request));
        }

        private void SendSubscription<I, O>(string channelId, Action<O> onEvent) where I: EventSubscriptionRequest, new() where O : IpcMessage, IDataResponse
        {
            I request = new()
            {
                nonce = Guid.NewGuid().ToString(),
                args = new(){ channel_id = channelId }
            };
            EVENT_SUBSCRIPTION_HANDLERS[request.evt] = (str) =>
            {
                O response = JsonUtility.FromJson<O>(str);
                onEvent(response);
            };
            SendRequest<I, IpcMessage>(request, 
            (s) =>
            {
                LOGGER.Log($"Subscription to IPC Event: {request.evt} successfully updated.");
            }, 
            (err) => 
            { 
                LOGGER.LogError($"Error while updating subscription to IPC Event: {request.evt} - {err.data.message}"); 
            });
        }

        private void SendUnsubscription<I>(string channelId) where I : EventSubscriptionRequest, new()
        {
            I request = new()
            {
                cmd = "UNSUBSCRIBE",
                nonce = Guid.NewGuid().ToString(),
                args = new(){ channel_id = channelId }
            };
            EVENT_SUBSCRIPTION_HANDLERS.Remove(request.evt);
            SendRequest<I, IpcMessage>(request, (s) => { }, (err) => { });
        }

        #endregion

        #region Handshake

        private void Handshake(Action<HandshakeResponse> onSuccess, Action<ErrorResponse> onError)
        {
            const string eventCode = "READY";
            EVENT_SUBSCRIPTION_HANDLERS[eventCode] = (str) =>
            {
                REQUEST_RESPONSE_HANDLERS.Remove(eventCode);
                onSuccess(JsonUtility.FromJson<HandshakeResponse>(str)); 
            };
            RequestCallback callbacks = new(
                (s) => { },
                (err) => {
                    ErrorResponseData data = JsonUtility.FromJson<ErrorResponseData>(LAST_MESSAGE_RECEIVED);
                    err.data.code = data.code;
                    err.data.message = data.message;
                    onError(err);
                },
                REQUEST_TIMEOUT_MS
            );
            REQUEST_RESPONSE_HANDLERS[eventCode] = callbacks;
            Send(0, JsonUtility.ToJson(
                new HandshakeRequest
                {
                    v = 1,
                    client_id = CLIENT_ID,
                }
            ));
        }

        #endregion

        #region Tokens

        private void SetToken(string token)
        {
            // Save our updated token
            USER_TOKEN = token;
            SaveDataManager.WriteSaveData(this);
            // Check if we're currently in a voice channel
            HandleSelfChannelJoin();
            // Begin event subscriptions
            SubscribeToVoiceChannelSelectEvent(
            (res) =>
            {
                if(!string.IsNullOrEmpty(res.data.channel_id))
                {
                    string newChannel = res.data.channel_id;
                    LOGGER.Log($"User has joined channel with ID: {newChannel}");
                    // If our channel ID has changed,
                    // Leave the old channel
                    if(newChannel != CURRENT_VOICE_CHANNEL_ID)
                    {
                        HandleSelfChannelLeave();
                    }
                    // When we join a new channel, 
                    // broadcast its current state and subscribe to new events
                    HandleSelfChannelJoin();
                }
                else
                {
                    // If no channel ID is in the response...
                    LOGGER.Log("User has left the channel");
                    // Unsub from old channel
                    HandleSelfChannelLeave();
                }
                CURRENT_VOICE_CHANNEL_ID = res.data.channel_id;
            });
            if(!string.IsNullOrEmpty(TEXT_CHANNEL)){
                SetTextChannel(TEXT_CHANNEL, (s) =>
                {
                    LOGGER.Log($"Successfully subscribed to text channel: {TEXT_CHANNEL}");
                }, (e) =>
                {
                    LOGGER.LogError($"Error subscribing to text channel: {TEXT_CHANNEL} - {e.data.message}");
                });
            }
        }

        private void RequestToken(Action<AuthenticateResponse> onSuccess, Action<ErrorResponse> onError)
        {
            string codeVerifier = PKCE.GenerateCodeVerifier();
            string codeChallenge = PKCE.GenerateCodeChallenge(codeVerifier);
            AuthorizeRequest request = new()
            {
                args = new AuthorizeRequestArgs
                {
                    client_id = CLIENT_ID,
                    scopes = new[] { "rpc", "rpc.voice.read", "identify", "messages.read" },
                    code_challenge = codeChallenge,
                    code_challenge_method = "S256"
                }
            };
            SendRequest<AuthorizeRequest, AuthorizeResponse>(request, 
            (res) =>
            {
                ExchangeCodeForToken(res.data.code, codeVerifier, (res) =>
                {
                    ValidateToken(res.access_token, onSuccess, onError);
                }, onError);
            }, onError);
        }

        private void ExchangeCodeForToken(string code, string codeVerifier, Action<API.TokenResponse> onSuccess, Action<ErrorResponse> onError)
        {
            string body = $"client_id={Uri.EscapeDataString(CLIENT_ID)}" +
                $"&grant_type=authorization_code" +
                $"&code={Uri.EscapeDataString(code)}" +
                $"&code_verifier={Uri.EscapeDataString(codeVerifier)}";
            HttpUtils.HttpHeaders headers = new()
            {
                contentType="application/x-www-form-urlencoded",
            };
            StartCoroutine(
                HttpUtils.PostRequest(
                    DISCORD_API.USER_TOKEN_ENDPOINT,
                    body,
                    headers,
                    (str) =>
                    {
                        API.TokenResponse res = JsonUtility.FromJson<API.TokenResponse>(str);
                        onSuccess(res);
                    },
                    (err) =>
                    {
                        ErrorResponse res = new()
                        {
                            data = new()
                            {
                                code=(int)err.statusCode,
                                message=err.message
                            }
                        };
                        onError(res);
                    }
                )
            );
        }

        private void ValidateToken(string token, Action<AuthenticateResponse> onSuccess, Action<ErrorResponse> onError)
        {
            AuthenticateRequest request = new()
            {
                args = new AuthenticateRequestArgs { access_token = token }
            };
            SendRequest<AuthenticateRequest, AuthenticateResponse>(request, (s) => {
                onSuccess(s);
                SetToken(s.data.access_token);
            }, onError);
        }

        #endregion

        #region Voice Channel State

        private void GetVoiceChannelState(Action<VoiceChannelStateResponse> onSuccess, Action<ErrorResponse> onError)
        {
            VoiceChannelStateRequest request = new()
            {
                args = new(){}
            };
            SendRequest(request, onSuccess, onError);
        }

        #endregion

        #region User Info

        private void GetUserAvatar(UserData user, Action<StreamImage> onSuccess, Action<StreamError> onError)
        {
            ImageHandler.GetFromRemote(
                $"https://cdn.discordapp.com/avatars/{user.id}/{user.avatar}.png", Headers,
                $"avatar_{user.id}", (success) =>
                {
                    onSuccess(success);
                },
                (err) =>
                {
                    onError(err);
                }
            );
        }

        // private void GetUserInfo(string userId, Action<API.UserData> onSuccess, Action<StreamError> onError)
        // {
        //     // TODO: HTTP API
        // }

        #endregion

        #region Voice Channel Select Event

        private void SubscribeToVoiceChannelSelectEvent(Action<ChannelSelectEvent> onEvent)
        {
            SendSubscription<ChannelSelectSubscriptionRequest,ChannelSelectEvent>(null, onEvent);
        }

        #endregion

        #region Text Channel Select

        /// <summary>
        /// Selects a text channel to subscribe to chat messages from.
        /// </summary>
        /// <param name="channelId">The channel to subscribe to.</param>
        /// <param name="onSuccess">The callback executed on a successful request.</param>
        /// <param name="onError">The callback executed on an unsuccessful request.</param>
        public void SetTextChannel(string channelId, Action<TextChannelSelectResponse> onSuccess, Action<ErrorResponse> onError)
        {
            SendUnsubscription<TextMessageCreateSubscriptionRequest>(TEXT_CHANNEL);
            SendUnsubscription<TextMessageDeleteSubscriptionRequest>(TEXT_CHANNEL);
            TextChannelSelectRequest request = new()
            {
                args = new()
                {
                    channel_id = channelId
                }
            };
            SendRequest<TextChannelSelectRequest, TextChannelSelectResponse>(request, (s) =>
            {
                SendSubscription<TextMessageCreateSubscriptionRequest, TextMessageEvent>(s.data.id, HandleChatMessage);
                SendSubscription<TextMessageDeleteSubscriptionRequest, TextMessageEvent>(s.data.id, HandleChatMessageDeletion);
                // TODO: handle message editing, something not available on most platforms
                // SendSubscription<TextMessageEditSubscriptionRequest, TextMessageEvent>(s.data.id, HandleChatMessage);
                onSuccess(s);
            }, 
            onError);
            TEXT_CHANNEL = channelId;
            SaveDataManager.WriteSaveData(this);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper function to execute when we join a channel ourselves
        /// </summary>
        private void HandleSelfChannelJoin()
        {
            GetVoiceChannelState((res) =>
            {
                string channelId = res.data.id;
                if(!string.IsNullOrEmpty(res.data.name)){
                    LOGGER.Log($"User has joined channel: {res.data.name}");
                }
                // Users already in the channel when we join
                foreach(VoiceStateData voiceState in res.data.voice_states)
                {
                    // Treat them all as though they suddenly joined
                    HandleUserJoin(voiceState.user);
                }
                if(!string.IsNullOrEmpty(channelId)){
                    // Subscribe to all voice channel events
                    SendSubscription<SpeakingStartSubscriptionRequest, SpeakingEvent>(channelId, HandleSpeakStartEvent);
                    SendSubscription<SpeakingStopSubscriptionRequest, SpeakingEvent>(channelId, HandleSpeakStopEvent);
                    SendSubscription<VoiceStateCreateSubscriptionRequest, VoiceStateEvent>(channelId, HandleVoiceStateCreateEvent);
                    SendSubscription<VoiceStateDeleteSubscriptionRequest, VoiceStateEvent>(channelId, HandleVoiceStateDeleteEvent);
                }
            },
            (err) =>
            {
                LOGGER.LogError(err.data.message);
            });
        }

        /// <summary>
        /// Helper function to execute when we leave a channel ourselves
        /// </summary>
        private void HandleSelfChannelLeave()
        {
            // Broadcast a User Leave/Stop Speaking event 
            // for all users in the channel when we leave
            for(int i = CURRENT_CHANNEL_USERS.Count - 1; i >= 0; i--)
            {
                HandleUserLeave(CURRENT_CHANNEL_USERS[i].id);
            }
            SendUnsubscription<SpeakingStartSubscriptionRequest>(CURRENT_VOICE_CHANNEL_ID);
            SendUnsubscription<SpeakingStopSubscriptionRequest>(CURRENT_VOICE_CHANNEL_ID);
            SendUnsubscription<VoiceStateCreateSubscriptionRequest>(CURRENT_VOICE_CHANNEL_ID);
            SendUnsubscription<VoiceStateDeleteSubscriptionRequest>(CURRENT_VOICE_CHANNEL_ID);
        }

        private void HandleSpeakStartEvent(SpeakingEvent speakingEvent)
        {
            onUserSpeakStart.Invoke(speakingEvent.data.user_id);
        }

        private void HandleVoiceStateCreateEvent(VoiceStateEvent voiceStateEvent)
        {
            HandleUserJoin(voiceStateEvent.data.user);
        }

        private void HandleUserJoin(UserData data)
        {
            StreamUser user = new(
                data.global_name,
                data.id
            );
            GetUserAvatar(data, 
            (success) =>
            {
                user.avatar = success;
                onUserJoin.Invoke(user);
            },
            (err) =>
            {
                onUserJoin.Invoke(user);
            });
            CURRENT_CHANNEL_USERS.Add(user);
        }

        private void HandleSpeakStopEvent(SpeakingEvent speakingEvent)
        {
            onUserSpeakStop.Invoke(speakingEvent.data.user_id);
        }

        private void HandleVoiceStateDeleteEvent(VoiceStateEvent voiceStateEvent)
        {
            HandleUserLeave(voiceStateEvent.data.user.id);
        }

        private void HandleUserLeave(string userId)
        {
            int index = CURRENT_CHANNEL_USERS.FindIndex(user => userId == user.id);
            if(index >= 0) {
                onUserLeave.Invoke(userId);
                onUserSpeakStop.Invoke(userId);
                CURRENT_CHANNEL_USERS.RemoveAt(index);
            }
        }

        private void HandleChatMessage(TextMessageEvent textMessageEvent)
        {
            StreamUser chatter = new(textMessageEvent.data.message.author.global_name, textMessageEvent.data.message.author.id);
            List<StreamChatMessage.Fragment> fragments = new();
            List<StreamBadge> badges = new();
            DateTime timestamp = DateTime.Now;
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () =>
                {
                    StreamChatMessage message = new(
                        textMessageEvent.data.message.id, 
                        "Discord",
                        timestamp, 
                        chatter, 
                        textMessageEvent.data.message.author_color, 
                        badges, 
                        fragments
                    );
                    onChatMessage.Invoke(message);
                }
            );
            foreach (TextMessageFragment fragment in textMessageEvent.data.message.content_parsed)
            {
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                if ("emoji".Equals(fragment.type))
                {
                    var newFragment = new StreamChatMessage.Fragment(StreamChatMessage.Fragment.Type.EMOTE, fragment.content);
                    fragments.Add(newFragment);
                    ImageHandler.GetFromRemote(
                        $"https://cdn.discordapp.com/emojis/{fragment.emojiId}.{(fragment.animated ? "gif" : "png")}",
                        new HttpUtils.HttpHeaders(), 
                        $"emoji_{fragment.emojiId}",
                        (success) =>
                        {
                            newFragment.image = success;
                            manager.ResolveDependency(taskId);
                        },
                        (err) =>
                        {
                            manager.ResolveDependency(taskId);
                        }
                    );
                }
                else
                {
                    var newFragment = new StreamChatMessage.Fragment(StreamChatMessage.Fragment.Type.TEXT, fragment.content);
                    fragments.Add(newFragment);
                    manager.ResolveDependency(taskId);
                }
            }
            string avatarTaskId = Guid.NewGuid().ToString();
            manager.AddDependency(avatarTaskId);
            GetUserAvatar(textMessageEvent.data.message.author, (avatar) =>
            {
                chatter.avatar = avatar;
                manager.ResolveDependency(avatarTaskId);
            }, (err) =>
            {
                LOGGER.LogError(err.message);
                manager.ResolveDependency(avatarTaskId);
            });
            manager.Enable(true);
        }

        private void HandleChatMessageDeletion(TextMessageEvent textMessageEvent)
        {
            StreamChatMessageDeletion deletion = new(textMessageEvent.data.message.id);
            onChatMessageDelete.Invoke(deletion);
        }

        #endregion

        #region File IO

        public override void FromSaveData(IntegrationData data)
        {
            USER_TOKEN = data.token;
            TEXT_CHANNEL = data.textChannel;
            // Once we load our settings, 
            // we can attempt to establish a connection.
            ConnectToIPC();
        }

        public override IntegrationData ToSaveData()
        {
            return new IntegrationData()
            {
                token = USER_TOKEN,
                textChannel = TEXT_CHANNEL
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
            public string textChannel;
        }

        #endregion
    }
}