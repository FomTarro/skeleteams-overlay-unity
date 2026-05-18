using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Skeletom.BattleStation.Integrations.OBS.Models;
using Skeletom.Essentials.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.BattleStation.Integrations.OBS
{
    public class OBSIntegration : Integration<OBSIntegration, OBSIntegration.IntegrationData>
    {
        public override string FileName => "obs.json";

        #region Events

        [Serializable]
        public class VolumeEvent : UnityEvent<string, float> { }
        public VolumeEvent onVolume = new();

        #endregion

        private readonly WebSocket _socket = new();

        private readonly Dictionary<string, Action<string>> EVENT_HANDLERS = new();

        private readonly Dictionary<string, Action<string>> REQUEST_HANDLERS = new();

        #region Lifecycle

        public override void Disable()
        {

        }

        public override void Enable()
        {

        }

        public override void Initialize()
        {
            // TODO: have these update an object reference that gets polled, because muting and volume are handled by separate events
            EVENT_HANDLERS.Add("InputVolumeMeters", (msg) =>
            {
                var eventMessage = JsonConvert.DeserializeObject<OBSMessage<OBSEvent<InputVolumeMetersEventData>>>(msg);
                foreach (InputVolumeMeterEntry input in eventMessage.d.eventData.inputs)
                {
                    if (input.inputLevelsMul.Length > 1 && input.inputLevelsMul[0].Length > 2 && input.inputLevelsMul[1].Length > 2)
                    {
                        onVolume.Invoke(input.inputName, (input.inputLevelsMul[0][2] + input.inputLevelsMul[1][2]) / 2f);
                    }
                }
            });
            EVENT_HANDLERS.Add("InputMuteStateChanged", (msg) =>
            {
                var eventMessage = JsonConvert.DeserializeObject<OBSMessage<OBSEvent<InputMuteStateChangedEventData>>>(msg);
                if ("Mic/Aux".Equals(eventMessage.d.eventData.inputName))
                {
                    Debug.Log(eventMessage.d.eventData.inputMuted);
                    // Debug.Log("Mic Volume: " + micInput.inputLevelsMul[0, 2]);
                }

            });
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
                else if (OpCode.REQUEST_RESPONSE == message.op)
                {
                    OBSResponseMessage<string> responseMessage = JsonUtility.FromJson<OBSResponseMessage<string>>(msg);
                    if (REQUEST_HANDLERS.ContainsKey(responseMessage.d.requestId))
                    {
                        var handler = REQUEST_HANDLERS[responseMessage.d.requestId];
                        REQUEST_HANDLERS.Remove(responseMessage.d.requestId);
                        handler(msg);
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

        #endregion

        #region Requests

        public void GetInputVolume(string source, Action<GetInputVolumeResponseData> onSuccess, Action<string> onError)
        {
            var data = new GetInputVolumeRequestData();
            data.inputName = source;
            var request = new GetInputVolumeRequest(data);
            REQUEST_HANDLERS.Add(request.d.requestId, (msg) =>
            {
                var response = JsonUtility.FromJson<OBSResponseMessage<GetInputVolumeResponseData>>(msg);
                if (response.d.requestStatus.result == true)
                {
                    onSuccess(response.d.responseData);
                }
                else
                {
                    onError(response.d.requestStatus.comment);
                }
            });
            _socket.Send(JsonUtility.ToJson(request));
        }

        public void GetRecordingStatus(Action<GetRecordingStatusData> onSuccess, Action<string> onError)
        {
            var request = new GetRecordingStatusRequest();
            REQUEST_HANDLERS.Add(request.d.requestId, (msg) =>
            {
                var response = JsonUtility.FromJson<OBSResponseMessage<GetRecordingStatusData>>(msg);
                if (response.d.requestStatus.result == true)
                {
                    onSuccess(response.d.responseData);
                }
                else
                {
                    onError(response.d.requestStatus.comment);
                }
            });
            _socket.Send(JsonUtility.ToJson(request));
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
