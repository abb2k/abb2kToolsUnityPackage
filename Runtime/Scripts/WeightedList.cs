using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools
{
    [Serializable]
    public class WeightedListElement<T>
    {
        public T element;

        [Range(0f, 100f)]
        public float _weight = 100f;
    }


    [Serializable]
    public class WeightedList<T> : IEnumerable<WeightedListElement<T>>
    {
        [SerializeField]
        public List<WeightedListElement<T>> elements = new();

        public IEnumerator<WeightedListElement<T>> GetEnumerator() => elements.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count => elements.Count;

        public WeightedListElement<T> this[int index]
        {
            get => elements[index];
            set => elements[index] = value;
        }

        public void Add(T element, float weight = 100f)
        {
            elements.Add(new WeightedListElement<T> { element = element, _weight = weight });
        }

        public void Remove(WeightedListElement<T> item) => elements.Remove(item);

        public void RemoveAt(int index) => elements.RemoveAt(index);

        private float WeightGetter(WeightedListElement<T> element) => element._weight;

        public T Pick()
        {
            if (elements.Count == 0) return default;
            return elements.GetRandom(WeightGetter).element;
        }
    }
}