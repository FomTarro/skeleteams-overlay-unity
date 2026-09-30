using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Discord
{
    public class DiscordIntegration : Integration<DiscordIntegration, DiscordIntegration.IntegrationData>
    {
        public override string FileName => throw new System.NotImplementedException();

        public override void Disable()
        {
            throw new System.NotImplementedException();
        }

        public override void Enable()
        {
            throw new System.NotImplementedException();
        }

        public override void FromSaveData(IntegrationData data)
        {
            throw new System.NotImplementedException();
        }

        public override void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public override IntegrationData ToSaveData()
        {
            throw new System.NotImplementedException();
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
        }
    }
}
