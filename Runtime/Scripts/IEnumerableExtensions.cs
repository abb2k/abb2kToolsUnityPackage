using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Abb2kTools
{
public static class IEnumerableExtensions
{
    /// <summary>
    /// Returns a uniformly random element from the collection.
    /// </summary>
    public static T GetRandom<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable == null || !enumerable.Any())
            throw new System.ArgumentException("Cannot pick a random element from an empty or null collection.");

        if (enumerable is IList<T> list)
        {
            return list[Random.Range(0, list.Count)];
        }

        int count = enumerable.Count();
        return enumerable.ElementAt(Random.Range(0, count));
    }

    /// <summary>
    /// Returns a uniformly random element from the collection within the index boundaries of the given Ranged.
    /// </summary>
    public static T GetRandom<T>(this IEnumerable<T> enumerable, Ranged indexRange)
    {
        if (enumerable == null || !enumerable.Any())
            throw new System.ArgumentException("Cannot pick a random element from an empty or null collection.");

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
    /// Returns a random element from the collection, where the chance of being picked is determined by the provided weight function.
    /// </summary>
    public static T GetRandom<T>(this IEnumerable<T> enumerable, System.Func<T, float> weightSelector)
    {
        if (enumerable == null || !enumerable.Any())
            throw new System.ArgumentException("Cannot pick a random element from an empty or null collection.");

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
    /// Returns a weighted random element from the collection within the index boundaries of the given Ranged.
    /// </summary>
    public static T GetRandom<T>(this IEnumerable<T> enumerable, Ranged indexRange, System.Func<T, float> weightSelector)
    {
        if (enumerable == null || !enumerable.Any())
            throw new System.ArgumentException("Cannot pick a random element from an empty or null collection.");

        int count = enumerable.Count();
        int minIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.min), 0, count - 1);
        int maxIndex = Mathf.Clamp(Mathf.RoundToInt(indexRange.max), 0, count - 1);

        if (minIndex > maxIndex)
        {
            (minIndex, maxIndex) = (maxIndex, minIndex);
        }

        // Extract the sub-range using LINQ and pass it to the standard weighted GetRandom
        int rangeCount = maxIndex - minIndex + 1;
        IEnumerable<T> subRange = enumerable.Skip(minIndex).Take(rangeCount);

        return subRange.GetRandom(weightSelector);
    }
}
}