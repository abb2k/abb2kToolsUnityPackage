using UnityEngine;

namespace Abb2kTools {
    [System.Serializable]
    public struct Ranged : System.IEquatable<Ranged>
    {
        public float min;
        public float max;

        public float? _lastChosenValue;
        public float LastChosenValue {
            get {
                if (_lastChosenValue == null)
                    return GetRandomInRange();
                return _lastChosenValue.Value;
            }
        }

        public Ranged(float min, float max, bool randomize = true)
        {
            this.min = min;
            this.max = max;

            this._lastChosenValue = null;
        }
        /// <summary>
        /// A range from 0 to 1.
        /// </summary>
        public static readonly Ranged range01 = new(0, 1, false);

        /// <summary>
        /// A range from -1 to 1.
        /// </summary>
        public static readonly Ranged rangeOneMinusOne = new(-1, 1, false);

        public static implicit operator float(Ranged r) => r.LastChosenValue;
        public static implicit operator int(Ranged r) => Mathf.RoundToInt(r.LastChosenValue);

        public static Ranged operator *(Ranged a, Ranged b) => new(a.min * b.min, a.max * b.max);
        public static Ranged operator *(Ranged a, float b) => new(a.min * b, a.max * b);
        public static Ranged operator *(float a, Ranged b) => new(a * b.min, a * b.max);
        public static Ranged operator *(Ranged a, int b) => new(a.min * b, a.max * b);
        public static Ranged operator *(int a, Ranged b) => new(a * b.min, a * b.max);
        public static Ranged operator /(Ranged a, Ranged b) => new(a.min / b.min, a.max / b.max);
        public static Ranged operator /(Ranged a, float b) => new(a.min / b, a.max / b);
        public static Ranged operator /(float a, Ranged b) => new(a / b.min, a / b.max);
        public static Ranged operator /(Ranged a, int b) => new(a.min / b, a.max / b);
        public static Ranged operator /(int a, Ranged b) => new(a / b.min, a / b.max);
        public static Ranged operator +(Ranged a, Ranged b) => new(a.min + b.min, a.max + b.max);
        public static Ranged operator +(Ranged a, float b) => new(a.min + b, a.max + b);
        public static Ranged operator +(float a, Ranged b) => new(a + b.min, a + b.max);
        public static Ranged operator +(Ranged a, int b) => new(a.min + b, a.max + b);
        public static Ranged operator +(int a, Ranged b) => new(a + b.min, a + b.max);
        public static Ranged operator -(Ranged a, Ranged b) => new(a.min - b.min, a.max - b.max);
        public static Ranged operator -(Ranged a, float b) => new(a.min - b, a.max - b);
        public static Ranged operator -(float a, Ranged b) => new(a - b.min, a - b.max);
        public static Ranged operator -(Ranged a, int b) => new(a.min - b, a.max - b);
        public static Ranged operator -(int a, Ranged b) => new(a - b.min, a - b.max);


        public float GetRandomInRange()
        {
            _lastChosenValue = Random.Range(min, max);
            return _lastChosenValue.Value;
        }

        public int GetRandomIntInRange()
        {
            _lastChosenValue = Random.Range(Mathf.RoundToInt(min), Mathf.RoundToInt(max) + 1);
            return Mathf.RoundToInt(_lastChosenValue.Value);
        }

        public bool Equals(Ranged other)
        {
            return this.min == other.min && this.max == other.max;
        }

        override public string ToString()
        {
            return $"[{min}, {max}]";
        }
    }
}