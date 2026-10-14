using System;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Discord.API
{
    [Serializable]
    public class UserData
    {
        public string id;
        public string username;
        public string global_name;
    }

    [Serializable]
    public class VoiceStateData
    {
        public UserData user;
    }
}