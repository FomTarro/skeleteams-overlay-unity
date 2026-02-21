namespace Skeletom.BattleStation.Integrations.Twitch
{
    public interface IEndpoints
    {
        public string USER_TOKEN_ENDPOINT { get; }
        public string USER_TOKEN_REDIRECT { get; }
        public string USER_INFO_ENDPOINT { get; }

        public string EVENTSUB_SOCKET_ENDPOINT { get; }
        public string EVENTSUB_SUBSCRIPTION_ENDPOINT { get; }

        public string EMOTES_GLOBAL_ENDPOINT { get; }
        public string EMOTES_SET_ENDPOINT { get; }
        public string EMOTES_CHANNEL_ENDPOINT { get; }
        public string EMOTES_INDIVIDUAL_ENDPOINT { get; }

        public string BADGES_GLOBAL_ENDPOINT { get; }
        public string BADGES_CHANNEL_ENDPOINT { get; }
        public string BADGES_INDIVIDUAL_ENDPOINT { get; }

        public string CHANNEL_INFO_ENDPOINT { get; }
        public string STREAMS_ENDPOINT { get; }
        public string CHATTERS_ENDPOINT { get; }

        public string AD_SCHEDULE_ENDPOINT { get; }

        public string TOKEN_VALIDATION_ENDPOINT { get; }
    }

    public class TwitchAPI : IEndpoints
    {
        public string USER_TOKEN_ENDPOINT => "https://id.twitch.tv/oauth2/authorize";
        public string USER_TOKEN_REDIRECT => "http://localhost:61616/twitch/oauth2";
        public string USER_INFO_ENDPOINT => "https://api.twitch.tv/helix/users";

        public string EVENTSUB_SOCKET_ENDPOINT => "wss://eventsub.wss.twitch.tv/ws";
        public string EVENTSUB_SUBSCRIPTION_ENDPOINT => "https://api.twitch.tv/helix/eventsub/subscriptions";

        public string EMOTES_GLOBAL_ENDPOINT => "https://api.twitch.tv/helix/chat/emotes/global";

        public string EMOTES_SET_ENDPOINT => "https://api.twitch.tv/helix/chat/emotes/set";
        public string EMOTES_INDIVIDUAL_ENDPOINT => "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}";
        public string EMOTES_CHANNEL_ENDPOINT => "https://api.twitch.tv/helix/chat/emotes";

        public string BADGES_GLOBAL_ENDPOINT => "https://api.twitch.tv/helix/chat/badges/global";
        public string BADGES_CHANNEL_ENDPOINT => "https://api.twitch.tv/helix/chat/badges";
        public string BADGES_INDIVIDUAL_ENDPOINT => throw new System.NotImplementedException();

        public string CHANNEL_INFO_ENDPOINT => "https://api.twitch.tv/helix/channels";

        public string CHATTERS_ENDPOINT => "https://api.twitch.tv/helix/chat/chatters";
        public string STREAMS_ENDPOINT => "https://api.twitch.tv/helix/stream";

        public string AD_SCHEDULE_ENDPOINT => "https://api.twitch.tv/helix/channels/ads";

        public string TOKEN_VALIDATION_ENDPOINT => "https://id.twitch.tv/oauth2/validate";
    }

    public class LocalAPI : IEndpoints
    {
        public string USER_TOKEN_ENDPOINT => "https://id.twitch.tv/oauth2/authorize";
        public string USER_TOKEN_REDIRECT => "http://localhost:61616/twitch/oauth2";
        public string USER_INFO_ENDPOINT => "https://api.twitch.tv/helix/users";

        public string EVENTSUB_SOCKET_ENDPOINT => "ws://localhost:8080/ws";
        public string EVENTSUB_SUBSCRIPTION_ENDPOINT => "http://localhost:8080/eventsub/subscriptions";

        public string EMOTES_GLOBAL_ENDPOINT => "https://api.twitch.tv/helix/chat/emotes/global";
        public string EMOTES_SET_ENDPOINT => "https://api.twitch.tv/helix/chat/emotes/set";
        public string EMOTES_INDIVIDUAL_ENDPOINT => "https://static-cdn.jtvnw.net/emoticons/v2/{{id}}/{{format}}/{{theme_mode}}/{{scale}}";
        public string EMOTES_CHANNEL_ENDPOINT => "https://api.twitch.tv/helix/chat/emotes";

        public string BADGES_GLOBAL_ENDPOINT => "https://api.twitch.tv/helix/chat/badges/global";
        public string BADGES_CHANNEL_ENDPOINT => "https://api.twitch.tv/helix/chat/badges";
        public string BADGES_INDIVIDUAL_ENDPOINT => throw new System.NotImplementedException();

        public string CHANNEL_INFO_ENDPOINT => "https://api.twitch.tv/helix/channels";

        public string CHATTERS_ENDPOINT => "https://api.twitch.tv/helix/chat/chatters";
        public string STREAMS_ENDPOINT => "https://api.twitch.tv/helix/stream";

        public string AD_SCHEDULE_ENDPOINT => "https://api.twitch.tv/helix/channels/ads";

        public string TOKEN_VALIDATION_ENDPOINT => "https://id.twitch.tv/oauth2/validate";
    }
}
