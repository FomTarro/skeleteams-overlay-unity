using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations;
using Skeletom.Essentials.Collections;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public abstract class ChatLogGameObject<T> : MonoBehaviour where T : ChatMessageGameObject
{
    private ObjectPool<T> _pool;
    protected ObjectPool<T> Pool
    {
        get
        {
            if (_pool == null)
            {
                _pool = new ObjectPool<T>(_prefab, _maxMessageHistory, _prefabPoolParent);
            }
            return _pool;
        }
    }
    [SerializeField]
    private Transform _prefabPoolParent;
    [SerializeField]
    private T _prefab;
    [SerializeField]
    private int _maxMessageHistory = 100;

    private LRUDictionary<string, T> _messages;
    protected LRUDictionary<string, T> Messages
    {
        get
        {
            if (_messages == null)
            {
                _messages = new LRUDictionary<string, T>(_maxMessageHistory, (del) =>
                {
                    del.DisposeMessage();
                    Pool.Retire(del);
                });
            }
            return _messages;
        }
    }
    public void DisposeMessage(StreamChatMessageDeletion message)
    {
        if (Messages.ContainsKey(message.id))
        {
            Messages.Remove(message.id);
        }
    }

    public T DisplayMessage(StreamChatMessage message)
    {
        T obj = Pool.GetNext();
        obj.DisplayMessage(message);
        Messages.Add(message.id, obj);
        return obj;
    }
}
