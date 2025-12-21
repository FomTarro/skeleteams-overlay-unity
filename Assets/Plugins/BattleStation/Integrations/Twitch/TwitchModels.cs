using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations.Twitch
{

    #region HTTP

    [Serializable]
    public class DataResponse<T>
    {
        public List<T> data;
    }

    [Serializable]
    public class UserData
    {
        public string id;
        public string login;
        public string display_name;
        public string type;
        public string broadcaster_type;
        public string description;
        public string profile_image_url;
        public string offline_image_url;
        public int view_count;
        public string email;
        public string created_at;
    }

    namespace EventSub
    {
        [Serializable]
        public class EventSubSubscriptionRequest<T>
        {
            public string type;
            public string version = "1";
            public T condition;
            public EventSubSubscriptionTransport transport;

            public EventSubSubscriptionRequest(string sessionId)
            {
                transport = new EventSubSubscriptionTransport()
                {
                    session_id = sessionId
                };
            }
        }

        [Serializable]
        public class EventSubSubscriptionTransport
        {
            public string method = "websocket";
            public string session_id;
        }


        [Serializable]
        public class EventSubSubscriptionResponse
        {
            // TODO
        }


        public class EventSubChatMessageSubscriptionRequest : EventSubSubscriptionRequest<EventSubChatMessageCondition>
        {
            public EventSubChatMessageSubscriptionRequest(string sessionId) : base(sessionId)
            {
                type = "channel.chat.message";
            }
        }

        [Serializable]
        public class EventSubChatMessageCondition
        {
            public string broadcaster_user_id;
            public string user_id;
        }

        #endregion

        #region WebSocket

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
        public class EventSubWelcomePayload
        {
            public EventSubSession session;
        }

        [Serializable]
        public class EventSubSession
        {
            public string id;
            public string status;
            public string connected_at;
            public int keepalive_timeout_seconds;
            public string reconnect_url;
        }

        [Serializable]
        public class EventSubEventPayload<T>
        {
            public EventSubSubscriptionInfo subscription;
            public T @event;
        }

        [Serializable]
        public class EventSubSubscriptionInfo
        {
            public string id;
            public string status;
            public string type;
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
        public class EventSubChatMessageEvent
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
    #endregion
}
