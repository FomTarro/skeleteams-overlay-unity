using System;
using Skeletom.Essentials.IO;

public class ExampleSettingsManager : BaseSettingsManager<ExampleSettingsManager.ExampleSettingsData>
{
    public override void Initialize()
    {
        FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        SaveDataManager.Instance.WriteSaveData(this);
    }

    [Serializable]
    public class ExampleSettingsData : BaseSettingsData
    {
        public string propA;
        public string propB;
    }
}
