using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Events;

public class ParticipantManager : Singleton<ParticipantManager>, ISaveable<ParticipantManager.Properties>
{
    [SerializeField]
    private StreamImageHandler _imageHandler;

    [SerializeField]
    private WebServer _webServer;

    public string FileFolder => "Participants";
    public string FileName => "metadata.json";

    [Serializable]
    public class StreamParticipant
    {
        public Properties.Participant metadata;
        public Color borderColor;
        public StreamImage icon;
    }

    [SerializeField]
    private List<StreamParticipant> _loadedParticipants = new();

    private readonly Dictionary<string, float> _micSources = new();

    #region Events

    [Serializable]
    public class ParticipantJoinEvent : UnityEvent<StreamParticipant> { }
    public ParticipantJoinEvent onParticipantJoin = new();

    [Serializable]
    public class ParticipantLeaveEvent : UnityEvent<string> { }
    public ParticipantLeaveEvent onParticipantLeave = new();

    #endregion

    public override void Initialize()
    {
        FromSaveData(SaveDataManager.ReadSaveData(this));
        IntegrationManager.Instance.Discord.onUserJoin.AddListener((user) =>
        {
            if (IntegrationManager.Instance.Discord.SelfUserId != user.id)
            {
                Debug.Log($"Participant {user.displayName} has joined.");
                CreateParticipant(user);
            }
        });
        IntegrationManager.Instance.Discord.onUserLeave.AddListener((userId) =>
        {
            if (IntegrationManager.Instance.Discord.SelfUserId != userId)
            {
                Debug.Log($"Participant {userId} has quit.");
            }
        });
        IntegrationManager.Instance.Discord.onUserSpeakStart.AddListener((userId) =>
        {
            _micSources[$"discord_{userId}"] = 1.0f;
        });
        IntegrationManager.Instance.Discord.onUserSpeakStop.AddListener((userId) =>
        {
            _micSources[$"discord_{userId}"] = 0.0f;
        });
        IntegrationManager.Instance.OBS.onVolume.AddListener((source, vol) =>
        {
            _micSources[$"obs_{source}"] = vol;
        });
    }

    public float GetMicSourceVolume(string source)
    {
        if (!string.IsNullOrEmpty(source) && _micSources.ContainsKey(source))
        {
            return _micSources[source];
        }
        return 0.0f;
    }

    private void CreateParticipant(StreamUser user)
    {
        // StreamParticipant overrideSettings = _loadedParticipants.Find(item => user.id == item.metadata.id);

        // public bool enabled;
        // public string id;
        // public string name;
        // public string color;
        // public string faceSource;
        // public string screenSource;
        // public string micSource;
        // public string icon;        
        Properties.Participant metadata = new(){
            id=user.id,
            name=user.displayName,
            color="#FEB3D0",
            faceSource=$"{user.id}_face",
            screenSource=$"{user.id}_screen",
            micSource=$"discord_{user.id}",
        };
        CreateParticipant(metadata, (success) =>
        {
            success.icon = user.avatar;
            _loadedParticipants.Add(success);
            onParticipantJoin.Invoke(success);
        });
    }

    private void CreateParticipant(Properties.Participant participant, Action<StreamParticipant> onCreate)
    {
        StreamParticipant streamParticipant = new()
        {
            metadata = participant,
        };
        DependencyManager dependencies = new(() =>
        {
            onCreate(streamParticipant);
        });
        if (!string.IsNullOrEmpty(participant.color))
        {
            Color borderColor = Color.clear;
            ColorUtility.TryParseHtmlString(participant.color, out borderColor);
            streamParticipant.borderColor = borderColor;
        }
        if (!string.IsNullOrEmpty(participant.icon))
        {
            dependencies.AddDependency("icon");
            _imageHandler.GetFromLocal(participant.icon, $"participant_icon_${participant.id}",
            (img) =>
            {
                streamParticipant.icon = img;
                dependencies.ResolveDependency("icon");
            },
            (err) =>
            {
                dependencies.ResolveDependency("icon");
            });
        }
        dependencies.Enable(true);
    }

    #region I/O


    public void FromSaveData(Properties data)
    {
        foreach (Properties.Participant participant in data.participants)
        {
            CreateParticipant(participant, (success) =>
            {
                _loadedParticipants.Add(success);
                onParticipantJoin.Invoke(success);
            });
        }
    }

    public Properties ToSaveData()
    {
        return new Properties();
    }

    [Serializable]
    public class Properties : BaseSaveData
    {
        public List<Participant> participants;

        [Serializable]
        public struct Participant
        {
            public bool enabled;
            public string id;
            public string name;
            public string color;
            public string faceSource;
            public string screenSource;
            public string micSource;
            public string icon;
        }
    }
    #endregion
}
