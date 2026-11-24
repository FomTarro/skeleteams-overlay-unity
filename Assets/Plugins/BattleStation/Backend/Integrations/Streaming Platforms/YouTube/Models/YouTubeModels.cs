using System;

namespace Skeletom.BattleStation.Integrations.YouTube.API
{
    [Serializable]
    public class PaginatedItemResponse<T>
    {
        public string kind;
        public string etag;
        public string nextPageToken;
        public string prevPageToken;
        public PageInfo pageInfo;
        public T[] items;
    }

    [Serializable]
    public class PageInfo
    {
        public int totalResults;
        public int resultsPerPage;
    }

    [Serializable]
    public class LiveStreamingDetails
    {
        public string scheduledStartTime;
        public string actualStartTime;
        public string activeLiveChatId;
        public int concurrentViewers;
    }

    [Serializable]
    public class LiveStreamingDetailsItem
    {
        public string kind;
        public string etag;
        public string id;
        public LiveStreamingDetails liveStreamingDetails;
    }

    [Serializable]
    public class LiveStreamingDetailsResponse : PaginatedItemResponse<LiveStreamingDetailsItem> { }

}