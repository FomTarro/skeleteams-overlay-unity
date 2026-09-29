using System;
using System.Collections;

namespace Skeletom.Essentials.Utils
{
    public static class EnumUtils
    {
        /// <summary>
        /// Combine any number of IEnumerators into a single IEnumerator.
        /// 
        /// Useful for only requiring one yield for many discrete routines.
        /// </summary>
        /// <param name="enumerators">Any number of IEnumerators</param>
        /// <returns></returns>
        public static IEnumerator CombineEnumerators(Action onAllComplete, params IEnumerator[] enumerators)
        {
            object[] nextObjects = new object[enumerators.Length];
            bool shouldLoop;
            do
            {
                shouldLoop = false;
                for (int i = 0; i < nextObjects.Length; i++)
                {
                    bool hasNext = enumerators[i].MoveNext();
                    nextObjects[i] = hasNext ? enumerators[i].Current : null;
                    shouldLoop |= hasNext;
                }
                yield return nextObjects;
            }
            while (shouldLoop);
            onAllComplete();
        }

        /// <summary>
        /// A WaitForSeconds implementation that can be interrupted at any frame.
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        public static IEnumerator GenericWaitForSeconds(float seconds, Action<float> onTick = null)
        {
            float t = 0f;
            do
            {
                t += UnityEngine.Time.deltaTime;
                onTick?.Invoke(t / seconds);
                yield return null;
            }
            while (t < seconds);
        }
    }
}