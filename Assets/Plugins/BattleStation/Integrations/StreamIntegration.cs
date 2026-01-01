using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations
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
        public Color displayColor;
        public string id;
        public ChatImage avatar;
    }

    [Serializable]
    public class ChatMessage
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
            public ChatImage image;
            public Type type;

            public Fragment(Type type, string text, ChatImage image = null)
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

    public abstract class StreamIntegration<T, K> : Integration<T, K> where T : StreamIntegration<T, K> where K : BaseSaveData
    {
        protected Dictionary<string, ChatImage> CHAT_IMAGE_CACHE = new Dictionary<string, ChatImage>();
        protected readonly Dictionary<string, Queue<ChatImageCallbackPair>> CHAT_IMAGE_PENDING_CONTEXTS = new Dictionary<string, Queue<ChatImageCallbackPair>>();
        protected struct ChatImageCallbackPair
        {
            public Action<ChatImage> onSuccess;
            public Action<HttpUtils.HttpError> onError;
            public ChatImageCallbackPair(Action<ChatImage> onSuccess, Action<HttpUtils.HttpError> onError)
            {
                this.onSuccess = onSuccess;
                this.onError = onError;
            }
        }

        protected void CacheChatImage(ChatImage img)
        {
            if (img != null)
            {
                if (CHAT_IMAGE_CACHE.ContainsKey(img.name))
                {
                    foreach (ChatImage.Frame frame in CHAT_IMAGE_CACHE[img.name].frames)
                    {
                        Destroy(frame.image);
                    }
                }
                CHAT_IMAGE_CACHE[img.name] = img;
            }
        }

        protected void HandleChatImageContext(string key, ChatImage img, HttpUtils.HttpError err)
        {
            if (CHAT_IMAGE_PENDING_CONTEXTS.ContainsKey(key))
            {
                do
                {
                    if (CHAT_IMAGE_PENDING_CONTEXTS[key].TryDequeue(out ChatImageCallbackPair pair))
                    {
                        if (img != null)
                        {
                            try
                            {
                                pair.onSuccess(img);
                            }
                            catch (Exception e)
                            {
                                pair.onError(new HttpUtils.HttpError(500, e.Message));
                            }
                        }
                        else
                        {
                            pair.onError(err);
                        }
                    }
                } while (CHAT_IMAGE_PENDING_CONTEXTS[key].Count > 0);
                CHAT_IMAGE_PENDING_CONTEXTS.Remove(key);
            }
        }

        [Serializable]
        public class ChatMessageEvent : UnityEvent<ChatMessage> { }
        public ChatMessageEvent onChatMessage = new ChatMessageEvent();
    }
}