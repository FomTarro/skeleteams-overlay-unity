using System;

namespace Skeletom.BattleStation.Integrations.Discord.API
{
    [Serializable]
    public class TokenResponse
    {
        public string access_token;
    }


    [Serializable]
    public class UserData
    {
        public string id;
        public string username;
        public string global_name;
        public string avatar;
        public int accent_color;
    }
}