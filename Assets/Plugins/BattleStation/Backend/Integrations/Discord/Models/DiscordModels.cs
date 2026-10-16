using System;

namespace Skeletom.BattleStation.Integrations.Discord.API
{
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