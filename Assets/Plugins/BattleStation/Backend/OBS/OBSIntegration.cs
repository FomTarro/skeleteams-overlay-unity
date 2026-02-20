using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.OBS.Models;
using Skeletom.Essentials.IO;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.OBS {
    public class OBSIntegration : Integration<OBSIntegration, OBSIntegration.IntegrationData>
    {
        public override string FileName => "obs.json";

        private readonly WebSocket _socket = new();

        private readonly Dictionary<string, Action<string>> EVENT_HANDLERS = new();

        #region Lifecycle

        public override void Disable()
        {
            
        }

        public override void Enable()
        {
            
        }

        public override void Initialize()
        {
            FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        }

        private void Update()
        {
            _socket.Tick(Time.deltaTime);
            string data = null;
            do
            {
                if (_socket != null)
                {
                    data = _socket.GetNextResponse();
                    if (data != null)
                    {
                        ProcessSocketMessage(data);
                    }
                }
            } while (data != null);
        }

        private void ProcessSocketMessage(string msg)
        {
            try
            {
                Debug.Log(msg);
                OBSMessage<string> message = JsonUtility.FromJson<OBSMessage<string>>(msg);
                if (OpCode.HELLO == message.op)
                {
                    _socket.Send(JsonUtility.ToJson(new IdentifyMessage()));
                }
                else if (OpCode.EVENT == message.op)
                {
                    OBSMessage<OBSEvent<string>> eventMessage = JsonUtility.FromJson<OBSMessage<OBSEvent<string>>>(msg);
                    if (EVENT_HANDLERS.ContainsKey(eventMessage.d.eventType))
                    {
                        EVENT_HANDLERS[eventMessage.d.eventType](msg);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        #endregion

        #region Subscriptions

        private void SubscribeToEvent<T>()
        {
            
        }

        #endregion

        #region  File I/O

        public override void FromSaveData(IntegrationData data)
        {
            _socket.Start($"{data.url}:{data.port}", 
                () =>
                {
                    Debug.Log("OBS Socket connected!");
                },
                () =>
                {
                    Debug.Log("OBS Socket disconnected.");
                },
                (err) =>
                {
                    Debug.LogError($"OBS Socket error: {err}");
                }
            );
        }

        public override IntegrationData ToSaveData()
        {
            return new IntegrationData()
            {
                
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string url = "ws://localhost";
            public string password;
            public int port = 4455;
        }

        #endregion
    }
}
