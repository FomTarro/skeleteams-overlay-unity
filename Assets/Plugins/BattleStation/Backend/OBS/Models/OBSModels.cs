using System;

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
        }
    }

    [Serializable]
    public class IdentifyData
    {
        public int rpcVersion = 1;
        public EventSubscriptionFlag eventSubscriptions = EventSubscriptionFlag.ALL_BASIC;
    }

    #endregion
}
