using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.Essentials.Utils;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    [Serializable]
    public class StreamError
    {
        [Serializable]
        public enum ErrorCode { RemoteError, ApplicationError }
        public ErrorCode errorCode;
        public string message;

        public StreamError(ErrorCode errorCode, string message)
        {
            this.errorCode = errorCode;
            this.message = message;
        }

        public StreamError(HttpUtils.HttpError error)
        {
            errorCode = ErrorCode.RemoteError;
            message = error.message;
        }

        public override string ToString()
        {
            return errorCode + ": " + message;
        }
    }

    /// <summary>
    /// Class used to represent any kind of image that might appear in an a stream platform (emote, sub badge, avatar)
    /// </summary>
    [Serializable]
    public class StreamImage : AnimatedTextureDisplay.AnimatedTexture
    {
        public string name;

        public StreamImage(string name, IEnumerable<Frame> frames) : base(frames)
        {
            this.name = name;
        }

        public StreamImage(string name, Texture image) : base(new List<Frame>() { new Frame(image, -1) })
        {
            this.name = name;
        }
    }

    [SerializeField]
    public class StreamInfo
    {
        public string title;
        public string language;
        public string categoryId;
        public string categoryName;
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class StreamUser
    {
        public string displayName;
        public string id;
        public StreamImage avatar;

        public StreamUser(string displayName, string id)
        {
            this.displayName = displayName;
            this.id = id;
            // ColorUtility.TryParseHtmlString(displayColorHex, out this.displayColor);
        }
    }

    [Serializable]
    public class StreamBadge
    {
        public StreamImage image;
        public string displayName;
        public string id;

        public StreamBadge(string displayName, string id)
        {
            this.displayName = displayName;
            this.id = id;
        }
    }

    [Serializable]
    public class StreamChatMessage : IDisplayableData
    {
        [Serializable]
        public class Fragment
        {
            [Serializable]
            public enum Type
            {
                TEXT = 0,
                EMOTE = 1,
            }

            public string text;
            public StreamImage image;
            public Type type;

            public Fragment(Type type, string text, StreamImage image = null)
            {
                this.text = text;
                this.image = image;
                this.type = type;
            }
        }

        public string id;
        public string ID { get { return id; } }
        public DateTime timestamp;
        public StreamUser chatter;
        public Color nameColor = Color.white;
        public List<StreamBadge> badges = new();
        public List<Fragment> message = new();
        public StreamChatMessage(string id, DateTime timestamp, StreamUser chatter, string nameColorHexCode, ICollection<StreamBadge> badges, ICollection<Fragment> message)
        {
            this.id = id;
            this.timestamp = timestamp;
            this.chatter = chatter;
            this.message = new List<Fragment>(message);
            this.badges = new List<StreamBadge>(badges);
            ColorUtility.TryParseHtmlString(nameColorHexCode, out this.nameColor);
        }
    }

    [Serializable]
    public class StreamChatMessageDeletion
    {
        public string id;
        public StreamChatMessageDeletion(string id)
        {
            this.id = id;
        }
    }

    [Serializable]
    public struct StreamEvent : IDisplayableData
    {
        public string ID { get; private set; }
        public StreamUser user;
        public string description;
        public string details;
        public StreamEvent(StreamUser user, string description, string details)
        {
            this.ID = Guid.NewGuid().ToString(); ;
            this.user = user;
            this.description = description;
            this.details = details;
        }
    }

    [Serializable]
    public class StreamChatRedeem
    {
        public string name;
        public string id;
        public int cost;
        public StreamUser redeemer;

        public StreamChatRedeem(StreamUser redeemer, string name, string id, int cost)
        {
            this.redeemer = redeemer;
            this.name = name;
            this.id = id;
            this.cost = cost;
        }
    }

    [Serializable]
    public class StreamPaidChatMessage
    {
        // TODO, what happens if the message has emotes?
        public int amount;
        public string currency;
    }

    [SerializeField]
    public class StreamChannelFollow
    {
        public DateTime timestamp;
        public StreamUser follower;

        public StreamChannelFollow(StreamUser follower, string timestamp)
        {
            this.follower = follower;
            try
            {
                DateTime.TryParse(timestamp, out this.timestamp);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
        }
    }

    [SerializeField]
    public class StreamChannelPaidSubscription
    {
        public StreamUser subscriber;
        public bool isGifted;
        public string tier;
        public int streak;

        public StreamChannelPaidSubscription(StreamUser subscriber)
        {
            this.subscriber = subscriber;
        }
    }

    [SerializeField]
    public class StreamRaid
    {
        public StreamUser raider;
        public int viewers;

        public StreamRaid(StreamUser raider, int viewers)
        {
            this.raider = raider;
            this.viewers = viewers;
        }
    }
}
