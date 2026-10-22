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
        DiscordIntegration.Instance.onUserJoin.AddListener((user) =>
        {
            if(DiscordIntegration.Instance.SelfUserId != user.id){
                Debug.Log($"Participant {user.displayName} has joined.");
            }
        });
        DiscordIntegration.Instance.onUserLeave.AddListener((userId) =>
        {
            if(DiscordIntegration.Instance.SelfUserId != userId){
                Debug.Log($"Participant {userId} has quit.");
            }
        });
        DiscordIntegration.Instance.onUserSpeakStart.AddListener((userId) =>
        {
            if(DiscordIntegration.Instance.SelfUserId != userId){
                _micSources[$"discord_{userId}"] = 1.0f;
            }
        });
        DiscordIntegration.Instance.onUserSpeakStop.AddListener((userId) =>
        {
            if(DiscordIntegration.Instance.SelfUserId != userId){
                _micSources[$"discord_{userId}"] = 0.0f;
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
