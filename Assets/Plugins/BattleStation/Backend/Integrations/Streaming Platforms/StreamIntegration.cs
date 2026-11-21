using System;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Skeletom.BattleStation.Integrations
{
    [RequireComponent(typeof(StreamImageHandler))]
    public abstract class StreamIntegration<K> : Integration<K> where K : BaseSaveData
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
        /// <summary>
        /// Event dispatched when a chat message is posted in the current chatroom
        /// </summary>
        public ChatMessageEvent onChatMessage = new();
        [Serializable]
        public class ChatMessageDeletionEvent : UnityEvent<StreamChatMessageDeletion> { }
        public ChatMessageDeletionEvent onChatMessageDelete = new();

        [Serializable]
        public class ChatPaidMessageEvent : UnityEvent<StreamPaidChatMessage> { }
        /// <summary>
        /// Event dispatched when a paid chat message is posted in the current chatroom
        /// </summary>
        public ChatPaidMessageEvent onPaidChatMessage = new();

        [Serializable]
        public class ChatRedeemEvent : UnityEvent<StreamChatRedeem> { }
        /// <summary>
        /// Event dispatched when a chat reward is redeemed by a user in the current chatroom
        /// </summary>
        public ChatRedeemEvent onChatRedeem = new();

        [Serializable]
        public class ChannelFollowEvent : UnityEvent<StreamChannelFollow> { }
        /// <summary>
        /// Event dispatched when a user follows the channel
        /// </summary>
        public ChannelFollowEvent onChannelFollow = new();

        [Serializable]
        public class ChannelPaidSubscriptionEvent : UnityEvent<StreamChannelPaidSubscription> { }
        /// <summary>
        /// Event dispatched when a user pays for a subscription to the given channel
        /// TODO: does this include gift subs?
        /// </summary>
        public ChannelPaidSubscriptionEvent onChannelPaidSubscription = new();

        [Serializable]
        public class StreamRaidEvent : UnityEvent<StreamRaid> { }
        /// <summary>
        /// Event dispatched when the current stream is raided
        /// </summary>
        public StreamRaidEvent onChannelRaid = new();

        [Serializable]
        public class StreamInfoUpdateEvent : UnityEvent<StreamInfo> { }
        /// <summary>
        /// Event dispatched when information about the current stream, 
        /// such as title or category, is updated
        /// </summary>
        public StreamInfoUpdateEvent onStreamInfoUpdate = new();

        #endregion

        #region Public API

        // public abstract void GetUserAvatar(StreamUser user, Action<StreamImage> onSuccess, Action<StreamError> onError);

        /// <summary>
        /// Gets a list of all current users active in the current chatroom
        /// </summary>
        /// <param name="onSuccess">The callback executed on a successful request.</param>
        /// <param name="onError">The callback executed on an unsuccessful request.</param>
        public abstract void GetCurrentChatUsers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError);
        // public abstract void GetCurrentSubscribers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError);
        // public abstract void GetCurrentFollowers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError);

        #endregion
    }
}