using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Discord
{
    public class DiscordIntegration : Integration<DiscordIntegration, DiscordIntegration.IntegrationData>
    {
        public override string FileName => "discord.json";

        private const string CLIENT_ID = "TODO";
        private string USER_TOKEN = "NO_TOKEN_SET";

        #region Lifecycle

        public override void Disable()
        {
            throw new System.NotImplementedException();
        }

        public override void Enable()
        {
            throw new System.NotImplementedException();
        }

        public override void Initialize()
        {
            throw new System.NotImplementedException();
        }

        private void Update()
        {
        
        }

        #endregion

        #region Tokens

        public void RequestToken()
        {
            string userToken = "TODO";
            Application.OpenURL(userToken);
        }

        public void SetToken(string token)
        {
            USER_TOKEN = token;
        }

        private void ValidateToken(string token, Action<bool> onValidation)
        {
            onValidation(true);
        }

        #endregion

        #region File IO

        public override void FromSaveData(IntegrationData data)
        {
            ValidateToken(data.token, (isValid) =>
            {
                if (!isValid)
                {
                    RequestToken();
                }
                else
                {
                    SetToken(data.token);
                }
            });
        }

        public override IntegrationData ToSaveData()
        {
            return new IntegrationData()
            {
                token = USER_TOKEN
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
        }

        #endregion
    }
}
