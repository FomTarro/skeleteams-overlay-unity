using UnityEngine;

namespace Skeletom.BattleStation {
    public abstract class Layout : MonoBehaviour
    {
        public abstract string LayoutName { get; }
        public abstract void Enable();
        public abstract void Disable();
    }
}
