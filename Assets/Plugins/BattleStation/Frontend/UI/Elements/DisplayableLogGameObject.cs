using Skeletom.BattleStation.Integrations;
using Skeletom.Essentials.Collections;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public abstract class DisplayableLogGameObject<T, D> : MonoBehaviour where T : DisplayableGameObject<D> where D : IDisplayableData
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
                    del.Dispose();
                    Pool.Retire(del);
                });
            }
            return _messages;
        }
    }
    public void Dispose(string dataId)
    {
        if (Messages.ContainsKey(dataId))
        {
            Messages.Remove(dataId);
        }
    }

    public T Display(D data)
    {
        T obj = Pool.GetNext();
        obj.Display(data);
        Messages.Add(data.ID, obj);
        return obj;
    }
}
