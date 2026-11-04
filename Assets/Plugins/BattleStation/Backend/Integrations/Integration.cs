using Skeletom.Essentials.IO;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    public abstract class Integration<K> : MonoBehaviour, ISaveable<K> where K : BaseSaveData
    {
        public string FileFolder => "Integrations";

        public abstract string FileName { get; }

        public abstract void FromSaveData(K data);
        public abstract K ToSaveData();

        public abstract void Enable();
        public abstract void Disable();
    }
}
