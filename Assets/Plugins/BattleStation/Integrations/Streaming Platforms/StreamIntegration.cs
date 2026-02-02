using System;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations
{
    [RequireComponent(typeof(StreamImageHandler))]
    public abstract class StreamIntegration<T, K> : Integration<T, K> where T : StreamIntegration<T, K> where K : BaseSaveData
    {
        #region Caches

        [SerializeField]
        private StreamImageHandler _imageHandler;
        public StreamImageHandler ImageHandler
        {
            get
            {
                if(_imageHandler == null)
                {
                    _imageHandler = GetComponent<StreamImageHandler>();
                    if(_imageHandler == null)
                    {
                        _imageHandler = gameObject.AddComponent<StreamImageHandler>();
                    }
                }
                return _imageHandler;
            }
        }

        #endregion

        #region Events

        [Serializable]
        public class ChatMessageEvent : UnityEvent<StreamChatMessage> { }
        public ChatMessageEvent onChatMessage = new();
        [Serializable]
        public class ChatMessageDeletionEvent : UnityEvent<StreamChatMessageDeletion> { }
        public ChatMessageDeletionEvent onChatMessageDelete = new ChatMessageDeletionEvent();

        [Serializable]
        public class ChatPaidMessageEvent : UnityEvent<StreamPaidChatMessage> { }
        public ChatPaidMessageEvent onPaidChatMessage = new();

        [Serializable]
        public class ChatRedeemEvent : UnityEvent<StreamChatRedeem> { }
        public ChatRedeemEvent onChatRedeem = new();

        [Serializable]
        public class ChannelFollowEvent : UnityEvent<StreamChannelFollow> { }
        public ChannelFollowEvent onChannelFollow = new();

        [Serializable]
        public class ChannelPaidSubscriptionEvent : UnityEvent<StreamChannelPaidSubscription> { }
        public ChannelPaidSubscriptionEvent onChannelPaidSubscription = new();

        [Serializable]
        public class StreamRaidEvent : UnityEvent<StreamRaid> { }
        public StreamRaidEvent onChannelRaid = new();

        [Serializable]
        public class StreamInfoUpdateEvent : UnityEvent<StreamInfo> { }
        public StreamInfoUpdateEvent onStreamInfoUpdate = new();

        #endregion

        #region Public API

        // public abstract void GetUserAvatar(StreamUser user, Action<StreamImage> onSuccess, Action<StreamError> onError);

        public abstract void GetCurrentChatUsers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError);
        // public abstract void GetCurrentSubscribers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError);
        // public abstract void GetCurrentFollowers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError);

        #endregion
    }
}