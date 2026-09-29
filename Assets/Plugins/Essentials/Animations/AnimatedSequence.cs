using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Skeletom.Essentials.Animations
{
    public class AnimatedSequence : BaseAnimatedElement
    {
        [System.Serializable]
        public struct AnimationRoutineItem
        {
            public BaseAnimationRoutine routine;
            public GameObject target;
        }

        [SerializeField]
        private List<AnimationRoutineItem> _sequence = new List<AnimationRoutineItem>();

        protected override IEnumerator Animate()
        {
            foreach (AnimationRoutineItem item in this._sequence)
            {
                IEnumerator anim = item.routine.Animate(item.target);
                do
                {
                    yield return anim.Current;
                } while (anim.MoveNext());
            }
        }

        protected override void InitializeImplementation()
        {

        }

        protected override void ResetImplementation()
        {

        }
    }
}
