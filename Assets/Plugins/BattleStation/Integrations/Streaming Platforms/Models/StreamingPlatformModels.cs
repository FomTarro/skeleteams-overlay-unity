using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    [Serializable]
    public class StreamingPlatformError
    {
        public string message;
    }

    /// <summary>
    /// Class used to represent any kind of image that might appear with a chat message (emote, sub badge, avatar)
    /// </summary>
    [Serializable]
    public class StreamingPlatformImage : AnimatedTextureDisplay.AnimatedTexture
    {
        public string name;

        public StreamingPlatformImage(string name, IEnumerable<Frame> frames) : base(frames)
        {
            this.name = name;
        }

        public StreamingPlatformImage(string name, Texture image) : base(new List<Frame>() { new Frame(image, -1) })
        {
            this.name = name;
        }
    }

    [Serializable]
    public class StreamingPlatformChatUser
    {
        public string displayName;
        public Color displayColor;
        public string id;
        public StreamingPlatformImage avatar;
        public List<StreamingPlatformBadge> badges = new List<StreamingPlatformBadge>();

        public StreamingPlatformChatUser(string displayName, string displayColorHex, string id)
        {
            this.displayName = displayName;
            ColorUtility.TryParseHtmlString(displayColorHex, out this.displayColor);
            this.id = id;
        }
    }

    [Serializable]
    public class StreamingPlatformBadge
    {
        public StreamingPlatformImage image;
        public string displayName;
        public string id;

        public StreamingPlatformBadge(string displayName, string id)
        {
            this.displayName = displayName;
            this.id = id;
        }
    }

    [Serializable]
    public class StreamingPlatformChatMessage
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
            public StreamingPlatformImage image;
            public Type type;

            public Fragment(Type type, string text, StreamingPlatformImage image = null)
            {
                this.text = text;
                this.image = image;
                this.type = type;
            }
        }

        public StreamingPlatformChatUser chatter;
        public List<Fragment> fragments = new List<Fragment>();
        public StreamingPlatformChatMessage(StreamingPlatformChatUser chatter, IEnumerable<Fragment> fragments)
        {
            this.chatter = chatter;
            this.fragments = new List<Fragment>(fragments);
        }
    }
}
