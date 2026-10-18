using System;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    [Serializable]
    public class DiscordUser
    {
        public string displayName;
        public string id;
        public StreamImage avatar;
        public Color accentColor;

        public DiscordUser(string displayName, string id)
        {
            this.displayName = displayName;
            this.id = id;
        }
    }
}