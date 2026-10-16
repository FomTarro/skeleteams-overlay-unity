using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Discord;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public class ParticipantManager : Singleton<ParticipantManager>
{
    public override void Initialize()
    {
        DiscordIntegration.Instance.onUserJoin.AddListener((user, status) =>
        {
            if(DiscordIntegration.Instance.SelfUserId != user){
                Debug.Log($"Participant {user} has {(status ? "joined" : "quit")}.");
            }
        });
        DiscordIntegration.Instance.onUserSpeak.AddListener((user, status) =>
        {
            if(DiscordIntegration.Instance.SelfUserId != user){
                Debug.Log($"Participant {user} has {(status ? "started" : "stopped")} speaking.");
            }
        });
    }
}
