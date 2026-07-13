using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace qtLib.Extension
{
    public static partial class qtGameExtension
    {
        public static List<T> AddAndReturn<T>(this List<T> origin, T value)
        {
            origin.Add(value);
            return origin;
        }

        public static T[] TakeSomeFromArray<T>(this IEnumerable<T> input, int amount, bool returnEmpty = true)
        {
            T[] array = input.ToArray();
            if (array.Length <= 0)
            {
                if (returnEmpty)
                {
                    return Array.Empty<T>();
                }
                else
                {
                    throw new ArgumentException("The input array is empty.");
                }
            }
            T[] output = new T[amount];
            for (int i = 0; i < amount; i++)
            {
                output[i] = array[Random.Range(0, array.Length)];
            }

            return output;
        }
     
        public static T TakeRandom<T>(this IEnumerable<T> input, Func<T, bool> predicate = null, bool returnEmpty = false)
        {
            List<T> temp;
            if (predicate != null)
            {
                temp = input.Where(predicate).ToList();
                if (temp.Count < 1)
                {
                    if (returnEmpty)
                    {
                        return default;
                    }
                    throw new ArgumentException("Collection must has at least 1 elements different from the original.");
                }
            }
            else
            {
                temp = input.ToList();
            }

            int randomIndex = Random.Range(0, temp.Count());
            
            return temp[randomIndex];
        }
        
        public static void ForEach<T>(this T[] array, Action<T> @action)
        {
            for (int i = 0; i < array.Length; i++)
            {
                @action?.Invoke(array[i]);
            }
        }
        
        /// <summary>
        /// Loops over all elements.
        /// </summary>
        /// <typeparam name="T">Elements type.</typeparam>
        /// <param name="enumerable">The enumerable.</param>
        /// <param name="action">What to do with each element?</param>
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var element in enumerable)
                action(element);
        }

        public static List<T> Clone<T>(this List<T> list) where T : class, Helper.ICloneable<T>
        {
            List<T> result = new List<T>();
            foreach (var cloneable in list)
            {
                result.Add(qtGameExtension.Clone(cloneable));
            }

            return result;
        }
      
        public static T[] Clone<T>(this T[] array) where T : class, Helper.ICloneable<T>
        {
            T[] result = new T[array.Length];
            for (var i = 0; i < array.Length; i++)
            {
                result[i] = array[i].Clone();
            }

            return result;
        }

        public static T Clone<T>(this T obj) where T : class, Helper.ICloneable<T>
        {
            if (obj == null)
            {
                return null;
            }
            return obj.Clone();
        }
        
        public static void AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            ICollection<KeyValuePair<TKey, TValue>> values)
        {
            values.ForEach(x => dictionary.Add(x.Key, x.Value));
        }
        
        public static void Shuffle<T>(this IList<T> list)
        {
            System.Random rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);        // random index từ 0 đến n
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}