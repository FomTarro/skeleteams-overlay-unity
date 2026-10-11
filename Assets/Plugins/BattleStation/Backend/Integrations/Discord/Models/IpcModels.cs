using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations.Discord.IPC
{
    [Serializable]
    public class DiscordRequest
    {
        public string cmd;
        public string nonce;
    }

    [Serializable]
    public class DiscordResponse<T>
    {
        public string cmd;
        public string nonce;
        public T data;
    }

    [Serializable]
    public class ErrorResponseData
    {
        public int code;
        public string message;
    }

    [Serializable]
    public class ErrorResponse : DiscordResponse<ErrorResponseData> { }

    [Serializable]
    public class HandshakeRequest
    {
        public int v;
        public string client_id;
    }

    [Serializable]
    public class ChannelArgs
    {
        public string channel_id;
    }

    [Serializable]
    public class EventRequest
    {
        public string cmd;
        public string evt;
        public ChannelArgs args;
    }

    [Serializable]
    public class AuthorizeRequestArgs
    {
        public string client_id;
        public string[] scopes;
        public string code_challenge;
        public string code_challenge_method;
    }

    [Serializable]
    public class AuthorizeRequest : DiscordRequest
    {
        new public string cmd = "AUTHORIZE";
        public AuthorizeRequestArgs args;
    }

    [Serializable]
    public class AuthenticateRequestArgs
    {
        public string access_token;
    }

    [Serializable]
    public class AuthenticateRequest : DiscordRequest
    {
        new public string cmd = "AUTHENTICATE";
        public AuthenticateRequestArgs args;
    }

    [Serializable]
    public class AuthenticateResponseData
    {
        public string access_token;
        public DiscordUser user;
    }

    [Serializable]
    public class AuthenticateResponse : DiscordResponse<AuthenticateResponseData> { }

    [Serializable]
    public class FrameHeader : DiscordRequest
    {
        public string evt;
    }

    [Serializable]
    public class HandshakeResponseData
    {
        public int v;
        public DiscordUser user;
    }

    [Serializable]
    public class HandshakeResponse : DiscordResponse<HandshakeResponseData> { }

    [Serializable]
    public class AuthorizeResponseData
    {
        public string code;
    }

    [Serializable]
    public class AuthorizeResponse : DiscordResponse<AuthorizeResponseData> { }

    [Serializable]
    public class TokenHttpResponse
    {
        public string access_token;
    }

    [Serializable]
    public class DiscordUser
    {
        public string id;
        public string username;
        public string global_name;
    }

    [Serializable]
    public class VoiceStateEntry
    {
        public DiscordUser user;
    }

    [Serializable]
    public class SelectedChannelData
    {
        public string id;
        public string name;
        public List<VoiceStateEntry> voice_states;
    }

    [Serializable]
    public class SelectedChannelFrame
    {
        public SelectedChannelData data;
    }

    [Serializable]
    public class ChannelSelectData
    {
        public string channel_id;
        public string guild_id;
    }

    [Serializable]
    public class ChannelSelectFrame
    {
        public ChannelSelectData data;
    }

    [Serializable]
    public class SpeakingData
    {
        public string user_id;
    }

    [Serializable]
    public class SpeakingFrame
    {
        public SpeakingData data;
    }

    [Serializable]
    public class VoiceStateData
    {
        public DiscordUser user;
    }

    [Serializable]
    public class VoiceStateFrame
    {
        public VoiceStateData data;
    }
}