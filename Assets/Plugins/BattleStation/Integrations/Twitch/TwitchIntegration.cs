using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string VALIDATE_ENDPOINT = "https://id.twitch.tv/oauth2/validate";

        private static readonly string[] USER_TOKEN_SCOPES = {
            "chat:read",
            "channel:read:redemptions",
            "channel:read:subscriptions",
            "moderator:read:followers",
        };

        [SerializeField]
        private WebServer _webServer;
        [SerializeField]
        private TextAsset _tokenRedirectPage;

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
            + "&state=" + new Guid().ToString()
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
                // GetUserInfo(new List<string> { "skeletom_ch", "henemimi" }, (users) =>
                // {
                //     foreach (UserData user in users)
                //     {
                //         Debug.Log(user.created_at);
                //     }
                // },
                // (err) =>
                // {
                //     Debug.LogError(err);
                // });
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
                        onValidation(true);
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
                    {"Client-ID", CLIENT_ID}
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


        [Serializable]
        public class TwitchChatMessageEvent : UnityEvent<EventSub.EventSubChatMessageEvent> { }
        public TwitchChatMessageEvent onTwitchChatMessage = new TwitchChatMessageEvent();

        private void Update()
        {

        }

        private void ProcessMessage(string msg)
        {
            try
            {
                EventSub.EventSubSocketMessage<EventSub.EventSubEventPayload<string>> message = JsonUtility.FromJson<EventSub.EventSubSocketMessage<EventSub.EventSubEventPayload<string>>>(msg);
                if ("session_welcome".Equals(message.metadata.message_type))
                {
                    EventSub.EventSubSocketMessage<EventSub.EventSubWelcomePayload> session = JsonUtility.FromJson<EventSub.EventSubSocketMessage<EventSub.EventSubWelcomePayload>>(msg);
                    string sessionId = session.payload.session.id;
                    // TODO: kick off all HTTP subscriptions
                    HttpUtils.HttpHeaders headers = new HttpUtils.HttpHeaders()
                    {
                        authorization = USER_TOKEN,
                        customHeaders = new Dictionary<string, string>()
                        {
                            {"Client-ID", BROADCASTER_ID}
                        }
                    };
                    StartCoroutine(HttpUtils.PostRequest(
                        EVENTSUB_SUBSCRIPTION_ENDPOINT,
                        JsonUtility.ToJson(new EventSub.EventSubChatMessageSubscriptionRequest(sessionId)
                        {
                            condition = {
                                broadcaster_user_id=BROADCASTER_ID,
                                user_id=BROADCASTER_ID
                            }
                        }),
                        headers,
                        (success) =>
                        {

                        },
                        (err) =>
                        {

                        })
                    );

                }
                else if ("notification".Equals(message.metadata.message_type))
                {
                    if ("channel.follow".Equals(message.payload.subscription.type))
                    {
                        EventSub.EventSubSocketMessage<EventSub.EventSubEventPayload<EventSub.EventSubChatMessageEvent>> obj =
                        JsonUtility.FromJson<EventSub.EventSubSocketMessage<EventSub.EventSubEventPayload<EventSub.EventSubChatMessageEvent>>>(msg);
                        onTwitchChatMessage.Invoke(obj.payload.@event);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }


        public override void FromSaveData(IntegrationData data)
        {
            SetToken(data.token);
            ValidateToken(USER_TOKEN, (isValid) =>
            {
                if (!isValid)
                {
                    RequestToken();
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
