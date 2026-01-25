using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations
{
    [RequireComponent(typeof(StreamIntegrationImageHandler))]
    public abstract class StreamIntegration<T, K> : Integration<T, K> where T : StreamIntegration<T, K> where K : BaseSaveData
    {
        [SerializeField]
        private StreamIntegrationImageHandler _imageHandler;
        public StreamIntegrationImageHandler ImageHandler => _imageHandler ??= GetComponent<StreamIntegrationImageHandler>() ?? gameObject.AddComponent<StreamIntegrationImageHandler>();

        [Serializable]
        public class ChatMessageEvent : UnityEvent<StreamingPlatformChatMessage> { }
        public ChatMessageEvent onChatMessage = new ChatMessageEvent();
    }
}