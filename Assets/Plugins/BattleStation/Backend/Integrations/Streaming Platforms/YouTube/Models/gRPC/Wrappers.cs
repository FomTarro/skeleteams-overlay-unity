using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.YouTube.GRPC
{
    [Serializable]
    public class LiveChatMessageWrapper
    {
        public string message;
        public string userName;
        public string userAvatarUrl;
        public string id;
    }
}
