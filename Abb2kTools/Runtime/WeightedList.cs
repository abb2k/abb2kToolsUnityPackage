using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
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
        [HideLabel]
        public T element;
        [SerializeField, HideInInspector]
        public float _weight = 100;

        [ShowInInspector]
        [PropertyRange(0f, 100f)]
        public float Weight
        {
            get => _weight;
            set
            {
                _weight = value;

                OnWeightChanged?.Invoke(this);
            }
        }

        [DisplayAsString, HideLabel, PropertyOrder(1)]
        public string percent;

        public void OnBeforeSerialize() {}

        public void OnAfterDeserialize()
        {
            int invList = 0;
            if (OnWeightChanged != null)
                invList = OnWeightChanged.GetInvocationList().Length;

            if (invList == 0)
                percent = "Not part of any WeightedList.";
        }
        
        public event UnityAction<WeightedListOption<T>> OnWeightChanged;
    }

    [System.Serializable, InlineProperty, HideLabel]
    public class WeightedList<T> : ISerializationCallbackReceiver
    {
        [SerializeField]
        [LabelText("@$property.ParentValueProperty.NiceName + \" (Weight: \" + overallWeight.ToString() + \")\"")]
        [ListDrawerSettings(CustomAddFunction = "CustomAddFunction", CustomRemoveElementFunction = "OnRemoveItem", CustomRemoveIndexFunction = "OnRemoveItemIndex", OnBeginListElementGUI = "OnListChanged")]
        private List<WeightedListOption<T>> elements;
        private float overallWeight;

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

        public void Add(T element, float weight)
        {
            elements.Add(new WeightedListOption<T>(element, weight));
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

        public void OnBeforeSerialize(){}

        public void OnAfterDeserialize()
        {
            OnListChanged();
        }
    }
}


