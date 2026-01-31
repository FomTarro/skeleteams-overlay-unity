using System;
using Skeletom.Essentials.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations
{
    [RequireComponent(typeof(StreamImageHandler))]
    public abstract class StreamIntegration<T, K> : Integration<T, K> where T : StreamIntegration<T, K> where K : BaseSaveData
    {
        [SerializeField]
        private StreamImageHandler _imageHandler;
        public StreamImageHandler ImageHandler => _imageHandler ??= GetComponent<StreamImageHandler>() ?? gameObject.AddComponent<StreamImageHandler>();

        [Serializable]
        public class ChatMessageEvent : UnityEvent<StreamChatMessage> { }
        public ChatMessageEvent onChatMessage = new();
        [Serializable]
        public class ChatMessageDeletionEvent : UnityEvent<StreamChatMessageDeletion> { }
        public ChatMessageDeletionEvent onChatMessageDelete = new ChatMessageDeletionEvent();

        [Serializable]
        public class ChatRedeemEvent : UnityEvent<StreamChatRedeem> { }
        public ChatRedeemEvent onChatRedeem = new();

        [Serializable]
        public class ChannelFollowEvent : UnityEvent<StreamChannelFollow> { }
        public ChannelFollowEvent onChannelFollow = new();

        [Serializable]
        public class StreamInfoUpdateEvent : UnityEvent<StreamInfo> { }
        public StreamInfoUpdateEvent onStreamInfoUpdate = new();
    }
}