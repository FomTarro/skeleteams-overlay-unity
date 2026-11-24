using System;
using System.Collections.Generic;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using Skeletom.Essentials.IO;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using UnityEngine;
using Skeletom.Essentials.Utils;
using Skeletom.BattleStation.Integrations.YouTube.API;
using Youtube.Api.V3;
using Skeletom.BattleStation.Server;
using System.Net.Http;
using YTLiveChat.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace Skeletom.BattleStation.Integrations.YouTube
{
    public class YouTubeIntegration : StreamIntegration<YouTubeIntegration.IntegrationData>
    {
        private readonly Utils.Logger LOGGER = new("YouTube");
        public override string FileName => "youtube.json";

        private const string CLIENT_ID = "AIzaSyAazq1yIPfoSTLRssMfV7vhN3PmQDcE96Y";
        private string USER_TOKEN = "NO_TOKEN_SET";
        private string BROADCASTER_ID = "NO_ID_SET";

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
            YTLiveChat.Contracts.Services.ChatReceivedEventArgs data = null;
            do
            {
                if (_argsQueue.TryDequeue(out data))
                {
                    HandleChatMessage(data);
                }
            } while (data != null);
        }

        private string content;
        // private async Task GetEmoteData(string videoId, CancellationToken cts)
        // {
        //     HttpClient client = new();
        //     client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        //     var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.youtube.com/live_chat?is_popout=1&v={videoId}");
        //     var response = await client.SendAsync(request);
        //     var str = await response.Content.ReadAsStringAsync();
        //     Debug.Log(str.Contains("window[\"ytInitialData\"]"));
        //     try{
        //         var snippet = str.Split("window[\"ytInitialData\"] = ")[1].Split("</script>")[0];
        //         content = snippet;
                
        //     }catch(Exception e)
        //     {
        //         Debug.LogError(e);
        //     }
        //     SaveDataManager.WriteSaveData(this);
        // }

        #endregion

        #region GRPC

        private readonly GrpcChannel _channel = GrpcChannel.ForAddress("https://youtube.googleapis.com", new GrpcChannelOptions
        {
            HttpHandler = new YetAnotherHttpHandler()
        });
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<LiveChatMessage> _messageQueue = new();

       private readonly ConcurrentQueue<YTLiveChat.Contracts.Services.ChatReceivedEventArgs> _argsQueue= new();


        public void StartTrackingLiveChat(string videoId)
        {
            StopTrackingLiveChat();
            _cts = new CancellationTokenSource();
            HttpUtils.HttpHeaders header = new();
            header.customHeaders.Add("x-goog-api-key", CLIENT_ID);
            StartCoroutine(HttpUtils.GetRequest(
                $"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={videoId}", 
                header,
                (success) =>
                {
                    // Turn holistic video URL into live chat ID
                    var response = JsonUtility.FromJson<LiveStreamingDetailsResponse>(success);
                    if(response.items.Length > 0)
                    {
                        var client = new YTLiveChat.Services.YTLiveChat(
                            new YTLiveChat.Contracts.YTLiveChatOptions(),
                            new YTLiveChat.Services.YTHttpClient(
                                new HttpClient()
                                {
                                    BaseAddress=new Uri("https://www.youtube.com/watch")
                                }
                            )
                        );
                        client.InitialPageLoaded += (_, e) => LOGGER.Log($"Loaded stream: {e.LiveId}");
                        client.ChatStopped += (_, e) => LOGGER.LogWarning($"Chat stopped: {e.Reason}");
                        client.ErrorOccurred += (_, e) => LOGGER.LogError($"YTLiveChat Error: {e.GetException()}");
                        client.ChatReceived += (_, e) => LOGGER.Log($"Message received!");
                        client.ChatReceived += (chat, args) =>
                        {
                            _argsQueue.Enqueue(args);
                        };
                        client.Start(liveId:videoId);
                        Debug.LogWarning(response.items[0].liveStreamingDetails.activeLiveChatId);
                        // _ = ListenToChatAsync(response.items[0].liveStreamingDetails.activeLiveChatId, _cts.Token);
                        // _ = GetEmoteData(videoId, _cts.Token);
                    }
                    else
                    {
                        LOGGER.LogError("No videos returned in response.");
                    }
                }, 
                (err) =>
                {
                    LOGGER.LogError(err.message);
                }
            ));
        }

        private async Task ListenToChatAsync(string liveChatId, CancellationToken token)
        {
            var client = new V3DataLiveChatMessageService.V3DataLiveChatMessageServiceClient(_channel);
            var headers = new Metadata { { "x-goog-api-key", CLIENT_ID } };
            string nextPageToken = null;
            while (!token.IsCancellationRequested)
            {
                var request = new LiveChatMessageListRequest
                {
                    LiveChatId = liveChatId,
                    Part = { "snippet", "authorDetails" }
                };
                if (!string.IsNullOrEmpty(nextPageToken))
                {
                    request.PageToken = nextPageToken;
                }

                try
                {
                    using var call = client.StreamList(request, headers: headers, cancellationToken: token);
                    while (await call.ResponseStream.MoveNext(token))
                    {
                        var response = call.ResponseStream.Current;
                        nextPageToken = response.NextPageToken;
                        foreach (var item in response.Items)
                        {
                            _messageQueue.Enqueue(item);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled)
                {
                    LOGGER.LogWarning($"gRPC stream interrupted ({ex.StatusCode}). Reconnecting...");
                    await Task.Delay(2000, token);
                }
                catch(Exception ex)
                {
                    LOGGER.LogError(ex.Message);
                }
            }
        }

        public void StopTrackingLiveChat()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void HandleChatMessage(YTLiveChat.Contracts.Services.ChatReceivedEventArgs chatEvent)
        {
            StreamUser chatter = new(chatEvent.ChatItem.Author.Name, chatEvent.ChatItem.Id);
            List<StreamChatMessage.Fragment> fragments = new();
            List<StreamBadge> badges = new();
            DateTime timestamp = DateTime.Now;
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () =>
                {
                    StreamChatMessage message = new(
                        chatEvent.ChatItem.Id,
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
            foreach(YTLiveChat.Contracts.Models.MessagePart fragment in chatEvent.ChatItem.Message){
                string taskId = Guid.NewGuid().ToString();
                manager.AddDependency(taskId);
                switch (fragment)
                {
                    case TextPart t:
                        fragments.Add( new StreamChatMessage.Fragment(
                            StreamChatMessage.Fragment.Type.TEXT, 
                            t.Text
                        ));
                        manager.ResolveDependency(taskId);
                        break;
                    case EmojiPart e:
                        var emojiFragment = new StreamChatMessage.Fragment(
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
            string avatarTaskId = Guid.NewGuid().ToString();
            manager.AddDependency(avatarTaskId);
            ImageHandler.GetFromRemote(chatEvent.ChatItem.Author.Thumbnail.Url, new HttpUtils.HttpHeaders(),
            $"avatar_{chatEvent.ChatItem.Author.ChannelId}",
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
        }

        private void HandleChatMessage(LiveChatMessage chatEvent)
        {
            StreamUser chatter = new(chatEvent.AuthorDetails.DisplayName, chatEvent.Id);
            List<StreamChatMessage.Fragment> fragments = new();
            List<StreamBadge> badges = new();
            DateTime timestamp = DateTime.Now;
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () =>
                {
                    StreamChatMessage message = new(
                        chatEvent.Id,
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
            fragments.Add( new StreamChatMessage.Fragment(
                StreamChatMessage.Fragment.Type.TEXT, 
                chatEvent.Snippet.DisplayMessage
            ));
            string avatarTaskId = Guid.NewGuid().ToString();
            manager.AddDependency(avatarTaskId);
            ImageHandler.GetFromRemote(chatEvent.AuthorDetails.ProfileImageUrl, new HttpUtils.HttpHeaders(),
            $"avatar_{chatEvent.AuthorDetails.DisplayName}",
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
        }

        #endregion

        #region Chatters

        public override void GetCurrentChatUsers(Action<List<StreamUser>> onSuccess, Action<StreamError> onError)
        {
            throw new NotImplementedException();
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
                token = USER_TOKEN,
                content = content
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
            public string content;
        }

        #endregion
    }
}


