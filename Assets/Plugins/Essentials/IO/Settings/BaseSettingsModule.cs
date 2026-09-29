using UnityEngine;

namespace Skeletom.Essentials.IO
{
    public abstract class BaseSettingsModule<T> : MonoBehaviour where T : BaseSettingsData, new()
    {
        public abstract void ToSettingsData(T data);
        public abstract void FromSettingsData(T data);

        public void SaveSetting()
        {
            BaseSettingsManager<T>.Instance.SaveSettings();
        }
    }
}
