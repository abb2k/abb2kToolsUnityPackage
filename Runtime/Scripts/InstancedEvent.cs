using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools
{
    public enum ListenerResult
    {
        Propagate,
        Block
    }

    public static class InstancedEventHandler
    {
        private static Dictionary<System.Type, InstancedEventBaseOpaque> events = new();

        internal static bool IsSpawning = false;

        internal static T GetSharedEventInstance<T>() where T : InstancedEventBaseOpaque
        {
            if (!events.TryGetValue(typeof(T), out var existing))
            {
                IsSpawning = true;
                T newEvent = System.Activator.CreateInstance<T>();
                IsSpawning = false;
                events.Add(typeof(T), newEvent);
                return newEvent;
            }

            return (T)existing;
        }
    }

    public class ListenerHandle
    {
        public int Priority { get; internal set; }
        public System.Delegate Callback { get; private set; }
        private InstancedEventBaseOpaque owner;

        internal bool isEnabled = true;

        public ListenerHandle(System.Delegate callback, InstancedEventBaseOpaque owner)
        {
            this.Callback = callback;
            this.owner = owner;
        }

        public void SetEnabled(bool enabled) => isEnabled = enabled;

        public void Destroy()
        {
            owner.Remove(this);
        }

        public ListenerHandle BindTo(MonoBehaviour owner)
        {
            owner.destroyCancellationToken.Register(Destroy);
            return this;
        }
    }

    public abstract class InstancedEventBaseOpaque
    {
        public class ListenerPriorityComparer : IComparer<ListenerHandle>
        {
            public int Compare(ListenerHandle x, ListenerHandle y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return 1;
                if (y == null) return -1;

                int priorityComparison = y.Priority.CompareTo(x.Priority);

                if (priorityComparison == 0)
                {
                    return y.GetHashCode().CompareTo(x.GetHashCode());
                }

                return priorityComparison;
            }
        }

        protected SortedSet<ListenerHandle> instancesByPriority = new(new ListenerPriorityComparer());

        internal void SendBase(System.Func<System.Delegate, ListenerResult> onCallback)
        {
            foreach (var handle in instancesByPriority)
            {
                if (!handle.isEnabled) continue;

                if (onCallback(handle.Callback) == ListenerResult.Block) break;
            }
        }

        internal ListenerHandle ListenBase(System.Delegate callback, int priority)
        {
            var listener = new ListenerHandle(callback, this);
            listener.Priority = priority;

            instancesByPriority.Add(listener);
            return listener;
        }

        internal void Remove(ListenerHandle handle)
        {
            instancesByPriority.Remove(handle);
        }
    }

    public abstract class InstancedEventBase<TSelf> : InstancedEventBaseOpaque where TSelf : InstancedEventBaseOpaque
    {
        public InstancedEventBase()
        {
            if (!InstancedEventHandler.IsSpawning)
            {
                throw new System.InvalidOperationException($"Cannot use 'new' to create instanced events. Attempted to create new instance of {typeof(TSelf).Name}");
            }
        }

        internal static TSelf Get()
        {
            return InstancedEventHandler.GetSharedEventInstance<TSelf>();
        }
    }

    public abstract class InstancedEvent<TSelf> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf>
    {
        public static void Send() => Get().SendBase(dele =>
        {
            if (dele is System.Func<ListenerResult> func)
                return func();
            
            return ListenerResult.Propagate;
        });

        public static ListenerHandle Listen(System.Func<ListenerResult> callback, int priority = 0) => Get().ListenBase(callback, priority);
    }

    public abstract class InstancedEvent<TSelf, T1> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1>
    {
        public static void Send(T1 param1) => Get().SendBase(dele =>
        {
            if (dele is System.Func<T1, ListenerResult> func)
                return func(param1);
            
            return ListenerResult.Propagate;
        });

        public static ListenerHandle Listen(System.Func<T1, ListenerResult> callback, int priority = 0) => Get().ListenBase(callback, priority);
    }

    public abstract class InstancedEvent<TSelf, T1, T2> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1, T2>
    {
        public static void Send(T1 param1, T2 param2) => Get().SendBase(dele =>
        {
            if (dele is System.Func<T1, T2, ListenerResult> func)
                return func(param1, param2);
            
            return ListenerResult.Propagate;
        });

        public static ListenerHandle Listen(System.Func<T1, T2, ListenerResult> callback, int priority = 0) => Get().ListenBase(callback, priority);
    }

    public abstract class InstancedEvent<TSelf, T1, T2, T3> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1, T2, T3>
    {
        public static void Send(T1 param1, T2 param2, T3 param3) => Get().SendBase(dele =>
        {
            if (dele is System.Func<T1, T2, T3, ListenerResult> func)
                return func(param1, param2, param3);
            
            return ListenerResult.Propagate;
        });

        public static ListenerHandle Listen(System.Func<T1, T2, T3, ListenerResult> callback, int priority = 0) => Get().ListenBase(callback, priority);
    }

    public abstract class InstancedEvent<TSelf, T1, T2, T3, T4> : InstancedEventBase<TSelf> where TSelf : InstancedEvent<TSelf, T1, T2, T3, T4>
    {
        public static void Send(T1 param1, T2 param2, T3 param3, T4 param4) => Get().SendBase(dele =>
        {
            if (dele is System.Func<T1, T2, T3, T4, ListenerResult> func)
                return func(param1, param2, param3, param4);
            
            return ListenerResult.Propagate;
        });

        public static ListenerHandle Listen(System.Func<T1, T2, T3, T4, ListenerResult> callback, int priority = 0) => Get().ListenBase(callback, priority);
    }
}