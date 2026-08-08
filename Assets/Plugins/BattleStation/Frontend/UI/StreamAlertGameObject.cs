using UnityEngine;

public abstract class StreamAlertGameObject<T> : MonoBehaviour
{
    public abstract void DisplayAlert(T message);

    public abstract void DisposeAlert();
}
