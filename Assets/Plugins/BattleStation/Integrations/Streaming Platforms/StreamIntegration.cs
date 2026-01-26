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
        public ChatMessageEvent onChatMessage = new ChatMessageEvent();
        public ChatMessageEvent onChatMessageDeleted = new ChatMessageEvent();

        [Serializable]
        public class ChatRedeemEvent : UnityEvent<StreamChatRedeem> { }
        public ChatRedeemEvent onChatRedeem = new ChatRedeemEvent();
    }
}