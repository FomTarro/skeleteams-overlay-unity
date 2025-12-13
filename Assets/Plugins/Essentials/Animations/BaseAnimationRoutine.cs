using System.Collections;
using UnityEngine;

namespace Skeletom.Essentials.Animations
{
    public abstract class BaseAnimationRoutine : ScriptableObject
    {
        public abstract IEnumerator Animate(GameObject target);
        public abstract void ResetDefaults();
    }
}
