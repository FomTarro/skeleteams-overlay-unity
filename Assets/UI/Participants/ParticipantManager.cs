using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Skeletom.BattleStation.Integrations;
using Skeletom.BattleStation.Integrations.Discord;
using Skeletom.BattleStation.Integrations.OBS;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation
{
    public class ParticipantManager : Singleton<ParticipantManager>, ISaveable<ParticipantManager.Properties>
    {
        [SerializeField]
        private StreamImageHandler _imageHandler;
        public StreamImageHandler ImageHandler
        {
            get
            {
                if(_imageHandler == null)
                {
                    _imageHandler = GetComponent<StreamImageHandler>();
                    if(_imageHandler == null)
                    {
                        _imageHandler = gameObject.AddComponent<StreamImageHandler>();
                    }
                }
                return _imageHandler;
            }
        }

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
        [SerializeField]

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
            
        }

        private void Start()
        {
            FromSaveData(SaveDataManager.ReadSaveData(this));
            DiscordIntegration.Instance.onUserJoin.AddListener((user) =>
            {
                if (DiscordIntegration.Instance.SelfUserId != user.id)
                {
                    Debug.Log($"Participant {user.displayName} has joined.");
                }
            });
            DiscordIntegration.Instance.onUserLeave.AddListener((userId) =>
            {
                if (DiscordIntegration.Instance.SelfUserId != userId)
                {
                    Debug.Log($"Participant {userId} has quit.");
                }
            });
            DiscordIntegration.Instance.onUserSpeakStart.AddListener((userId) =>
            {
                if (DiscordIntegration.Instance.SelfUserId != userId)
                {
                    _micSources[$"discord_{userId}"] = 1.0f;
                }
            });
            DiscordIntegration.Instance.onUserSpeakStop.AddListener((userId) =>
            {
                if (DiscordIntegration.Instance.SelfUserId != userId)
                {
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
            if (!string.IsNullOrEmpty(source) && _micSources.ContainsKey(source))
            {
                return _micSources[source];
            }
            return 0.0f;
        }

        private void CreateParticipant(StreamUser user)
        {
            
        }

        #region I/O

        public void FromSaveData(Properties data)
        {
            foreach (Properties.Participant participant in data.participants)
            {
                StreamParticipant streamParticipant = new()
                {
                    metadata = participant,
                };
                DependencyManager dependencies = new(() =>
                {
                    _loadedParticipants.Add(streamParticipant);
                    if(streamParticipant.metadata.enabled){
                        Debug.LogWarning("PARTICIPANT JOIN A");
                        onParticipantJoin.Invoke(streamParticipant);
                    }
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
                    _imageHandler.GetFromLocal(participant.icon, $"participant_icon_${participant.key}",
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
                public string key;
                public string name;
                public string color;
                public string faceSource;
                public string screenSource;
                public string micSource;
                public string icon;
            }
        }
    }

    #endregion
}
