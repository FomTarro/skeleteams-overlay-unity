using System;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Twitch.EventSub
{
    #region General Subscription

    public interface IEventSubscriptionRequest
    {
        public string GetSubscriptionType();
    }

    [Serializable]
    public class EventSubSubscriptionRequest<T> : IEventSubscriptionRequest where T : ICondition
    {
        public string type;
        public string version = "1";
        public T condition;
        public Transport transport;

        public EventSubSubscriptionRequest(string sessionId)
        {
            transport = new Transport()
            {
                session_id = sessionId
            };
        }

        public string GetSubscriptionType()
        {
            return this.type;
        }

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    public interface ICondition { }

    [Serializable]
    public class Transport
    {
        public string method = "websocket";
        public string session_id;
    }

    [Serializable]
    public class SubscriptionResponse : DataResponse<SubscriptionData>
    {
        public int max_total_cost;
        public int total;
        public int total_cost;
    }

    [Serializable]
    public class SubscriptionData
    {
        // TODO: not sure how to easily get this back out;
        // public T condition;
        public Transport transport;
        public int cost;
        public string created_at;
        public string status;
        public string type;
        public string version;
        public string id;
    }

    #endregion

    #region Generic Events

    public class EventMessage<T>
    {
        public Metadata metadata;
        public T payload;

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public class Metadata
    {
        public string message_id;
        public string message_type;
        public string message_timestamp;
    }

    [Serializable]
    public class EventPayload<T>
    {
        public SubscriptionInfo subscription;
        public T @event;
    }

    [Serializable]
    public class SubscriptionInfo
    {
        public string id;
        public string status;
        public string type;
    }

    public interface IEventSubEvent { }

    #endregion

    #region Welcome Event

    [Serializable]
    public class WelcomePayload
    {
        public Session session;
    }

    [Serializable]
    public class Session
    {
        public string id;
        public string status;
        public string connected_at;
        public int keepalive_timeout_seconds;
        public string reconnect_url;
    }

    #endregion

    #region Chat Message Event

    [Serializable]
    public class ChatMessageSubscriptionRequest : EventSubSubscriptionRequest<ChatMessageEventCondition>
    {
        public ChatMessageSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.chat.message";
        }
    }

    [Serializable]
    public class ChatMessageEventCondition : ICondition
    {
        public string broadcaster_user_id;
        public string user_id;
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
        public ChatMessageFragmentEmote emote;
        public string mention;
    }

    [Serializable]
    public class ChatMessageFragmentEmote
    {
        public string emote_set_id;
        public string[] format = new string[0];
        public string id;
        public string owner_id;
    }

    [Serializable]
    public class ChatMessageBadge
    {
        public string set_id;
        public string id;
        public string info;
    }

    [Serializable]
    public class ChatMessageEvent : IEventSubEvent
    {
        public ChatMessageBadge[] badges = new ChatMessageBadge[0];
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

    #endregion
}