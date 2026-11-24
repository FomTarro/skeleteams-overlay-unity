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
using Newtonsoft.Json;
using Skeletom.BattleStation.Integrations.YouTube.GRPC;

namespace Skeletom.BattleStation.Integrations.YouTube
{
    public class YouTubeIntegration : StreamIntegration<YouTubeIntegration.IntegrationData>
    {

        private readonly Utils.Logger LOGGER = new("YouTube");
        public override string FileName => "youtube.json";

        private const string CLIENT_ID = "AIzaSyAazq1yIPfoSTLRssMfV7vhN3PmQDcE96Y";
        private string USER_TOKEN = "NO_TOKEN_SET";
        private string BROADCASTER_ID = "NO_ID_SET";

        #region Lifecycle

        public override void Enable()
        {
            // Set up token ingest endpoints 
            // foreach (Endpoint endpoint in WebServerEndpoints)
            // {
            //     _webServer.RegisterEndpoint(endpoint);
            // }
            FromSaveData(SaveDataManager.ReadSaveData(this));
        }

        public override void Disable()
        {
            // foreach (Endpoint endpoint in WebServerEndpoints)
            // {
            //     _webServer.UnregisterEndpoint(endpoint);
            // }
            // _socket.Stop();
        }

        private void Start()
        {
            _cts = new CancellationTokenSource();
            var handler = new YetAnotherHttpHandler();
            _channel = GrpcChannel.ForAddress("https://youtube.googleapis.com", new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            TrackVideo("BtRcjDLipZY");
            // _ = ListenToChatAsync(_cts.Token);
        }

        private void Update()
        {
            string data = null;
            do
            {
                if (_messageQueue.TryDequeue(out data))
                {
                    var msg = JsonConvert.DeserializeObject<LiveChatMessageWrapper>(data);
                    HandleChatMessage(msg);
                }
            } while (data != null);
        }

        #endregion

        #region GRPC

        private GrpcChannel _channel;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _messageQueue = new();

        public void TrackVideo(string videoId)
        {
            HttpUtils.HttpHeaders header = new();
            header.customHeaders.Add("x-goog-api-key", CLIENT_ID);
            StartCoroutine(HttpUtils.GetRequest(
                $"https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id={videoId}", 
                header,
                (success) =>
                {
                    Debug.Log(success);
                    var response = JsonUtility.FromJson<LiveStreamingDetailsResponse>(success);
                    Debug.Log(JsonUtility.ToJson(response));
                    _ = ListenToChatAsync(response.items[0].liveStreamingDetails.activeLiveChatId, _cts.Token);
                }, 
                (err) =>
                {
                    Debug.LogError(err.message);
                }
            ));
        }
        private async Task ListenToChatAsync(string liveChatId, CancellationToken token)
        {
            var client = new V3DataLiveChatMessageService.V3DataLiveChatMessageServiceClient(_channel);
            var headers = new Metadata { { "x-goog-api-key", CLIENT_ID } };
            string nextPageToken = null;
            Debug.Log("Firing up the engine");
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
                    Debug.Log("Calling...");
                    using var call = client.StreamList(request, headers: headers, cancellationToken: token);
                    Debug.Log("Call made!");
                    while (await call.ResponseStream.MoveNext(token))
                    {
                        var response = call.ResponseStream.Current;
                        nextPageToken = response.NextPageToken;

                        foreach (var item in response.Items)
                        {
                            
                            var wrapper = new LiveChatMessageWrapper()
                            {
                                id=item.Id,
                                message=item.Snippet.DisplayMessage,
                                userName=item.AuthorDetails.DisplayName,
                                userAvatarUrl=item.AuthorDetails.ProfileImageUrl
                            };
                            _messageQueue.Enqueue(JsonConvert.SerializeObject(wrapper));
                            Debug.Log(JsonConvert.SerializeObject(wrapper));
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode != StatusCode.Cancelled)
                {
                    Debug.LogWarning($"gRPC stream interrupted ({ex.StatusCode}). Reconnecting...");
                    await Task.Delay(2000, token);
                }
                catch(Exception ex)
                {
                    Debug.LogError(ex.Message);
                }
            }
            Debug.Log("Spinning down the engine...");
        }

        private void HandleChatMessage(GRPC.LiveChatMessageWrapper chatEvent)
        {
            StreamUser chatter = new(chatEvent.userName, chatEvent.id);
            List<StreamChatMessage.Fragment> fragments = new();
            List<StreamBadge> badges = new();
            DateTime timestamp = DateTime.Now;
            // create a callback for all HTTP dependencies
            DependencyManager manager = new(
                () =>
                {
                    StreamChatMessage message = new(
                        chatEvent.id,
                        "YouTube",
                        timestamp,
                        chatter,
                        "#FFFFFF",
                        badges,
                        fragments);
                    onChatMessage.Invoke(message);
                }
            );
            fragments.Add( new StreamChatMessage.Fragment(
                StreamChatMessage.Fragment.Type.TEXT, 
                chatEvent.message
            ));
            string avatarTaskId = Guid.NewGuid().ToString();
            manager.AddDependency(avatarTaskId);
            ImageHandler.GetFromRemote(chatEvent.userAvatarUrl, new HttpUtils.HttpHeaders(),
            $"avatar_{chatEvent.userName}",
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
                token = USER_TOKEN
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
        }

        #endregion
    }
}
