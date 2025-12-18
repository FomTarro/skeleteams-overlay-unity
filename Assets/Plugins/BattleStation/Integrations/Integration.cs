using System;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;

public abstract class Integration<T, K> : Singleton<T>, ISaveable<K> where T : Integration<T, K> where K : BaseSaveData
{
    public string FileFolder => "Integrations";

    public abstract string FileName { get; }

    public abstract void GetToken(Action<string> onSuccess, Action onError);

    public abstract void FromSaveData(K data);

    public abstract K ToSaveData();
}
