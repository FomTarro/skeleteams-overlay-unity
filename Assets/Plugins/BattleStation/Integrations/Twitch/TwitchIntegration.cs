using System;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    public class TwitchIntegration : Integration<TwitchIntegration, TwitchIntegration.IntegrationData>
    {
        private const string CLIENT_ID = "x2rikvl9behn8k54flc95ulhbq265m";
        private const string USER_TOKEN_ENDPOINT = "https://id.twitch.tv/oauth2/authorize";
        private const string USER_TOKEN_REDIRECT = "http://localhost:61616/twitch/oauth2";
        private string USER_TOKEN = "NO_TOKEN_SET";
        private string BROADCASTER_ID = "NO_ID_SET";

        private const string USERS_ENDPOINT = "https://api.twitch.tv/helix/users";

        private static readonly string[] USER_TOKEN_SCOPES = {
        "chat:read",
        "channel:read:redemptions",
        "channel:read:subscriptions",
        "moderator:read:followers",
    };

        public override void Initialize()
        {
            RequestToken();
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
            SaveDataManager.Instance.WriteSaveData(this);
        }

        public void GetUserInfo(Action onSuccess, Action<HttpUtils.HttpError> onError)
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
                HttpUtils.GetRequest(USERS_ENDPOINT, headers,
                    (str) =>
                    {

                    },
                    (err) =>
                    {

                    }
                )
            );
        }

        [Serializable]
        public class TwitchChatMessageEvent : UnityEvent<ChatMessageEventSubEvent> { }
        public TwitchChatMessageEvent onTwitchChatMessage = new TwitchChatMessageEvent();

        private void Update()
        {

        }

        private void ProcessMessage(string msg)
        {
            try
            {
                EventSubSocketMessage<EventSubEventPayload<string>> message = JsonUtility.FromJson<EventSubSocketMessage<EventSubEventPayload<string>>>(msg);
                if ("session_welcome".Equals(message.metadata.message_type))
                {
                    EventSubSocketMessage<EventSubWelcomePayload> session = JsonUtility.FromJson<EventSubSocketMessage<EventSubWelcomePayload>>(msg);
                    string sessionId = session.payload.session.id;
                    // TODO: kick off all HTTP subscriptions
                }
                else if ("notification".Equals(message.metadata.message_type))
                {
                    if ("channel.follow".Equals(message.payload.subscription.type))
                    {
                        EventSubSocketMessage<EventSubEventPayload<ChatMessageEventSubEvent>> obj = JsonUtility.FromJson<EventSubSocketMessage<EventSubEventPayload<ChatMessageEventSubEvent>>>(msg);
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
            USER_TOKEN = data.token;
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
