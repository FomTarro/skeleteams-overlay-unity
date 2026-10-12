using System;
using Skeletom.BattleStation.Integrations.Discord.API;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Discord.IPC
{
    #region Generic IPC Messages

    public class IpcMessage
    {
        public string cmd;
        public string evt;
        public string nonce;
    }

    public interface IDataResponse { }

    [Serializable]
    public class DataResponse<T> : IpcMessage, IDataResponse
    {
        public T data;
    }

    public interface IArgRequest { }

    public class ArgRequest<T> : IpcMessage, IArgRequest
    {
        public T args;
    }

    #endregion

    #region Errors

    [Serializable]
    public class ErrorResponse : DataResponse<ErrorResponseData> { }

    [Serializable]
    public class ErrorResponseData
    {
        public int code;
        public string message;
    }

    #endregion

    #region Handshake

    [Serializable]
    public class HandshakeRequest
    {
        public int v;
        public string client_id;
    }

    [Serializable]
    public class HandshakeResponseData
    {
        public int v;
        public UserData user;
    }

    [Serializable]
    public class HandshakeResponse : DataResponse<HandshakeResponseData> { }

    #endregion

    #region Authentication/Authorization

    [Serializable]
    public class AuthorizeRequestArgs
    {
        public string client_id;
        public string[] scopes;
        public string code_challenge;
        public string code_challenge_method;
    }

    [SerializeField]
    public class AuthorizeRequest : ArgRequest<AuthorizeRequestArgs>
    {
        public AuthorizeRequest()
        {
            cmd = "AUTHORIZE";
        }
    }

    [Serializable]
    public class AuthorizeResponse : DataResponse<AuthorizeResponseData> { }

    [Serializable]
    public class AuthorizeResponseData
    {
        public string code;
    }

    [Serializable]
    public class AuthenticateRequestArgs
    {
        public string access_token;
    }

    [Serializable]
    public class AuthenticateRequest : ArgRequest<AuthenticateRequestArgs>
    {
        public AuthenticateRequest()
        {
            cmd = "AUTHENTICATE";
        }
    }

    [Serializable]
    public class AuthenticateResponse : DataResponse<AuthenticateResponseData> { }

    [Serializable]
    public class AuthenticateResponseData
    {
        public string access_token;
        public UserData user;
    }

    [Serializable]
    public class TokenHttpResponse
    {
        public string access_token;
    }

    #endregion

    #region Event Subscription

    [Serializable]
    public class ChannelArgs
    {
        public string channel_id;
    }

    [Serializable]
    public class EventSubscriptionRequest : ArgRequest<ChannelArgs>
    {
        public EventSubscriptionRequest()
        {
            cmd = "SUBSCRIBE";
        }
    }

    #endregion

    #region Channel Select Event

    [Serializable]
    public class ChannelSelectEvent : DataResponse<ChannelSelectData> { }

    [Serializable]
    public class ChannelSelectData
    {
        public string channel_id;
        public string guild_id;
    }

    #endregion

    // [Serializable]
    // public class DiscordUser
    // {
    //     public string id;
    //     public string username;
    //     public string global_name;
    // }

    // [Serializable]
    // public class VoiceStateEntry
    // {
    //     public DiscordUser user;
    // }

    // [Serializable]
    // public class SelectedChannelData
    // {
    //     public string id;
    //     public string name;
    //     public List<VoiceStateEntry> voice_states;
    // }

    // [Serializable]
    // public class SelectedChannelFrame
    // {
    //     public SelectedChannelData data;
    // }

    // [Serializable]
    // public class ChannelSelectData
    // {
    //     public string channel_id;
    //     public string guild_id;
    // }

    // [Serializable]
    // public class ChannelSelectFrame
    // {
    //     public ChannelSelectData data;
    // }

    // [Serializable]
    // public class SpeakingData
    // {
    //     public string user_id;
    // }

    // [Serializable]
    // public class SpeakingFrame
    // {
    //     public SpeakingData data;
    // }

    // [Serializable]
    // public class VoiceStateData
    // {
    //     public DiscordUser user;
    // }

    // [Serializable]
    // public class VoiceStateFrame
    // {
    //     public VoiceStateData data;
    // }
}