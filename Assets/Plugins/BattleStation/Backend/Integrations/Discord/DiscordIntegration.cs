using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Skeletom.BattleStation.Integrations.Discord.IPC;
using Skeletom.Essentials.Collections;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations.Discord
{
    public class DiscordIntegration : Integration<DiscordIntegration, DiscordIntegration.IntegrationData>
    {
        public override string FileName => "discord.json";

        private const string CLIENT_ID = "1524610169516986429";
        private string USER_TOKEN = "NO_TOKEN_SET";
        public string SelfUserId { get; private set; } = "NO_ID_SET";

        // This allows us to swap out endpoints for mock ones against a testing engine
        private readonly IDiscordEndpoints DISCORD_API = new DiscordAPI();
        
        private IpcSocket _socket;
        private const int REQUEST_TIMEOUT_MS = 4000;
        private readonly Dictionary<string, Action<string>> REQUEST_RESPONSE_HANDLERS = new();
        private readonly Dictionary<string, Action<ErrorResponse>> REQUEST_RESPONSE_ERROR_HANDLERS = new();
        private readonly Dictionary<string, float> REQUEST_RESPONSE_TIMEOUTS = new();
        private readonly Dictionary<string, Action<string>> EVENT_SUBSCRIPTION_HANDLERS = new();

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

        // private readonly LRUDictionary<string, StreamUser> USER_CACHE = new(10, (user) => {
        //     foreach (var frame in user.avatar?.frames)
        //     {
        //         Destroy(frame.image);
        //     }
        // });

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
            FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        }

        public override void Disable()
        {
            _socket?.Disconnect();
        }

        public override void Initialize()
        {
            Enable();
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
                List<string> nonces = new(REQUEST_RESPONSE_TIMEOUTS.Keys);
                foreach(string nonce in nonces)
                {
                    REQUEST_RESPONSE_TIMEOUTS[nonce] -= (1000f*Time.deltaTime);
                    if(REQUEST_RESPONSE_TIMEOUTS[nonce] < 0f)
                    {
                        ErrorResponse err = new()
                        {
                            data = new()
                            {
                                code = 0,
                                message = "Request timeout."
                            }
                        };
                        REQUEST_RESPONSE_ERROR_HANDLERS[nonce](err);

                        REQUEST_RESPONSE_TIMEOUTS.Remove(nonce);
                        REQUEST_RESPONSE_ERROR_HANDLERS.Remove(nonce); 
                        REQUEST_RESPONSE_HANDLERS.Remove(nonce);
                    }
                }
            }
        }

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
                if(!string.IsNullOrEmpty(header.nonce) && REQUEST_RESPONSE_HANDLERS.ContainsKey(header.nonce))
                {
                    if (!string.IsNullOrEmpty(header.evt) && "ERROR".Equals(header.evt))
                    {
                        ErrorResponse err = JsonUtility.FromJson<ErrorResponse>(data);
                        REQUEST_RESPONSE_ERROR_HANDLERS[header.nonce](err); 
                    }
                    else
                    {
                        REQUEST_RESPONSE_HANDLERS[header.nonce](data); 
                    }
                    REQUEST_RESPONSE_TIMEOUTS.Remove(header.nonce);
                    REQUEST_RESPONSE_ERROR_HANDLERS.Remove(header.nonce); 
                    REQUEST_RESPONSE_HANDLERS.Remove(header.nonce);  
                }
            }
        }

        private void ConnectToIPC()
        {
            _socket.Connect(() =>
            {
                Handshake((s) =>
                {
                    Debug.Log($"Discord IPC Handshake complete for user: {s.data.user.global_name}");
                    SelfUserId = s.data.user.id;
                    ValidateToken(USER_TOKEN, 
                    (s) =>
                    {
                        Debug.Log($"Discord IPC Authentication successful with existing token!");
                    },
                    (err) =>
                    {
                        RequestToken(
                        (s) =>
                        {
                            Debug.Log($"Discord IPC Authentication successful with new token!");
                        },
                        (err) =>
                        {
                            Debug.LogError($"Discord IPC unable to authenticate: {err.data.message}");
                        });
                    });
                },
                (err) =>
                {
                    Debug.LogError($"Discord IPC Handshake failed with error: {err.data.message}");
                });
            },
            () =>
            {
                Debug.LogWarning("Discord IPC disconnected!");
            },
            (e) =>
            {
                Debug.LogError("Discord IPC Error: " + e);
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
            string nonce = Guid.NewGuid().ToString();
            request.nonce = nonce;
            REQUEST_RESPONSE_HANDLERS[nonce] = (str) =>
            {
                O response = JsonUtility.FromJson<O>(str);
                onSuccess(response);
            };
            REQUEST_RESPONSE_ERROR_HANDLERS[nonce] = (err) =>
            {
                onError(err);
            };
            REQUEST_RESPONSE_TIMEOUTS[nonce] = REQUEST_TIMEOUT_MS;
            Send(1, JsonUtility.ToJson(request));
        }

        private void SendSubscription<I, O>(string channelId, Action<O> onEvent) where I: EventSubscriptionRequest, new() where O : IpcMessage, IDataResponse
        {
            string nonce = Guid.NewGuid().ToString();
            I request = new()
            {
                nonce = nonce,
                args = new(){ channel_id = channelId }
            };
            EVENT_SUBSCRIPTION_HANDLERS[request.evt] = (str) =>
            {
                O response = JsonUtility.FromJson<O>(str);
                onEvent(response);
            };
            SendRequest<I, IpcMessage>(request, (s) => { }, (err) => { });
        }

        private void SendUnsubscription<I>(string channelId) where I : EventSubscriptionRequest, new()
        {
            string nonce = Guid.NewGuid().ToString();
            I request = new()
            {
                cmd = "UNSUBSCRIBE",
                nonce = nonce,
                args = new(){ channel_id = channelId }
            };
            EVENT_SUBSCRIPTION_HANDLERS.Remove(request.evt);
            SendRequest<I, IpcMessage>(request, (s) => { }, (err) => { });
        }

        #endregion

        #region Handshake

        public void Handshake(Action<HandshakeResponse> onSuccess, Action<ErrorResponse> onError)
        {
            const string eventCode = "READY";
            EVENT_SUBSCRIPTION_HANDLERS[eventCode] = (str) =>
            {
                REQUEST_RESPONSE_TIMEOUTS.Remove(eventCode);
                onSuccess(JsonUtility.FromJson<HandshakeResponse>(str)); 
            };
            REQUEST_RESPONSE_ERROR_HANDLERS[eventCode] = (err) => {
                ErrorResponseData data = JsonUtility.FromJson<ErrorResponseData>(LAST_MESSAGE_RECEIVED);
                err.data.code = data.code;
                err.data.message = data.message;
                onError(err);
            };
            REQUEST_RESPONSE_TIMEOUTS[eventCode] = REQUEST_TIMEOUT_MS;
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
            SaveDataManager.Instance.WriteSaveData(this);
            // Check if we're currently in a voice channel
            OnChannelJoin(null);
            // Begin event subscriptions
            SubscribeToVoiceChannelSelectEvent(
            (res) =>
            {
                if(!string.IsNullOrEmpty(res.data.channel_id))
                {
                    // if the channel changed, unsub from old channel and sub to new channel
                    string newChannel = res.data.channel_id;
                    Debug.Log($"User has joined {newChannel}");
                    if(newChannel != CURRENT_VOICE_CHANNEL_ID)
                    {
                        OnChannelLeave();
                    }
                    // When we join a new channel, broadcast its current state and subscribe to new events
                    OnChannelJoin(newChannel);
                }
                else
                {
                    Debug.Log("User has left the channel");
                    // Unsub from old channel
                    OnChannelLeave();
                }
                CURRENT_VOICE_CHANNEL_ID = res.data.channel_id;
            });
        }

        private void RequestToken(Action<AuthenticateResponse> onSuccess, Action<ErrorResponse> onError)
        {
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);
            AuthorizeRequest request = new()
            {
                args = new AuthorizeRequestArgs
                {
                    client_id = CLIENT_ID,
                    scopes = new[] { "rpc", "rpc.voice.read", "identify" },
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

        // PKCE helpers
        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string GenerateCodeChallenge(string verifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
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

        private void GetUserInfo(string userId, Action<API.UserData> onSuccess, Action<StreamError> onError)
        {
            // TODO: HTTP API
        }

        #endregion

        #region Voice Channel Select Event

        private void SubscribeToVoiceChannelSelectEvent(Action<ChannelSelectEvent> onEvent)
        {
            SendSubscription<ChannelSelectSubscriptionRequest,ChannelSelectEvent>(null, onEvent);
        }

        #endregion

        #region Helper Methods

        private void OnChannelJoin(string channelId)
        {
            GetVoiceChannelState((res) =>
            {
                string channelId = res.data.id;
                if(!string.IsNullOrEmpty(res.data.name)){
                    Debug.Log($"User has joined channel: {res.data.name}");
                }
                // Users already in the channel when we join
                foreach(VoiceStateData voiceState in res.data.voice_states)
                {
                    HandleUserJoin(voiceState.user);
                }
                // Subscribe to all voice channel events
                SendSubscription<SpeakingStartSubscriptionRequest, SpeakingEvent>(channelId, HandleSpeakStartEvent);
                SendSubscription<SpeakingStopSubscriptionRequest, SpeakingEvent>(channelId, HandleSpeakStopEvent);
                SendSubscription<VoiceStateCreateSubscriptionRequest, VoiceStateEvent>(channelId, HandleVoiceStateCreateEvent);
                SendSubscription<VoiceStateDeleteSubscriptionRequest, VoiceStateEvent>(channelId, HandleVoiceStateDeleteEvent);
            },
            (err) =>
            {
                
            });
        }

        private void OnChannelLeave()
        {
            // Broadcast a User Leave/Stop Speaking event 
            // for all users in the channel when we leave
            // TODO: this should probably be a separate event for self channel join/leave
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
            GetUserAvatar(data, (success) =>
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

        #endregion

        #region File IO

        public override void FromSaveData(IntegrationData data)
        {
            USER_TOKEN = data.token;
            // Once we load our settings, 
            // we can attempt to establish a connection.
            ConnectToIPC();
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