using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations.Discord.IPC
{
    [Serializable]
    public class HandshakePayload
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
        public string nonce;
        public ChannelArgs args;
    }

    [Serializable]
    public class AuthorizeArgs
    {
        public string client_id;
        public string[] scopes;
        public string code_challenge;
        public string code_challenge_method;
    }

    [Serializable]
    public class AuthorizeRequest
    {
        public string cmd;
        public string nonce;
        public AuthorizeArgs args;
    }

    [Serializable]
    public class AuthenticateArgs
    {
        public string access_token;
    }

    [Serializable]
    public class AuthenticateRequest
    {
        public string cmd;
        public string nonce;
        public AuthenticateArgs args;
    }

    [Serializable]
    public class FrameHeader
    {
        public string cmd;
        public string evt;
    }

    [Serializable]
    public class AuthorizeResponseData
    {
        public string code;
    }

    [Serializable]
    public class AuthorizeResponse
    {
        public AuthorizeResponseData data;
    }

    [Serializable]
    public class TokenResponse
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