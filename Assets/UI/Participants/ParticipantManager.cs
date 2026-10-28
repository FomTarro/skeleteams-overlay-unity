using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Events;

public class ParticipantManager : Singleton<ParticipantManager>, ISaveable<ParticipantManager.Properties>
{
    [SerializeField]
    private StreamImageHandler _imageHandler;

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
    [SerializeField]
    private List<StreamParticipant> _activeParticipants = new();

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
                Debug.Log($"Participant {user.id} has joined.");
                CreateParticipantFromUser(user);
            }
        });
        IntegrationManager.Instance.Discord.onUserLeave.AddListener((userId) =>
        {
            if (IntegrationManager.Instance.Discord.SelfUserId != userId)
            {
                Debug.Log($"Participant {userId} has quit.");
                RemoveParticipant(userId);
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

    private void RemoveParticipant(string userId)
    {
        onParticipantLeave.Invoke(userId);
    }

    private void CreateParticipantFromUser(StreamUser user)
    {
        StreamParticipant overrideSettings = _loadedParticipants.Find(item => item.metadata.id == user.id);
        Properties.Participant metadata = new()
        {
            id = user.id,
            name = overrideSettings != null && !string.IsNullOrEmpty(overrideSettings.metadata.name) ? overrideSettings.metadata.name : user.displayName,
            color = overrideSettings != null && !string.IsNullOrEmpty(overrideSettings.metadata.color) ? overrideSettings.metadata.color : "#FEB3D0",
            faceSource = overrideSettings != null && !string.IsNullOrEmpty(overrideSettings.metadata.faceSource) ? overrideSettings.metadata.faceSource : $"{user.id}_face",
            screenSource = overrideSettings != null && !string.IsNullOrEmpty(overrideSettings.metadata.screenSource) ? overrideSettings.metadata.screenSource : $"{user.id}_screen",
            micSource = overrideSettings != null && !string.IsNullOrEmpty(overrideSettings.metadata.micSource) ? overrideSettings.metadata.micSource : $"discord_{user.id}",
            icon = overrideSettings != null && !string.IsNullOrEmpty(overrideSettings.metadata.icon) ? overrideSettings.metadata.icon : null
        };
        CreateParticipantFromMetadata(metadata, (success) =>
        {
            success.icon ??= user.avatar;
            _activeParticipants.Add(success);
            onParticipantJoin.Invoke(success);
        });
    }

    private void CreateParticipantFromMetadata(Properties.Participant participant, Action<StreamParticipant> onCreate)
    {
        StreamParticipant streamParticipant = new()
        {
            metadata = participant,
        };
        DependencyManager dependencies = new(() =>
        {
            onCreate(streamParticipant);
        });
        dependencies.AddDependency("create");
        if (!string.IsNullOrEmpty(participant.color))
        {
            Color borderColor = Color.clear;
            ColorUtility.TryParseHtmlString(participant.color, out borderColor);
            streamParticipant.borderColor = borderColor;
        }
        if (!string.IsNullOrEmpty(participant.icon))
        {
            dependencies.AddDependency("icon");
            _imageHandler.GetFromLocal(participant.icon, $"participant_icon_{participant.id}",
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
        dependencies.ResolveDependency("create");
    }

    #region I/O


    public void FromSaveData(Properties data)
    {
        foreach (Properties.Participant participant in data.participants)
        {
            CreateParticipantFromMetadata(participant, (success) =>
            {
                _loadedParticipants.Add(success);
                if (success.metadata.enabled)
                {
                    _activeParticipants.Add(success);
                    onParticipantJoin.Invoke(success);
                }
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
