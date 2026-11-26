using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Core;
using Grpc.Net.Client;
using Skeletom.BattleStation.Integrations.YouTube.API;
using Skeletom.Essentials.Utils;
using UnityEngine;
using Youtube.Api.V3;

public class YouTubeGRPC : MonoBehaviour
{
        #region GRPC
        private const string CLIENT_ID = "NOT_SET";
        private readonly GrpcChannel _channel = GrpcChannel.ForAddress("https://youtube.googleapis.com", new GrpcChannelOptions
        {
            HttpHandler = new YetAnotherHttpHandler()
        });
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<LiveChatMessage> _messageQueue = new();

        private void Update()
        {
            LiveChatMessage data = null;
            do
            {
                if (_messageQueue.TryDequeue(out data))
                {
                    // Do something with data;
                }
            } while (data != null);
        }

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
                        _ = ListenToChatAsync(response.items[0].liveStreamingDetails.activeLiveChatId, _cts.Token);
                    }
                    else
                    {
                        Debug.LogError("No videos returned in response.");
                    }
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
                    Debug.LogWarning($"gRPC stream interrupted ({ex.StatusCode}). Reconnecting...");
                    await Task.Delay(2000, token);
                }
                catch(Exception ex)
                {
                    Debug.LogError(ex.Message);
                }
            }
        }

        public void StopTrackingLiveChat()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    #endregion
}
