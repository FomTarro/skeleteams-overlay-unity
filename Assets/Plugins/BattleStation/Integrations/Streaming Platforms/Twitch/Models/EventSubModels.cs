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
    public class EventSubscriptionRequest<T> : IEventSubscriptionRequest where T : ICondition
    {
        public string type;
        public string version = "1";
        public T condition;
        public Transport transport;

        public EventSubscriptionRequest(string sessionId)
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

    public interface IEventSubEvent
    {
        public string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

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
    public class ChatMessageSubscriptionRequest : EventSubscriptionRequest<ChatMessageEventCondition>
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

    #region Chat Message Delete Event

    [Serializable]
    public class ChatMessageDeletionSubscriptionRequest : EventSubscriptionRequest<ChatMessageDeletionEventCondition>
    {
        public ChatMessageDeletionSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.chat.message_delete";
        }
    }

    [Serializable]
    public class ChatMessageDeletionEventCondition : ICondition
    {
        public string broadcaster_user_id;
        public string user_id;
    }

    [Serializable]
    public class ChatMessageDeletionEvent : IEventSubEvent
    {
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string target_user_id;
        public string target_user_login;
        public string target_user_name;
        public string message_id;
    }

    #endregion

    #region Channel Point Redeem Event 

    [Serializable]
    public class ChannelPointRedeemSubscriptionRequest : EventSubscriptionRequest<ChannelPointRedeemEventCondition>
    {
        public ChannelPointRedeemSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.channel_points_custom_reward_redemption.add";
        }
    }

    [Serializable]
    public class ChannelPointRedeemEventCondition : ICondition
    {
        public string broadcaster_user_id;
        public string reward_id;
    }

    [Serializable]
    public class ChannelPointRedeemReward
    {
        public string id;
        public string title;
        public int cost;
        public string prompt;
    }

    [Serializable]
    public class ChannelPointRedeemEvent : IEventSubEvent
    {
        public string id;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string user_id;
        public string user_login;
        public string user_name;
        public string user_input;
        public string status;
        public ChannelPointRedeemReward reward;
        public string redeemed_at;
    }

    #endregion

    #region Channel Update Event

    [Serializable]
    public class ChannelUpdateSubscriptionRequest : EventSubscriptionRequest<ChannelUpdateEventCondition>
    {
        public ChannelUpdateSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.update";
            version = "2";
        }
    }

    [Serializable]
    public class ChannelUpdateEventCondition : ICondition
    {
        public string broadcaster_user_id;
    }

    [Serializable]
    public class ChannelUpdateEvent : IEventSubEvent
    {
        public string broadcaster_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string title;
        public string language;
        public string category_id;
        public string category_name;
        public string[] content_classification_labels;
    }

    #endregion

    #region Channel Follow Event

    [Serializable]
    public class ChannelFollowSubscriptionRequest : EventSubscriptionRequest<ChannelFollowEventCondition>
    {
        public ChannelFollowSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.follow";
            version = "2";
        }
    }

    [Serializable]
    public class ChannelFollowEventCondition : ICondition
    {
        public string broadcaster_user_id;
        public string moderator_user_id;
    }

    [SerializeField]
    public class ChannelFollowEvent : IEventSubEvent
    {
        public string user_id;
        public string user_login;
        public string user_name;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string followed_at;
    }

    #endregion

    #region Channel Subscription New Event

    [Serializable]
    public class ChannelSubNewSubscriptionRequest : EventSubscriptionRequest<ChannelSubNewEventCondition>
    {
        public ChannelSubNewSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.subscribe";
            version = "1";
        }
    }

    [Serializable]
    public class ChannelSubNewEventCondition : ICondition
    {
        public string broadcaster_user_id;
    }

    [SerializeField]
    public class ChannelSubNewEvent : IEventSubEvent
    {
        public string user_id;
        public string user_login;
        public string user_name;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string tier;
        public bool is_gift;
    }

    #endregion

    #region Channel Subscription Gift Event

    [Serializable]
    public class ChannelSubGiftSubscriptionRequest : EventSubscriptionRequest<ChannelSubGiftEventCondition>
    {
        public ChannelSubGiftSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.subscription.gift";
            version = "1";
        }
    }

    [Serializable]
    public class ChannelSubGiftEventCondition : ICondition
    {
        public string broadcaster_user_id;
    }

    [SerializeField]
    public class ChannelSubGiftEvent : IEventSubEvent
    {
        public string user_id;
        public string user_login;
        public string user_name;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public int total;
        public string tier;
        public int cumulative_total;
        public bool is_anonymous;
    }

    #endregion

    #region Channel Subscription Renewal Event

    [Serializable]
    public class ChannelSubRenewalSubscriptionRequest : EventSubscriptionRequest<ChannelSubRenewalEventCondition>
    {
        public ChannelSubRenewalSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.subscription.message";
            version = "1";
        }
    }

    [Serializable]
    public class ChannelSubRenewalEventCondition : ICondition
    {
        public string broadcaster_user_id;
    }

    [SerializeField]
    public class ChannelSubRenewalEvent : IEventSubEvent
    {
        public string user_id;
        public string user_login;
        public string user_name;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        // TODO: Yet another way to represent a chat message, how fun
        // "message": {
        //     "text": "Love the stream! FevziGG",
        //     "emotes": [
        //         {
        //             "begin": 23,
        //             "end": 30,
        //             "id": "302976485"
        //         }
        //     ]
        // },
        public int cumulative_months;
        public int streak_months;
        public int duration_months;
    }

    #endregion

    #region Channel Raid Event

    [Serializable]
    public class ChannelRaidSubscriptionRequest : EventSubscriptionRequest<ChannelRaidEventCondition>
    {
        public ChannelRaidSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.raid";
            version = "1";
        }
    }

    [Serializable]
    public class ChannelRaidEventCondition : ICondition
    {
        public string to_broadcaster_user_id;
    }

    [SerializeField]
    public class ChannelRaidEvent : IEventSubEvent
    {
        public string from_broadcaster_user_id;
        public string from_broadcaster_user_login;
        public string from_broadcaster_user_name;
        public string to_broadcaster_user_id;
        public string to_broadcaster_user_login;
        public string to_broadcaster_user_name;
        public int viewers;
    }

    #endregion

    #region Channel Cheer Event

    [Serializable]
     public class ChannelCheerSubscriptionRequest : EventSubscriptionRequest<ChannelCheerEventCondition>
    {
        public ChannelCheerSubscriptionRequest(string sessionId) : base(sessionId)
        {
            type = "channel.cheer";
        }
    }

    [Serializable]
    public class ChannelCheerEventCondition : ICondition
    {
        public string broadcaster_user_id;
    }

    [SerializeField]
    public class ChannelCheerEvent : IEventSubEvent
    {
        public bool is_anonymous;
        public string user_id;
        public string user_login;
        public string user_name;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string message;
        public int bits;
    }

    #endregion
}

