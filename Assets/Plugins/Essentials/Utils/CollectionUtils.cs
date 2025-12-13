using System;
using System.Collections.Generic;
using System.Linq;
using Skeletom.Essentials.Collections;

namespace Skeletom.Essentials.Utils
{
    public static class CollectionUtils
    {
        private static LRUDictionary<Type, Array> _enumCache = new LRUDictionary<Type, Array>(5, null);
        private static readonly Random RANDOM = new Random();

        public static LinkedList<T> Shuffle<T>(LinkedList<T> source)
        {
            LinkedList<T> result = new LinkedList<T>();
            int[] indicies = Enumerable.Range(0, source.Count).ToArray();

            ShuffleArray(indicies);
            foreach (int choice in indicies)
            {
                result.AddLast(ElementAt(source, choice));
            }

            return result;
        }

        public static T[] ShuffleArray<T>(T[] array)
        {
            for (int i = array.Length; i > 1; i--)
            {
                int j = RANDOM.Next(i);
                if (i - 1 != j)
                {
                    T t = array[i - 1];
                    array[i - 1] = array[j];
                    array[j] = t;
                }
            }
            return array;
        }

        public static T ElementAt<T>(LinkedList<T> source, int index)
        {
            LinkedListNode<T> current = source.First;
            while (index-- > 0)
            {
                current = current.Next;
            }
            return current.Value;
        }

        public static T SelectRandom<T>(params T[] values)
        {
            if (values.Length > 0)
            {
                ShuffleArray<T>(values);
                return values[0];
            }
            throw new IndexOutOfRangeException("Supplied array contained 0 items");
        }

        /// <summary>
        /// Caching method for storing static lists of compiled enum values
        /// </summary>
        /// <typeparam name="T">The enum type to get the list of valeus for</typeparam>
        /// <returns></returns>
        public static T[] GetEnumValues<T>() where T : Enum
        {
            if (!_enumCache.ContainsKey(typeof(T)))
            {
                _enumCache.Add(typeof(T), Enum.GetValues(typeof(T)));
            }
            return _enumCache[typeof(T)] as T[];
        }
    }
}
