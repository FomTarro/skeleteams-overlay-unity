using System;
using System.Collections.Generic;

namespace Skeletom.BattleStation.Integrations.OBS.Models
{
    #region Enums

    [Serializable]
    public enum OpCode : int
    {
        HELLO = 0,
        IDENTIFY = 1,
        IDENTIFIED = 2,
        REIDENTIFY = 3,
        EVENT = 5,
        REQUEST = 6,
        REQUEST_RESPONSE = 7,
        REQUEST_BATCH = 8,
        REQUEST_BATCH_RESPONSE = 9
    }

    [Serializable]
    public enum EventSubscriptionFlag : int
    {
        NONE = 0,
        GENERAL = 1 << 0,
        SCENES = 1 << 2,
        INPUTS = 1 << 3,
        TRANSITIONS = 1 << 4,
        FILTERS = 1 << 5,
        OUTPUTS = 1 << 6,
        SCENE_ITEMS = 1 << 7,
        MEDIA_INPUTS = 1 << 8,
        VENDORS = 1 << 9,
        UI = 1 << 10,
        ALL_BASIC = GENERAL | SCENES | INPUTS | TRANSITIONS | FILTERS | OUTPUTS | SCENE_ITEMS | MEDIA_INPUTS | VENDORS | UI,
        INPUT_VOLUME_METERS = 1 << 16,
        INPUT_ACTIVE_STATE_CHANGE = 1 << 17,
        INPUT_SHOW_STATE_CHANGE = 1 << 18,
        SCENE_ITEM_TRANSFORM_CHANGED = 1 << 19,
        ALL_HIGH_FREQUENCY = INPUT_VOLUME_METERS | INPUT_ACTIVE_STATE_CHANGE | INPUT_SHOW_STATE_CHANGE | SCENE_ITEM_TRANSFORM_CHANGED,
        ALL = ALL_BASIC | ALL_HIGH_FREQUENCY
    }

    #endregion

    #region Generic Message

    [Serializable]
    public class OBSMessage<T>
    {
        public OpCode op;
        public T d;
    }

    #endregion

    #region Generic Event

    [Serializable]
    public class OBSEvent<T>
    {
        public string eventType;
        public int eventIntent;
        public T eventData;
    }

    #endregion

    #region Generic Request

    [Serializable]
    public class OBSRequest<T>
    {
        public string requestType;
        public string requestId;
        public T requestData;

        public OBSRequest(string requestType, T data)
        {
            requestId = Guid.NewGuid().ToString();
            this.requestType = requestType;
            this.requestData = data;
        }
    }

    [Serializable]
    public class OBSRequestMessage<T> : OBSMessage<OBSRequest<T>>
    {
        public OBSRequestMessage(string requestType, T data)
        {
            op = OpCode.REQUEST;
            this.d = new OBSRequest<T>(requestType, data);

        }
    }

    #endregion

    #region Generic Response

    [Serializable]
    public struct OBSResponseStatus
    {
        public bool result;
        public int code;
        public string comment;
    }

    [Serializable]
    public class OBSResponse<T>
    {
        public string requestType;
        public string requestId;
        public OBSResponseStatus requestStatus;
        public T responseData;

        public OBSResponse(string requestType, T data)
        {
            requestId = Guid.NewGuid().ToString();
            this.requestType = requestType;
            this.responseData = data;
        }
    }

    [Serializable]
    public class OBSResponseMessage<T> : OBSMessage<OBSResponse<T>>
    {
        public OBSResponseMessage(string requestType, T data)
        {
            op = OpCode.REQUEST;
            this.d = new OBSResponse<T>(requestType, data);

        }
    }


    #endregion

    #region Hello Message

    public class HelloData
    {
        public string obsStudioVersion;
        public string obsWebSocketVersion;
        public int rpcVersion;
    }

    #endregion

    #region Identify Message 

    [Serializable]
    public class IdentifyMessage : OBSMessage<IdentifyData>
    {
        public IdentifyMessage()
        {
            op = OpCode.IDENTIFY;
            d = new IdentifyData();
        }
    }

    [Serializable]
    public class IdentifyData
    {
        public int rpcVersion = 1;
        public EventSubscriptionFlag eventSubscriptions = EventSubscriptionFlag.INPUT_VOLUME_METERS | EventSubscriptionFlag.ALL_BASIC;
    }

    #endregion

    #region Input Volume Change Event 

    [Serializable]
    public class InputVolumeChangedEventData
    {
        public string inputName;
        public string inputUuid;
        public string inputVolumeMul;
        public string inputVolumeDb;
    }

    #endregion

    #region InputVolumeMeters Event

    [Serializable]
    public class InputVolumeMeterEntry
    {
        public string inputName;
        public float[][] inputLevelsMul;
    }

    [Serializable]
    public class InputVolumeMetersEventData
    {
        public List<InputVolumeMeterEntry> inputs;
    }

    #endregion

    #region InputMuteStateChanged Event 

    [Serializable]
    public class InputMuteStateChangedEventData
    {
        public string inputName;
        public string inputUuid;
        public bool inputMuted;
    }

    #endregion

    #region Input Volume Request/Response

    [Serializable]
    public struct GetInputVolumeRequestData
    {
        public string inputName;
        public string inputUuid;
    }

    [Serializable]
    public class GetInputVolumeRequest : OBSRequestMessage<GetInputVolumeRequestData>
    {
        public GetInputVolumeRequest(GetInputVolumeRequestData data) : base("GetInputVolume", data)
        { }
    }

    [Serializable]
    public struct GetInputVolumeResponseData
    {
        public float inputVolumeMul;
        public float inputVolumeDb;
    }

    #endregion

    #region Stream Status Request/Response

    [Serializable]
    public class GetStreamStatusRequest : OBSRequestMessage<string>
    {
        public GetStreamStatusRequest() : base("GetStreamStatus", null)
        { }
    }

    [Serializable]
    public struct GetStreamStatusData
    {
        // milliseconds
        public int outputDuration;
    }

    #endregion


    #region Recording Request/Response

    [Serializable]
    public class GetRecordingStatusRequest : OBSRequestMessage<string>
    {
        public GetRecordingStatusRequest() : base("GetRecordStatus", null)
        { }
    }

    [Serializable]
    public struct GetRecordingStatusData
    {
        // milliseconds
        public int outputDuration;
    }

    #endregion


    #region Wrapper Models

    #endregion
}
