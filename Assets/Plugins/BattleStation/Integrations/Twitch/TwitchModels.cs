using System;

namespace Skeletom.BattleStation.Integrations.Twitch
{
    [Serializable]
    public class EventSubSocketMessage<T>
    {
        public EventSubMetadata metadata;
        public T payload;
    }

    [Serializable]
    public class EventSubMetadata
    {
        public string message_id;
        public string message_type;
        public string message_timestamp;
    }


    [Serializable]
    public class EventSubSubscriptionInfo
    {
        public string id;
        public string status;
        public string type;
    }

    [Serializable]
    public class EventSubEvent<T>
    {
        public EventSubSubscriptionInfo subscription;
        public T @event;
    }

    [Serializable]
    public class ChatMessage
    {
        public string text;
        public ChatMessageFragment[] fragments;
    }

    [Serializable]
    public class ChatMessageFragment
    {
        public string type;
        public string text;
        public string cheermote;
        public string emote;
        public string mention;
    }

    [Serializable]
    public class ChatMessageBadge
    {
        public string set_id;
        public string id;
        public string info;
    }

    [Serializable]
    public class ChatMessageEventSubEvent
    {
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string chatter_user_id;
        public string chatter_user_login;
        public string chatter_user_name;
        public string message_id;
        public ChatMessage message;
        public string color;
    }
}
