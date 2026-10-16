using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Discord;
using Skeletom.BattleStation.Integrations.OBS;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public class ParticipantManager : Singleton<ParticipantManager>, ISaveable<ParticipantManager.ParticipantListMetadata>
{
    [SerializeField]
    private WebServer _webServer;
    
    [SerializeField]
    private StreamParticipantDisplay _prefab;

    public string FileFolder => "Participants";

    public string FileName => "metadata.json";

    [Serializable]
    public class ParticipantListMetadata : BaseSaveData
    {
        public List<ParticipantMetadata> participants;

        [Serializable]
        public struct ParticipantMetadata
        {
            
        }
    }

    private readonly Dictionary<string, float> _micSources = new();

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
                _micSources[$"discord_{user}"] = status ? 1.0f : 0.0f;
                Debug.Log($"Participant {user} has {(status ? "started" : "stopped")} speaking.");
            }
        });
        OBSIntegration.Instance.onVolume.AddListener((source, vol) =>
        {
            _micSources[$"obs_{source}"] = vol;
        });
    }

    public float GetMicSourceVolume(string source)
    {
        if (_micSources.ContainsKey(source))
        {
            return _micSources[source];
        }
        return 0.0f;
    }

    public void FromSaveData(ParticipantListMetadata data)
    {
        foreach(ParticipantListMetadata.ParticipantMetadata participant in data.participants)
        {
            
        }
    }

    public ParticipantListMetadata ToSaveData()
    {
        return new ParticipantListMetadata();
    }
}
