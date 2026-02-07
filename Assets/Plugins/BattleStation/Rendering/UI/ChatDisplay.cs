using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Collections;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public abstract class ChatDisplay<T> : MonoBehaviour where T : IChatMessage
{
    private LRUDictionary<string, IChatMessage> _messages;
    // Start is called before the first frame update
    public void DeleteMessage(string id)
    {
        
    }
}
