using UnityEngine;

public abstract class DisplayableGameObject<T> : MonoBehaviour
{
    public abstract void Display(T data);

    public abstract void Dispose();
}
