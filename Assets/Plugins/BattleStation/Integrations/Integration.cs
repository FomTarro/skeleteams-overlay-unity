using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;

public abstract class Integration<T, K> : Singleton<T>, ISaveable<K> where T : Integration<T, K> where K : BaseSaveData
{
    public string FileFolder => "Integrations";

    public string FileName => throw new System.NotImplementedException();

    public abstract void FromSaveData(K data);

    public abstract K ToSaveData();
}
