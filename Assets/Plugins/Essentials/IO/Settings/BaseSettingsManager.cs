using Skeletom.Essentials.Lifecycle;
using UnityEngine;

namespace Skeletom.Essentials.IO
{
    public abstract class BaseSettingsManager<T> : Singleton<BaseSettingsManager<T>>, ISaveable<T> where T : BaseSettingsData, new()
    {
        public virtual string FileFolder => "Settings";

        public virtual string FileName => "settings.json";

        public bool IsLoading { get; private set; }

        public void SaveSettings()
        {
            if (!IsLoading)
            {
                SaveDataManager.Instance.WriteSaveData(this);
                LoadSettings();
            }
        }

        public void LoadSettings()
        {
            if (!IsLoading)
            {
                FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
            }
        }

        public void FromSaveData(T data)
        {
            IsLoading = true;
            var settingsModules = FindObjectsByType<BaseSettingsModule<T>>(FindObjectsSortMode.None);
            foreach (BaseSettingsModule<T> module in settingsModules)
            {
                module.FromSettingsData(data);
            }
            IsLoading = false;
        }

        public T ToSaveData()
        {
            T data = new();
            var settingsModules = FindObjectsByType<BaseSettingsModule<T>>(FindObjectsSortMode.None);
            foreach (BaseSettingsModule<T> module in settingsModules)
            {
                module.ToSettingsData(data);
            }
            return data;
        }

        public virtual string TransformAfterRead(string content)
        {
            return content;
        }

        public virtual string TransformBeforeWrite(string content)
        {
            return content;
        }
    }
}