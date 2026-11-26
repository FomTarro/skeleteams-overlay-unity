using System;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;
using Skeletom.Essentials.Utils;

using Skeletom.BattleStation.Server;
using YTLiveChat.Contracts.Models;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Linq;

namespace Skeletom.BattleStation.Integrations.YouTube
{
    public class YouTubeIntegration : StreamIntegration<YouTubeIntegration.IntegrationData>
    {
        private static readonly Utils.Logger LOGGER = new("YouTube");
        public override string FileName => "youtube.json";

        private string TRACKED_CHANNEL_HANDLE = "NO_CHANNEL";

        private readonly ChatClient _client = new(LOGGER);
        private readonly Dictionary<string, RecentChatter> RECENT_CHATTERS = new();
        private const int RECENT_CHATTER_TIMEOUT = 15*60;
        private class RecentChatter
        {
            public readonly StreamUser user;
            public float timeout;
            public RecentChatter(StreamUser user, float timeout)
            {
                this.user = user;
                this.timeout = timeout;
            }
        }

        [Header("Networking")]
        [SerializeField]
        private WebServer _webServer;
        private List<Endpoint> WebServerEndpoints
        {
            get
            {
                return new()
                {
                    new Endpoint("/youtube/track", (req) =>
                    {
                        var queryParam = new List<QueryParameter>(req.queryParameters).Find(param => param.key.Equals("v"));
                        if(queryParam != null)
                        {
                            StartTrackingLiveChat(queryParam.value);
                        }
                        return new EndpointResponse(200, "Live Video Set");
                    }),
                    new Endpoint("/youtube/reload", (req) =>
                    {
                        FromSaveData(SaveDataManager.ReadSaveData(this));
                        return new EndpointResponse(200, "Reload Requested");
                    })
                };
            }
        }

        #region Lifecycle

        public override void Enable()
        {
            foreach (Endpoint endpoint in WebServerEndpoints)
            {
                _webServer.RegisterEndpoint(endpoint);
            }
            FromSaveData(SaveDataManager.ReadSaveData(this));
            
        }

        public override void Disable()
        {
            foreach (Endpoint endpoint in WebServerEndpoints)
            {
                _webServer.UnregisterEndpoint(endpoint);
            }
            StopTrackingLiveChat();
        }

        private void Update()
        {
            if(_client != null) {
                ChatClient.ChatEvent chatEvent;
                do
                {
                    chatEvent = _client.GetNextChatItem();
                    if (chatEvent != null)
                    {
                        ProcessChatEvent(chatEvent);
                    }
                } while (chatEvent != null);
            }
            ProcessRecentChatterTimeouts(Time.deltaTime);
        }

        private void ProcessChatEvent(ChatClient.ChatEvent chatEvent)
        {
            try
            {
                switch (chatEvent.eventType)
                {
                    case ChatClient.ChatEvent.EventType.Message:
                        HandleChatMessage(chatEvent.item);
                        break;
                    case ChatClient.ChatEvent.EventType.Delete:
                        HandleChatMessageDeletion(chatEvent.id);
                        break;
                }
            }catch(Exception e)
            {
                LOGGER.LogError(e.Message);
            }
        }

        private readonly List<string> TIMEOUTS = new();
        /// <summary>
        /// Decrements all pending request timeouts by the provided delta,
        /// and invokes the OnError callback for requests which expire.
        /// </summary>
        /// <param name="delta">The time delta in milliseconds</param>
        private void ProcessRecentChatterTimeouts(float delta)
        {
            foreach(var kvp in RECENT_CHATTERS)
            {
                kvp.Value.timeout -= delta;
                if(kvp.Value.timeout < 0f)
                {
                    TIMEOUTS.Add(kvp.Key);
                }
            }
            for(int i = 0; i < TIMEOUTS.Count; i++)
            {
                RECENT_CHATTERS.Remove(TIMEOUTS[i]);
            }
            TIMEOUTS.Clear();
        }

        #endregion

        #region Chat Events

        // Wrapper Class for YTLiveChat, the secret API
        private class ChatClient
        {
            private readonly YTLiveChat.Services.YTHttpClient _httpClient;
            private readonly YTLiveChat.Services.YTLiveChat _chatClient;

            private readonly ConcurrentQueue<ChatEvent> _chatEventQueue = new();

            public ChatClient(Utils.Logger logger) {
                _httpClient = new(
                    new HttpClient()
                    {
                        BaseAddress=new Uri("https://www.youtube.com/watch")
                    }
                );
                _chatClient = new(
                    new YTLiveChat.Contracts.YTLiveChatOptions(), 
                    _httpClient
                );
                // TODO: reconnection logic
                _chatClient.InitialPageLoaded += (_, e) => logger.Log($"Loaded stream: {e.LiveId}");
                _chatClient.ChatStopped += (_, e) => logger.LogWarning($"Chat stopped: {e.Reason}");
                _chatClient.ErrorOccurred += (_, e) => logger.LogError($"YTLiveChat Error: {e.GetException()}");
                _chatClient.ChatReceived += (_, e) =>
                {
                    _chatEventQueue.Enqueue(new ChatEvent(
                        ChatEvent.EventType.Message,
                        e.ChatItem.Id,
                        e.ChatItem)
                    );
                };
                _chatClient.ChatItemDeleted += (_, e) =>
                {
                    _chatEventQueue.Enqueue(new ChatEvent(
                        ChatEvent.EventType.Delete,
                        e.TargetId,
                        null)
                    );
                };
            }

