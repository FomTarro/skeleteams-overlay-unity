using System;
using Skeletom.BattleStation.Integrations.Twitch;
using Skeletom.Essentials.IO;
using UnityEngine;
using UnityEngine.Events;

public class TwitchIntegration : Integration<TwitchIntegration, TwitchIntegration.IntegrationData>
{
    private const string CLIENT_ID = "x2rikvl9behn8k54flc95ulhbq265m";
    private const string USER_TOKEN_ENDPOINT = "https://id.twitch.tv/oauth2/authorize";
    private const string USER_TOKEN_REDIRECT = "https://twitchapps.com/tokengen/";
    private string USER_TOKEN = "NO_TOKEN_SET";
    private static readonly string[] USER_TOKEN_SCOPES = {
        "chat:read",
        "channel:read:redemptions",
        "channel:read:subscriptions",
        "moderator:read:followers",
    };

    public override void Initialize()
    {

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
