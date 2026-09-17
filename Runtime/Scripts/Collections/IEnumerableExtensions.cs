using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Abb2kTools.Collections
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Returns a uniformly random element from the collection. Returns default(T) if the collection is empty.
        /// </summary>
        public static T GetRandom<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null || !enumerable.Any())
                return default;

            if (enumerable is IList<T> list)
            {
                return list[Random.Range(0, list.Count)];
            }

            int count = enumerable.Count();
            return enumerable.ElementAt(Random.Range(0, count));
        }

        /// <summary>
        /// Returns a uniformly random element from the collection within the index boundaries of the given Ranged. Returns default(T) if the collection is empty.
        /// </summary>
        public static T GetRandom<T>(this IEnumerable<T> enumerable, Ranged indexRange)
        {
            if (enumerable == null || !enumerable.Any())
                return default;

            int count = enumerable.Count();
            int minIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.min), 0, count - 1);
            int maxIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.max), 0, count - 1);

            if (minIndex > maxIndex)
            {
                (minIndex, maxIndex) = (maxIndex, minIndex);
            }

            int randomIndex = Random.Range(minIndex, maxIndex + 1);

            if (enumerable is IList<T> list)
            {
                return list[randomIndex];
            }

            return enumerable.ElementAt(randomIndex);
        }

        /// <summary>
        /// Returns a random element from the collection, where the chance of being picked is determined by the provided weight function. Returns default(T) if the collection is empty.
        /// </summary>
        public static T GetRandom<T>(this IEnumerable<T> enumerable, System.Func<T, float> weightSelector)
        {
            if (enumerable == null || !enumerable.Any())
                return default;

            float totalWeight = 0f;
            foreach (var item in enumerable)
            {
                float weight = weightSelector(item);
                if (weight > 0)
                    totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return enumerable.GetRandom();

            float randomVal = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var item in enumerable)
            {
                float weight = weightSelector(item);
                if (weight <= 0) continue;

                currentWeight += weight;
                if (currentWeight >= randomVal)
                {
                    return item;
                }
            }

            return enumerable.Last();
        }

        /// <summary>
        /// Returns a weighted random element from the collection within the index boundaries of the given Ranged. Returns default(T) if the collection is empty.
        /// </summary>
        public static T GetRandom<T>(this IEnumerable<T> enumerable, Ranged indexRange, System.Func<T, float> weightSelector)
        {
            if (enumerable == null || !enumerable.Any())
                return default;

            int count = enumerable.Count();
            int minIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.min), 0, count - 1);
            int maxIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.max), 0, count - 1);

            if (minIndex > maxIndex)
            {
                (minIndex, maxIndex) = (maxIndex, minIndex);
            }

            int rangeCount = maxIndex - minIndex + 1;
            IEnumerable<T> subRange = enumerable.Skip(minIndex).Take(rangeCount);

            return subRange.GetRandom(weightSelector);
        }

        /// <summary>
        /// Returns an array of uniformly random elements from the collection. Returns an empty array if the collection is empty.
        /// </summary>
        public static T[] GetRandomMany<T>(this IEnumerable<T> enumerable, int count, bool unique = false)
        {
            if (enumerable == null || !enumerable.Any() || count <= 0)
                return new T[0];

            IList<T> list = enumerable as IList<T> ?? enumerable.ToList();

            if (unique)
            {
                int actualCount = Mathf.Min(count, list.Count);
                T[] result = new T[actualCount];
                List<T> pool = new List<T>(list);

                for (int i = 0; i < actualCount; i++)
                {
                    int randomIndex = Random.Range(0, pool.Count);
                    result[i] = pool[randomIndex];
                    
                    // Swap with the last element for an O(1) removal
                    pool[randomIndex] = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                }
                return result;
            }
            else
            {
                T[] result = new T[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = list[Random.Range(0, list.Count)];
                }
                return result;
            }
        }

        /// <summary>
        /// Returns an array of uniformly random elements from the collection within the index boundaries of the given Ranged. Returns an empty array if the collection is empty.
        /// </summary>
        public static T[] GetRandomMany<T>(this IEnumerable<T> enumerable, int count, Ranged indexRange, bool unique = false)
        {
            if (enumerable == null || !enumerable.Any() || count <= 0)
                return new T[0];

            int totalCount = enumerable.Count();
            int minIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.min), 0, totalCount - 1);
            int maxIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.max), 0, totalCount - 1);

            if (minIndex > maxIndex)
            {
                (minIndex, maxIndex) = (maxIndex, minIndex);
            }

            int rangeCount = maxIndex - minIndex + 1;
            IEnumerable<T> subRange = enumerable.Skip(minIndex).Take(rangeCount);

            return subRange.GetRandomMany(count, unique);
        }

        /// <summary>
        /// Returns an array of random elements from the collection, where the chance of being picked is determined by the provided weight function. Returns an empty array if the collection is empty.
        /// </summary>
        public static T[] GetRandomMany<T>(this IEnumerable<T> enumerable, int count, System.Func<T, float> weightSelector, bool unique = false)
        {
            if (enumerable == null || !enumerable.Any() || count <= 0)
                return new T[0];

            if (unique)
            {
                List<T> pool = enumerable.ToList();
                int actualCount = Mathf.Min(count, pool.Count);
                T[] result = new T[actualCount];

                for (int i = 0; i < actualCount; i++)
                {
                    // Reuse the original weighted single-pick function
                    T picked = pool.GetRandom(weightSelector); 
                    result[i] = picked;
                    pool.Remove(picked);
                }
                return result;
            }
            else
            {
                IList<T> list = enumerable as IList<T> ?? enumerable.ToList();
                T[] result = new T[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = list.GetRandom(weightSelector);
                }
                return result;
            }
        }

        /// <summary>
        /// Returns an array of weighted random elements from the collection within the index boundaries of the given Ranged. Returns an empty array if the collection is empty.
        /// </summary>
        public static T[] GetRandomMany<T>(this IEnumerable<T> enumerable, int count, Ranged indexRange, System.Func<T, float> weightSelector, bool unique = false)
        {
            if (enumerable == null || !enumerable.Any() || count <= 0)
                return new T[0];

            int totalCount = enumerable.Count();
            int minIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.min), 0, totalCount - 1);
            int maxIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.max), 0, totalCount - 1);

            if (minIndex > maxIndex)
            {
                (minIndex, maxIndex) = (maxIndex, minIndex);
            }

            int rangeCount = maxIndex - minIndex + 1;
            IEnumerable<T> subRange = enumerable.Skip(minIndex).Take(rangeCount);

            return subRange.GetRandomMany(count, weightSelector, unique);
        }

        /// <summary>
        /// Randomizes the order of the collection uniformly (Fisher-Yates shuffle).
        /// </summary>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null || !enumerable.Any())
                return new T[0];

            List<T> list = enumerable.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }

            return list;
        }

        /// <summary>
        /// Randomizes the order of the collection based on a weight function.
        /// </summary>
        /// <param name="highestWeightFirst">If true, higher weighted items are more likely to appear at the front of the collection. If false, they are more likely to appear at the back.</param>
        public static IEnumerable<T> ShuffleWeighted<T>(this IEnumerable<T> enumerable, System.Func<T, float> weightSelector, bool highestWeightFirst = true)
        {
            if (enumerable == null || !enumerable.Any())
                return new T[0];

            // Reusing the unique GetRandomMany automatically gives us a weighted shuffle
            // where higher weights are picked earlier.
            int count = enumerable.Count();
            IEnumerable<T> shuffled = enumerable.GetRandomMany(count, weightSelector, unique: true);

            if (!highestWeightFirst)
            {
                return shuffled.Reverse();
            }

            return shuffled;
        }
    }
}