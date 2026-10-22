using System;
using System.Collections.Generic;
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

    #endregion

    #region Shared Data

    [Serializable]
    public class UserData
    {
        public string id;
        public string username;
        public string global_name;
        public string avatar;
    }

    #endregion

    #region Voice Channel State

    [Serializable]
    public class VoiceChannelStateRequest : ArgRequest<ChannelArgs>
    {
        public VoiceChannelStateRequest()
        {
            cmd = "GET_SELECTED_VOICE_CHANNEL";
        }
    }

    [Serializable]
    public class VoiceChannelStateResponse : DataResponse<VoiceChannelStateResponseData> { }

    [Serializable]
    public class VoiceChannelStateResponseData
    {
        public string id;
        public string name;
        public List<VoiceStateData> voice_states;
    }

    #endregion

    #region Basic Event Subscription

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
    public class ChannelSelectSubscriptionRequest : EventSubscriptionRequest
    {
        public ChannelSelectSubscriptionRequest() : base()
        {
            evt = "VOICE_CHANNEL_SELECT";
        }
    }

    [Serializable]
    public class ChannelSelectEvent : DataResponse<ChannelSelectData> { }

    [Serializable]
    public class ChannelSelectData
    {
        public string channel_id;
        public string guild_id;
    }

    #endregion

    #region Speaking Events

    [Serializable]
    public class SpeakingStartSubscriptionRequest : EventSubscriptionRequest
    {
        public SpeakingStartSubscriptionRequest() : base()
        {
            evt = "SPEAKING_START";
        }
    }

    [Serializable]
    public class SpeakingStopSubscriptionRequest : EventSubscriptionRequest
    {
        public SpeakingStopSubscriptionRequest() : base()
        {
            evt = "SPEAKING_STOP";
        }
    }

    [Serializable]
    public class SpeakingEvent : DataResponse<SpeakingData> { }

    [Serializable]
    public class SpeakingData
    {
        public string user_id;
    }

    #endregion

    #region Voice State Events

    [Serializable]
    public class VoiceStateCreateSubscriptionRequest : EventSubscriptionRequest
    {
        public VoiceStateCreateSubscriptionRequest() : base()
        {
            evt = "VOICE_STATE_CREATE";
        }
    }

    [Serializable]
    public class VoiceStateDeleteSubscriptionRequest : EventSubscriptionRequest
    {
        public VoiceStateDeleteSubscriptionRequest() : base()
        {
            evt = "VOICE_STATE_DELETE";
        }
    }

    [Serializable]
    public class VoiceStateEvent : DataResponse<VoiceStateData> { }

    [Serializable]
    public class VoiceStateData
    {
        public UserData user;
    }

    #endregion

}