            public class ChatEvent
            {
                public enum EventType
                {
                    Message,
                    Delete,
                }

                public readonly EventType eventType;
                public string id;
                public ChatItem item;

                public ChatEvent(EventType eventType, string id, ChatItem item)
                {
                    this.eventType = eventType;
                    this.id = id;
                    this.item = item;
                }
            }

            public void Start(string videoId)
            {
                _chatClient.Start(liveId:videoId);
            }

            public void Stop()
            {
                _chatClient.Stop();
            }

            public ChatEvent GetNextChatItem()
            {
                if(_chatEventQueue.TryDequeue(out var item))
                {
                    return item;
                };
                return null;
            }
        }

        public void StartTrackingLiveChat(string videoId)
        {
            _client.Start(videoId);
        }

        public void StopTrackingLiveChat()
        {
            _client.Stop();
        }

        private void HandleChatMessage(ChatItem item)
        {
            StreamUser chatter = new(item.Author.Name, item.Id);
            List<StreamChatMessage.Fragment> fragments = new();
            List<StreamBadge> badges = new();
            DateTime timestamp = DateTime.Now;
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () =>
                {
                    StreamChatMessage message = new(
                        item.Id,
                        "YouTube",
                        timestamp,
                        chatter,
                        "#FFFFFF",
                        badges,
                        fragments
                    );
                    onChatMessage.Invoke(message);
                }
            );
            foreach(MessagePart fragment in item.Message){
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                switch (fragment)
                {
                    case TextPart t:
                        fragments.Add(new StreamChatMessage.Fragment(
                            StreamChatMessage.Fragment.Type.TEXT, 
                            t.Text
                        ));
                        manager.ResolveDependency(taskId);
                        break;
                    case EmojiPart e:
                        StreamChatMessage.Fragment emojiFragment = new(
                            StreamChatMessage.Fragment.Type.EMOTE, 
                            e.EmojiText
                        );
                        fragments.Add(emojiFragment);
                        ImageHandler.GetFromRemote(
                            e.Url,
                            new HttpUtils.HttpHeaders(), 
                            $"emoji_{e.EmojiText}",
                            (success) =>
                            {
                                emojiFragment.image = success;
                                manager.ResolveDependency(taskId);
                            },
                            (err) =>
                            {
                                manager.ResolveDependency(taskId);
                            }
                        );
                        break;
                }
            }
            if(item.Author.Badge != null)
            {
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                StreamBadge newBadge = new(item.Author.Badge.Label, taskId);
                badges.Add(newBadge);
                ImageHandler.GetFromRemote(
                    item.Author.Badge.Thumbnail.Url,
                    new HttpUtils.HttpHeaders(),
                    $"badge_{item.Author.Badge.Label}",
                    (success) =>
                    {
                        newBadge.image = success;
                        manager.ResolveDependency(taskId);
                    },
                    (err) =>
                    {
                        manager.ResolveDependency(taskId);
                    }
                );
            }
            string avatarTaskId = Guid.NewGuid().ToString();
            manager.AddDependency(avatarTaskId);
            ImageHandler.GetFromRemote(item.Author.Thumbnail.Url, new HttpUtils.HttpHeaders(),
            $"avatar_{item.Author.ChannelId}",
            (avatar) =>
            {
                chatter.avatar = avatar;
                manager.ResolveDependency(avatarTaskId);
            },
            (err) =>
            {
                LOGGER.LogError(err.message);
                manager.ResolveDependency(avatarTaskId);
            });
            manager.Enable(true);

            // Keep a list of recent chatters
            // Current best approach to getting a list of chatters
            // is to just collect them as chat messages come in
            // There is no API, public nor private, which can get a complete list
            if (RECENT_CHATTERS.ContainsKey(chatter.id))
            {
                RECENT_CHATTERS[chatter.id].timeout = RECENT_CHATTER_TIMEOUT;
            }
            else
            {
                RECENT_CHATTERS[chatter.id] = new(chatter, RECENT_CHATTER_TIMEOUT);
            }
        }

        private void HandleChatMessageDeletion(string messageId)
        {
            StreamChatMessageDeletion deletion = new(messageId);
            onChatMessageDelete.Invoke(deletion);
        }

        #endregion

        #region Chatters

        public override void GetCurrentChatUsers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError)
        {
            onSuccess(RECENT_CHATTERS.Values.Select(user => user.user).ToList());
        }


        #endregion

        #region File IO

        public override void FromSaveData(IntegrationData data)
        {

        }

        public override IntegrationData ToSaveData()
        {
            return new IntegrationData()
            {
                channelHandle=TRACKED_CHANNEL_HANDLE
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string channelHandle;
        }

        #endregion
    }
}


