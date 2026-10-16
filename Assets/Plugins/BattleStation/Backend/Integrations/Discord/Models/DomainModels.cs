using System;

namespace Skeletom.BattleStation.Integrations
{
    [Serializable]
    public class DiscordUser
    {
        public string displayName;
        public string id;
        public StreamImage avatar;

        public DiscordUser(string displayName, string id)
        {
            this.displayName = displayName;
            this.id = id;
        }
    }
}