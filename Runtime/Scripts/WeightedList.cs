using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace Abb2kTools
{
    [System.Serializable]
    public class WeightedListOption<T> : ISerializationCallbackReceiver
    {
        public WeightedListOption(T element, float weight)
        {
            this.element = element;
            this._weight = weight;
        }
#if ODIN_INSPECTOR
        [HideLabel]
#endif
        public T element;
        [SerializeField]
#if ODIN_INSPECTOR
        [HideInInspector]
#else
        [Range(0, 100)]
#endif
        private float _weight = 100;

#if ODIN_INSPECTOR
        [ShowInInspector]
        [PropertyRange(0f, 100f)]
#endif
        public float Weight
        {
            get => _weight;
            set
            {
                _weight = value;

                #if ODIN_INSPECTOR
                OnWeightChanged?.Invoke(this);
                #endif
            }
        }

#if ODIN_INSPECTOR
        [DisplayAsString, HideLabel, PropertyOrder(1)]
        public string percent;
#endif

        public void OnBeforeSerialize() {}

        public void OnAfterDeserialize()
        {
#if ODIN_INSPECTOR
            int invList = 0;
            if (OnWeightChanged != null)
                invList = OnWeightChanged.GetInvocationList().Length;

            if (invList == 0)
                percent = "Not part of any WeightedList.";
#endif
        }
        
#if ODIN_INSPECTOR
        public event UnityAction<WeightedListOption<T>> OnWeightChanged;
#endif
    }

    [System.Serializable]
#if ODIN_INSPECTOR
    [InlineProperty, HideLabel]
#endif
    public class WeightedList<T> : IEnumerable<WeightedListOption<T>>, IEnumerable, ISerializationCallbackReceiver
    {
        [SerializeField]
#if ODIN_INSPECTOR
        [LabelText("@$property.ParentValueProperty.NiceName + \" (Weight: \" + overallWeight.ToString() + \")\"")]
        [ListDrawerSettings(CustomAddFunction = "CustomAddFunction", CustomRemoveElementFunction = "OnRemoveItem", CustomRemoveIndexFunction = "OnRemoveItemIndex", OnBeginListElementGUI = "OnListChanged")]
#endif
        private List<WeightedListOption<T>> elements = new();
#if ODIN_INSPECTOR
        private float overallWeight;

        public int Count => elements.Count;

        public bool IsReadOnly => false;

        public WeightedListOption<T> this[int index] { get => elements[index]; set => elements[index] = value; }

        private WeightedListOption<T> CustomAddFunction()
        {
            var newEl = new WeightedListOption<T>(default, 100);

            Subscribe(newEl);

            return newEl;
        }
        private void OnRemoveItem(WeightedListOption<T> item)
        {
            if (item != null)
                Unsubscribe(item);
            elements.Remove(item);

            OnListChanged();
        }

        private void OnRemoveItemIndex(int index)
        {
            if (elements[index] != null)
                Unsubscribe(elements[index]);
            elements.RemoveAt(index);

            OnListChanged();
        }
#endif

        public void Add(T element, float weight)
        {
            this.Add(new WeightedListOption<T>(element, weight));
        }

        public void RemoveAll(T element)
        {
            elements.RemoveAll(e => EqualityComparer<T>.Default.Equals(e.element, element));
        }

        public T ChooseRandom()
        {
            if (elements.Count == 0)
                return default;

            float totalWeight = elements.Sum(e => e.Weight);
            float randomValue = UnityEngine.Random.value * totalWeight;

            float cumulative = 0f;
            foreach (var option in elements)
            {
                cumulative += option.Weight;
                if (randomValue <= cumulative)
                    return option.element;
            }

            return elements[^1].element;
        }

#if ODIN_INSPECTOR
        private void Subscribe(WeightedListOption<T> item)
        {
            item.OnWeightChanged -= HandleWeightChanged;
            item.OnWeightChanged += HandleWeightChanged;
        }

        private void Unsubscribe(WeightedListOption<T> item)
        {
            item.OnWeightChanged -= HandleWeightChanged;
        }

        private void HandleWeightChanged(WeightedListOption<T> option)
        {
            OnListChanged();
        }

        private void OnListChanged()
        {
            overallWeight = elements.Sum(e => e.Weight);
            
            foreach (var item in elements)
            {
                if (item == null) continue;
                Subscribe(item);
                item.percent = $"Chance in percentages: {(overallWeight == 0 ? 0 : item.Weight / overallWeight * 100)}%";
            }
        }
#endif

        public void OnBeforeSerialize(){}

        public void OnAfterDeserialize()
        {
#if ODIN_INSPECTOR
            OnListChanged();
#endif
        }

        public IEnumerator<WeightedListOption<T>> GetEnumerator()
        {
            return elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(WeightedListOption<T> item)
        {
            elements.Add(item);
        }

        public void Clear()
        {
            elements.Clear();
        }

        public bool Contains(WeightedListOption<T> item)
        {
            return elements.Contains(item);
        }

        public bool Contains(T item)
        {
            return elements.Any(x => x.element.Equals(item) || ReferenceEquals(x.element, item));
        }

        public void CopyTo(WeightedListOption<T>[] array, int arrayIndex)
        {
            elements.CopyTo(array, arrayIndex);
        }

        public bool Remove(WeightedListOption<T> item)
        {
            return elements.Contains(item);
        }

        public int IndexOf(WeightedListOption<T> item)
        {
            return elements.IndexOf(item);
        }

        public void Insert(int index, WeightedListOption<T> item)
        {
            elements.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            elements.RemoveAt(index);
        }
    }
}


