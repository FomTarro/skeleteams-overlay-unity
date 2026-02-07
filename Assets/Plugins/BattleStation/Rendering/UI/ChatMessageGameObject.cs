using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations;
using UnityEngine;

public abstract class ChatMessageGameObject : MonoBehaviour
{
    public abstract void DisplayMessage(StreamChatMessage message);
}
