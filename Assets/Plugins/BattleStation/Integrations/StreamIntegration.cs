using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.Essentials.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations
{
    public abstract class StreamIntegration<T, K> : Integration<T, K> where T : StreamIntegration<T, K> where K : BaseSaveData
    {

        [Serializable]
        // CLass used to represent any kind of image that might appear with a chat message (emote, sub badge, avatar)
        public class ChatImage : AnimatedTextureDisplay.AnimatedTexture
        {
            public string name;

            public ChatImage(string name, IEnumerable<Frame> frames) : base(frames)
            {
                this.name = name;
            }

            public ChatImage(string name, Texture image) : base(new List<Frame>() { new Frame(image, -1) })
            {
                this.name = name;
            }
        }

        [Serializable]
        public class ChatUser
        {
            public string displayName;
            public string id;
            public ChatImage avatar;
        }

        [Serializable]
        public class ChatMessage
        {
            public struct Fragment
            {
                public enum Type
                {
                    TEXT = 0,
                    EMOTE = 1,
                }

                public string text;
                public ChatImage image;
                public Type type;

                public Fragment(string text, ChatImage image, Type type)
                {
                    this.text = text;
                    this.image = image;
                    this.type = type;
                }
            }

            public ChatUser chatter;
            public List<Fragment> fragments = new List<Fragment>();
            public ChatMessage(ChatUser chatter, IEnumerable<Fragment> fragments)
            {
                this.chatter = chatter;
                this.fragments = new List<Fragment>(fragments);
            }
        }

        protected Dictionary<string, ChatImage> _imageCache = new Dictionary<string, ChatImage>();

        [Serializable]
        public class ChatMessageEvent : UnityEvent<ChatMessage> { }
        public ChatMessageEvent onChatMessage = new ChatMessageEvent();
    }
}