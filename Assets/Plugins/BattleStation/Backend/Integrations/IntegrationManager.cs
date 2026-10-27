using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Discord;
using Skeletom.BattleStation.Integrations.OBS;
using Skeletom.BattleStation.Integrations.Twitch;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    public class IntegrationManager : Singleton<IntegrationManager>
    {
        [Header("Streaming Platforms")]
        [SerializeField]
        private TwitchIntegration _twitch;
        public TwitchIntegration Twitch { get { return _twitch; } }

        [Header("Other Software")]
        [SerializeField]
        private DiscordIntegration _discord;
        public DiscordIntegration Discord { get { return _discord; } }

        [SerializeField]
        private OBSIntegration _obs;
        public OBSIntegration OBS { get { return _obs; } }

        public override void Initialize()
        {
            _twitch.Enable();
            _obs.Enable();
            _discord.Enable();
        }
    }
}