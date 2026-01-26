using System;
using System.Collections.Generic;
using JetBrains.Annotations;
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

    [Serializable]
    public class StreamChatUser
    {
        public string displayName;
        public string id;
        public Color displayColor;
        public StreamImage avatar;
        public List<StreamBadge> badges = new List<StreamBadge>();

        public StreamChatUser(string displayName, string id, string displayColorHex)
        {
            this.displayName = displayName;
            this.id = id;
            ColorUtility.TryParseHtmlString(displayColorHex, out this.displayColor);
        }

        public StreamChatUser(string displayName, string id)
        {
            this.displayName = displayName;
            this.id = id;
            this.displayColor = Color.white;
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
    public class StreamChatMessage
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
        public StreamChatUser chatter;
        public List<Fragment> fragments = new List<Fragment>();
        public StreamChatMessage(string id, StreamChatUser chatter, ICollection<Fragment> fragments)
        {
            this.id = id;
            this.chatter = chatter;
            this.fragments = new List<Fragment>(fragments);
        }
    }

    [Serializable]
    public class StreamChatRedeem
    {
        public string name;
        public string id;
        public int cost;
        public StreamChatUser redeemer;

        public StreamChatRedeem(StreamChatUser redeemer, string name, string id, int cost)
        {
            this.redeemer = redeemer;
            this.name = name;
            this.id = id;
            this.cost = cost;
        }
    }
}
